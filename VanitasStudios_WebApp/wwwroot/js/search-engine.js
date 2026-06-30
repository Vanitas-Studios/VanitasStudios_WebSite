let searchInput, autocompleteBox, suggestionsList, akinatorBox, akinatorLink, btnSearch, btnReset, netflixSection, algoSection, resultsGrid;
let activeTagIds = [];

function initMyEditor(config) {
    searchInput = document.getElementById(config.searchInput);
    autocompleteBox = document.getElementById(config.autocompleteBox);
    suggestionsList = document.getElementById(config.suggestionsList);
    akinatorBox = document.getElementById(config.akinatorBox);
    akinatorLink = document.getElementById(config.akinatorLink);
    btnSearch = document.getElementById(config.btnSearch);
    btnReset = document.getElementById(config.btnReset);
    netflixSection = document.getElementById(config.netflixSection);
    algoSection = document.getElementById(config.algoSection);
    resultsGrid = document.getElementById(config.resultsGrid);

    setupEventListeners();
}

function setupEventListeners() {
    searchInput.addEventListener("input", async function () {
        autocompleteFunc();
    });

    document.addEventListener("click", function (e) {
        if (!searchInput.contains(e.target) && !autocompleteBox.contains(e.target)) {
            autocompleteBox.classList.remove("show");
        }
    });

    btnSearch.addEventListener("click", executeAkinatorSearch);
    searchInput.addEventListener("keypress", function (e) {
        if (e.key === "Enter") {
            executeAkinatorSearch();
        }
    });

    akinatorLink.addEventListener("click", function (e) {
        e.preventDefault();
        const tagId = parseInt(this.getAttribute("data-tag-id"));
        if (tagId && !activeTagIds.includes(tagId)) {
            activeTagIds.push(tagId);
            searchInput.value = ""; // Svuota il testo libero per dare priorità al filtro selezionato
            executeAkinatorSearch();
        }
    });

    btnReset.addEventListener("click", function () {
        activeTagIds = [];
        searchInput.value = "";
        akinatorBox.classList.add("d-none");
        algoSection.classList.add("d-none");
        netflixSection.classList.remove("d-none");
    });
}

// 🔄 Funzione di comunicazione flessibile (Supporta sia GET che POST)
async function commitToServer(handlerName, payload = null, method = 'GET') {
    try {
        let url = `?handler=${handlerName}`;
        const options = { method: method, headers: {} };

        if (method === 'GET' && payload) {
            // Se è una GET, appendiamo il payload (che sarà una stringa di parametri) all'URL
            url += payload;
        } else if (method === 'POST' && payload) {
            // Se è una POST (es. per salvare dati futuri), gestiamo il body
            const isFormData = payload instanceof FormData;
            options.body = isFormData ? payload : JSON.stringify(payload);
            if (!isFormData) {
                options.headers['Content-Type'] = 'application/json';
            }
            // Token di sicurezza per le POST in Razor Pages
            if (typeof getAntiforgeryToken === "function") {
                options.headers['RequestVerificationToken'] = getAntiforgeryToken();
            }
        }

        const response = await fetch(url, options);
        if (!response.ok) throw new Error('Network response was not ok');

        // Restituiamo direttamente l'oggetto JSON già pronto
        return await response.json();
    }
    catch (error) {
        console.error('Error communicating with server:', error);
        return null;
    }
}

async function autocompleteFunc() {
    const term = searchInput.value.trim();

    if (term.length < 1) {
        autocompleteBox.classList.remove("show");
        return;
    }

    try {
        // Costruiamo la query string per la GET: ?handler=SearchAutocomplete&term=abc
        const queryString = `&term=${encodeURIComponent(term)}`;
        const suggestions = await commitToServer("SearchAutocomplete", queryString, 'GET');

        console.log("Tag ricevuti dal server:", suggestions);

        if (suggestions && suggestions.length > 0) {
            suggestionsList.innerHTML = "";

            suggestions.forEach(tag => {
                // Gestiamo in modo sicuro se il server risponde in camelCase o PascalCase
                const id = tag.tagId || tag.TagId;
                const name = tag.tagName || tag.TagName;
                const cat = tag.category || tag.Category || 'Generico';

                const item = document.createElement("button");
                item.type = "button";
                item.className = "dropdown-item text-white bg-transparent border-0 py-2 w-100 text-start hover-effect";
                item.innerHTML = `<span style="color: #bc9cff;">#</span>${name} <small class="text-muted ms-2">(${cat})</small>`;

                item.addEventListener("click", function () {
                    if (!activeTagIds.includes(id)) {
                        activeTagIds.push(id);
                    }
                    searchInput.value = "";
                    autocompleteBox.classList.remove("show");
                    executeAkinatorSearch();
                });

                suggestionsList.appendChild(item);
            });

            autocompleteBox.classList.add("show");
        } else {
            autocompleteBox.classList.remove("show");
        }
    } catch (error) {
        console.error("Errore nel recupero dell'autocomplete:", error);
    }
}

async function executeAkinatorSearch() {
    const userText = searchInput.value.trim();

    // 🎯 Costruiamo la query string completa con testo e TUTTI i tag accumulati
    let queryString = `&userText=${encodeURIComponent(userText)}`;
    activeTagIds.forEach(id => {
        queryString += `&selectedTagIds=${id}`;
    });

    try {
        // Passiamo la stringa all'endpoint GET
        const data = await commitToServer("ExecuteAkinatorSearch", queryString, 'GET');

        if (!data) return;

        netflixSection.classList.add("d-none");
        algoSection.classList.remove("d-none");

        if (!data.isFinalResult && data.nextTagIdSuggested) {
            akinatorLink.textContent = `#${data.nextQuestionText}`;
            akinatorLink.setAttribute("data-tag-id", data.nextTagIdSuggested);
            akinatorBox.classList.remove("d-none");
        } else {
            akinatorBox.classList.add("d-none");
        }

        renderResults(data.articles);

    } catch (error) {
        console.error("Errore durante l'esecuzione dell'algoritmo:", error);
    }
}

function renderResults(articles) {
    resultsGrid.innerHTML = "";

    if (!articles || articles.length === 0) {
        resultsGrid.innerHTML = `<div class="col-100 text-center my-5 w-100"><p class="text-muted fs-5">Nessun frammento trovato con questi filtri.</p></div>`;
        return;
    }

    articles.forEach(art => {
        // FALLBACK DI SICUREZZA: Intercettiamo sia le risposte camelCase che PascalCase dal server
        const id = art.id || art.Id;
        const title = art.title || art.Title;
        const slug = art.slug || art.Slug || "articolo";
        const description = art.description || art.Description || "";
        const coverImageUrl = art.coverImageUrl || art.CoverImageUrl || "/media/placeholder-default.png";

        const cardCol = document.createElement("div");
        cardCol.className = "col";
        cardCol.innerHTML = `
            <div class="card bg-transparent border-secondary h-100">
                <a href="/Content/${id}/${slug}" class="text-decoration-none text-white h-100 d-flex flex-column">
                    <div class="card-img-wrapper" style="position: relative; overflow: hidden; min-height: 140px; background-color: #222;">
                        <img src="${coverImageUrl}" class="card-img-top" alt="${title}" style="width: 100%; height: 100%; object-fit: cover;">
                    </div>
                    <div class="card-body d-flex flex-column justify-content-between">
                        <div>
                            <h3 class="card-title h5">${title}</h3>
                            <p class="card-text text-muted small">${description}</p>
                        </div>
                    </div>
                </a>
            </div>
        `;
        resultsGrid.appendChild(cardCol);
    });
}