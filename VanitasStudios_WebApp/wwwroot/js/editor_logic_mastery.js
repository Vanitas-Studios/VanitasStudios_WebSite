let editor, sidebarList, articleId, articleLastModifiedFromServer, mainTitle;
let existingIds = [];

//Variabili per salvataggio debounce
let isDirty = false;
const DEBOUNCE_DELAY = 5000;
const triggerAutosave = debounce(() => saveFullContent(), DEBOUNCE_DELAY);

// Variabile per il badge in stato di drag 
let draggedBadge = null;

let isSavingSection = false; // "Semaforo" per le sezioni singole
let isSavingFull = false;    // "Semaforo" per il salvataggio globale
let sectionTaskQueue = [];
let isProcessingQueue = false;


// Variabili globali per Tag
let debouncedSearch;
let tagInput, suggestionsMenu, tagsContainer;

// Funzione per ottenere il token antiforgery
const getAntiforgeryToken = () => {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
};

function initMyEditor(config) {
    editor = document.getElementById(config.editorId);
    sidebarList = document.getElementById(config.sidebarId);
    articleId = config.articleId;
    articleLastModifiedFromServer = config.lastModified;
    tagInput = document.getElementById(config.tagInput);
    suggestionsMenu = document.getElementById(config.suggestionsMenu);
    tagsContainer = document.getElementById(config.tagsContainer);
    mainTitle = document.getElementById(config.mainTitle)

    // Ora attacchiamo gli eventi perché siamo sicuri che il DOM c'è
    setupEventListeners();

    // Sincronizziamo e ripristiniamo
    syncExistingIds();
    checkAndRestoreBackup();
    updateSidebar();
}

function setupEventListeners() {

    // Forza il browser a usare <br> invece di creare nuovi <div> o <p>
    document.execCommand('defaultParagraphSeparator', false, 'br');

    editor.addEventListener("keydown", async (e) => {
        if (e.key === "Enter") {
            handleEnterKey(e);
        }
    });

    mainTitle.addEventListener("input", () => {
        triggerAutosave();
    });

    // MODIFICATO: L'input imposta solo il flag "isDirty" istantaneamente,
    // ma i calcoli pesanti e il salvataggio partono SOLO quando l'utente si ferma!
    editor.addEventListener("input", () => {
        isDirty = true;
        updateSaveStatusIndicator("Modifiche non salvate...", "warning");
        triggerAutosave(); // Chiama il debounce aggiornato qui sotto
    });

    // Funzioni per il drag and drop nella sidebar (riordinamento)
    sidebarList.addEventListener("dragstart", function (e) {
        // Identifichiamo il badge trascinato
        draggedBadge = e.target;
        e.target.classList.add("dragging");
    });

    sidebarList.addEventListener("dragend", async (e) => {
        handleSideBarDragEnd(e);
    });

    sidebarList.addEventListener("dragover", (e) => {
        e.preventDefault();
        handleSideBarDragOver(e);
    });

    //Funzioni per i drop di file media 
    // Drag and Drop Media File
    editor.addEventListener("dragover", function (e) {
        e.preventDefault();
        editor.classList.add("editor-drag-over");
        console.log("Drag over editor");
    });

    editor.addEventListener("dragleave", (e) => {
        e.preventDefault();
        editor.classList.remove("editor-drag-over");
    });

    // Gestione del drop dei file
    editor.addEventListener("drop", async (e) => {
        e.preventDefault();
        editor.classList.remove("editor-drag-over");

        handleFileDrop(e);
    });

    //Listener sul evento Incolla 
    editor.addEventListener("paste", (event) => {
        handlePasteEvent(event);
    });

    //Chiediamo se utente vuole uscire
    window.addEventListener('beforeunload', (e) => {
        if (isDirty) {
            e.preventDefault();
            e.returnValue = "Ci sono modifiche non salvate sul server. Vuoi uscire comunque?";
        }
    });

    debouncedSearch = debounce((query) => {
        fetchTagSuggestions(query);
    }, 300);

    if (tagInput) {
        //Evento input per tag search
        tagInput.addEventListener("input", function () {
            const query = this.value.trim();

            if (query.length < 2) {
                suggestionsMenu.style.display = "none";
                return;
            }

            // Chiamata alla funzione debouncata
            debouncedSearch(query);
        });
    }
}

function syncExistingIds() {
    // Aggiorna la lista degli ID esistenti, escludendo quelli temporanei
    existingIds = Array.from(editor.querySelectorAll(".editor-section"))
        .map(wrapper => wrapper.getAttribute("data-section-id"))
        .filter(id => id !== null && !id.startsWith("temp-"));
}

async function handleEnterKey(e) {
    const selection = window.getSelection();
    if (!selection.rangeCount) return;

    const range = selection.getRangeAt(0);
    const currentNode = range.startContainer;
    const text = currentNode.textContent || "";

    // Controlliamo se stiamo scrivendo un comando per una NUOVA sezione
    const isNewSectionCommand = /^##\s*(.+)/.test(text);

    if (isNewSectionCommand) {
        e.preventDefault();

        const regex = /^##\s*(.+)/;
        const match = text.match(regex);

        const titleText = match[1].trim();

        // 1. Identifichiamo dove siamo
        const currentWrapper = currentNode.nodeType === 3 ?
            currentNode.parentElement.closest(".editor-section") :
            currentNode.closest(".editor-section");

        // 2. Puliamo la riga attuale (dove l'utente ha scritto ##)
        currentNode.textContent = "";

        // 3. Creiamo SEMPRE un nuovo blocco per la nuova sezione
        // Usiamo un ID temporaneo perché è una sezione nuova di zecca
        const tempId = `temp-${Date.now()}`;
        const { wrapper, contentArea } = createSectionBlock(tempId, titleText);

        // 4. LOGICA DI POSIZIONAMENTO (Il "cuore" del fix)
        if (currentWrapper) {
            // Se siamo già in una sezione, la nuova deve nascere DOPO
            currentWrapper.after(wrapper);
        } else {
            // Se siamo fuori, rimpiazziamo il div/p generico in cui eravamo
            const rowToReplace = currentNode.nodeType === 3 ? currentNode.parentElement : currentNode;
            if (rowToReplace !== editor && editor.contains(rowToReplace)) {
                rowToReplace.replaceWith(wrapper);
            } else {
                editor.appendChild(wrapper);
            }
        }

        // 5. SALVATAGGIO: È una nuova sezione, quindi chiamiamo enqueueSectionSave
        // Non facciamo l'update qui, perché l'utente ha esplicitamente usato "##" 
        // per creare un nuovo capitolo/sezione.
        await enqueueSectionSave(wrapper);

        // 6. FOCUS
        updateCursorPos(contentArea);
    }
    else {
        // INVIO NORMALE
        e.preventDefault();

        const selection = window.getSelection();
        if (!selection.rangeCount) return;
        const range = selection.getRangeAt(0);

        // 1. Creiamo il BR e un nodo di testo "invisibile"
        // Lo Zero Width Space (\u200B) dà corpo alla riga senza mostrare nulla
        const br = document.createElement("br");
        const zeroWidthSpace = document.createTextNode("\u200B");

        range.deleteContents();

        // 2. Inseriamo prima il BR e poi lo spazio invisibile
        range.insertNode(zeroWidthSpace);
        range.insertNode(br);

        // 3. Posizioniamo il cursore esattamente dopo il BR, sullo spazio invisibile
        range.setStartAfter(br);
        range.setEndAfter(br);
        range.collapse(false);

        selection.removeAllRanges();
        selection.addRange(range);
    }
}

// Funzione per la queue di Task...attendiamo il salvataggio di ciascuna sezione.
async function enqueueSectionSave(wrapper) {
    // Aggiungiamo il compito alla coda
    sectionTaskQueue.push(wrapper);

    // Se il "motore" è spento, lo accendiamo
    if (!isProcessingQueue) {
        await processQueue();
    }
}

async function processQueue() {
    if (isProcessingQueue) return;
    isProcessingQueue = true;

    while (sectionTaskQueue.length > 0) {
        const wrapper = sectionTaskQueue.shift();
        try {
            await syncSectionToServer(wrapper);
        } catch (e) {
            console.error("Errore nel processing della coda:", e);
        }
    }

    isProcessingQueue = false;
}

// Funzione per creare un blocco di sezione con badge e area di testo
function createSectionBlock(id, titleText, existingWrapper = null) {
    // Crea un wrapper per la sezione, se non esiste
    const wrapper = existingWrapper || document.createElement("div");

    if (!existingWrapper) {
        wrapper.className = "editor-section mb-3 section-loading";
        wrapper.setAttribute("data-section-id", id);
    }

    // Crea il titolo (badge) della sezione, se non esiste già
    let badge = wrapper.querySelector(".badge");

    if (!badge) {
        badge = createBadge("section", `##${titleText}`);
        badge.classList.add("section-title-badge");
        wrapper.prepend(badge);
    }
    badge.textContent = `##${titleText}`;

    // Crea un'area di testo per il contenuto della sezione, se non esiste già
    let contentArea = wrapper.querySelector(".section-content");
    if (!contentArea) {
        contentArea = document.createElement("div");
        contentArea.className = "section-content";
        contentArea.setAttribute("contenteditable", "true");
        if (!contentArea.innerHTML) contentArea.innerHTML = "<br>"; // Inizialmente vuota, pronta a ricevere testo (necessario per il cursore)
        wrapper.appendChild(contentArea);
    }

    return { wrapper, contentArea };
}

//Funzione fabbrica per i badge
function createBadge(type, label) {
    // Creiamo elemento html per il badge
    const badge = document.createElement("span");

    const classStyle = {
        section: "bg-primary",
        image: "bg-success",
        video: "bg-warning",
        file: "bg-info"
    };

    // Applichiamo classi e testo in base al tipo
    badge.className = `badge ${classStyle[type] || "bg-secondary"} me-1`;
    badge.setAttribute("contenteditable", "false");
    badge.innerHTML = label;

    return badge;
}

//Aggiorna la posizione del cursore nella nuova riga.
function updateCursorPos(element) {
    const selection = window.getSelection();
    const newRange = document.createRange();

    // Imposta l'inizio del range all'interno dell'elemento
    newRange.selectNodeContents(element);
    newRange.collapse(false); // Collassa il range all'inizio
    selection.removeAllRanges();
    selection.addRange(newRange);
}

// Funzione per il cursore dopo nodi di testo
//function updateCursorPosAfterNode(node) {
//    const selection = window.getSelection();
//    const range = document.createRange();
//    range.setStartAfter(node);
//    range.collapse(true);
//    selection.removeAllRanges();
//    selection.addRange(range);
//}

// Funzione per gestire l'aggiornamento di una sezione esistente
//async function handleUpdateSection(sectionId, title = null, content = null) {
//    // Non facciamo Update su sezioni temporanee
//    if (sectionId.startsWith("temp-")) return;

//    // Definiamo il nome Handler per l'update
//    const handlerName = "UpdateSection";

//    // Preleviamo il wrapper conoscendo il sio ID
//    const wrapper = editor.querySelector(`.editor-section[data-section-id="${sectionId}"]`);

//    const payload = {
//        SectionId: sectionId,
//        ArticleId: articleId,
//        Order: calculateOrder(wrapper)
//    };

//    // Aggiorniamo solo i campi che sono stati modificati (title o content)
//    if (title !== null) payload.Title = title;
//    if (content !== null) payload.Content = content;

//    const result = await commitToServer(handlerName, payload);
//    if (result.success) {
//        console.log(`Sezione aggiornata! ID: ${sectionId}, Ordine: ${payload.Order}`);
//        updateSidebar();
//    }
//}

// Impacchetta i dati per spedire al server e gestisce la risposta 
// ==========================================
// FIX 1: syncSectionToServer (Risolto selettore CSS e rimosso bug)
// ==========================================
async function syncSectionToServer(wrapper) {

    if (isSavingFull || isSavingSection) {
        console.warn("Posticipated section save: process loading; re-enqueuing...");
        // Se c'è un salvataggio globale in corso, rimettiamo il wrapper in coda per non perderlo
        sectionTaskQueue.push(wrapper);
        return;
    }

    isSavingSection = true;
    wrapper.classList.add("section-loading");

    const handlerName = "SaveSection";
    const order = calculateOrder(wrapper);

    try {
        const titleText = wrapper.querySelector(".section-title-badge")?.textContent.replace("##", "").trim() ?? "Sezione senza titolo";

        // CORRETTO: .section-content invece di .editor-content
        const contentContainer = wrapper.querySelector(".section-content");
        const textContent = contentContainer ? contentContainer.innerHTML : "";

        const cleanContent = textContent
            .replace(/\u200B/g, '')
            .trim();

        const finalContent = cleanContent === "<br>" ? "" : cleanContent;

        const payload = {
            ArticleId: articleId,
            Id: wrapper.getAttribute("data-section-id"),
            Title: titleText,
            Content: finalContent,
            Order: order,
        };

        const result = await commitToServer(handlerName, payload);

        if (result.success) {
            if (editor.contains(wrapper)) {
                // Impostiamo l'ID reale restituito da OnPostSaveSectionAsync
                wrapper.setAttribute("data-section-id", result.sectionId);
                wrapper.classList.remove("section-loading");

                syncExistingIds();
                console.log(`Sezione salvata! ID REALE: ${result.sectionId}, Ordine: ${order}`);
                updateSidebar();
            }
            else {
                console.warn("La sezione non è più presente nell'editor. Preparando per la cancellazione.");
                await checkDeletedSections(result.sectionId);
            }
        }
        else {
            throw new Error("Server rejection");
        }
    }
    catch (error) {
        console.error("Errore nel salvataggio della sezione:", error);
        const badge = wrapper.querySelector(".badge");
        if (badge) badge.className = "badge bg-danger me-1";
    }
    finally {
        isSavingSection = false;
    }
}
// Calcola l'ordine
function calculateOrder(newWrapper) {
    const wrapperList = Array.from(editor.querySelectorAll(".editor-section"));
    const index = wrapperList.indexOf(newWrapper) + 1; // +1 per evitare ordine 0
    return index;
}

// Chiamata al server per salvare la sezione e ottenere un ID
async function commitToServer(handlerName, payload) {

    try {
        // Controlliamo se il payload è un FormData (per upload media) o un oggetto JSON (per le sezioni)
        const isFormData = payload instanceof FormData;

        const options = {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getAntiforgeryToken()
            },
            body: isFormData ? payload : JSON.stringify(payload)
        };

        if (!isFormData) {
            options.headers['Content-Type'] = 'application/json';
        }

        const response = await fetch(`?handler=${handlerName}`, options);
        if (!response.ok) throw new Error('Network response was not ok');

        const result = await response.json();
        return result;

    }
    catch (error) {
        console.error('Error sending section to server:', error);
        return { success: false };
    }
}

// Controlla le sezioni cancellate 
async function checkDeletedSections() {
    const handlerName = "DeleteSection";
    const allWrappers = Array.from(editor.querySelectorAll(".editor-section"));
    const allIds = allWrappers.map(wrapper => wrapper.getAttribute("data-section-id")).filter(id => id !== null && !id.startsWith("temp-"));

    const deletedIds = existingIds.filter(id => !allIds.includes(id));

    if (deletedIds.length > 0) {
        for (const id of deletedIds) {
            existingIds = existingIds.filter(oldId => oldId !== id);
            const payload = { SectionId: id, ArticleId: articleId };
            console.log(`Rilevata eliminazione ID: ${id}`);
            await commitToServer(handlerName, payload);
        }
        // Spostato fuori dal ciclo for: aggiorna la sidebar una volta sola alla fine!
        updateSidebar();
    }
}

// Aggiorna la sidebar laterale con i titoli e ID delle sezioni
function updateSidebar() {

    if (!sidebarList) return; // Controllo di sicurezza nel caso in cui la sidebar non esista

    // Puliamo la sidebar
    sidebarList.innerHTML = "";

    // Prendiamo tutte le sezioni attuali
    const allWrappers = Array.from(editor.querySelectorAll(".editor-section"));
    // Cicliamo
    allWrappers.forEach((wrapper, index) => {
        // Creiamo un elemento li per la sidebar
        const li = document.createElement("li");
        li.className = " draggable mb-2 p-2 rounded border bg-light small d-flex align-items-center";

        // Prendiamo ID della sezione
        const realId = wrapper.getAttribute("data-section-id");
        if (realId && !realId.startsWith("temp-")) {
            li.setAttribute("data-section-id", realId);
        }

        // Prendiamo il titolo (badge) della sezione
        const badge = wrapper.querySelector(".section-title-badge");
        const titleText = badge ? badge.textContent.replace("##", "").trim() : "Sezione senza titolo";

        // Aggiungiamo anche un numero che indica l'ordine
        const orderCircle = `<span class="badge bg-dark rounded-pill me-2">${index + 1}</span>`;
        li.innerHTML = `${orderCircle} <span>${titleText}</span>`;

        // Aggiungiamo un listener per scrollare alla sezione corrispondente quando clicchiamo sull'elemento della sidebar
        li.style.cursor = "pointer";
        li.onclick = () => wrapper.scrollIntoView({ behavior: "smooth", block: "center" });

        // Aggiungiamo attributo per il drag and drop
        li.setAttribute("draggable", "true");

        // Appendiamo alla sidebar
        sidebarList.appendChild(li);

    });
}

async function handleSideBarDragEnd(e) {
    // Puliamo lo stato di drag
    draggedBadge = null;
    e.target.classList.remove("dragging");

    // 1. Preleviamo l'ordine attuale dei badge 
    const badgesInOrder = Array.from(sidebarList.querySelectorAll(".draggable"))
        .map(li => li.getAttribute("data-section-id"))
        .filter(id => id && !id.startsWith("temp-"));

    // 2. Aggiorniamo subito l'ordine nell'editor, basandoci sull'ordine attuale dei badge nella sidebar
    reorderEditorFromSidebar();

    // 3. Aggiorniamo i numeretti della sidebar senza ricaricare tutto
    refreshSidebarNumbers();

    // Handler del metodo per aggiornare l'ordine delle sezioni
    const handlerName = "UpdateOrder";

    try {

        const payload = {
            ArticleId: articleId,
            SortedIds: badgesInOrder
        };

        const result = await commitToServer(handlerName, payload);

        if (!result.success) {
            console.error("Errore nell'aggiornamento dell'ordine sul server.");
        }

    }
    catch (error) {
        console.error("Errore durante l'aggiornamento dell'ordine:", error);
    }
}

function handleSideBarDragOver(e) {
    const afterElement = getDragAfterElement(sidebarList, e.clientY);
    // Se non viene rilevato alcun elemento sotto il mouse, facciamo un append alla fine della lista, altrimenti inseriamo prima dell'elemento rilevato
    if (afterElement == null) {
        sidebarList.appendChild(draggedBadge);
    } else {
        sidebarList.insertBefore(draggedBadge, afterElement);
    }
}

// Funzione per ottenere l'elemento dopo il quale inserire il badge trascinato
function getDragAfterElement(container, y) {
    // Prendiamo tutti gli elementi tranne quello che stiamo trascinando
    const draggableElements = [...container.querySelectorAll(".draggable:not(.dragging)")];

    // Calcoliamo quale elemento è più vicino alla posizione del mouse (quello con l'offset più piccolo e negativo, ovvero quello sotto il mouse)
    return draggableElements.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offset = y - box.top - box.height / 2;
        if (offset < 0 && offset > closest.offset) {
            return { offset: offset, element: child };
        } else {
            return closest;
        }
    }, { offset: Number.NEGATIVE_INFINITY }).element;
}

// Funzione per riordinare le sezioni nell'editor in base all'ordine dei badge nella sidebar
function reorderEditorFromSidebar() {
    const ids = Array.from(sidebarList.querySelectorAll(".draggable"))
        .map(li => li.getAttribute("data-section-id"));

    ids.forEach(id => {
        const wrapper = editor.querySelector(`.editor-section[data-section-id="${id}"]`);
        if (wrapper) {
            editor.appendChild(wrapper);
        }
    });
}

// Funzione per aggiornare i numeretti della sidebar senza ricaricare tutto
function refreshSidebarNumbers() {
    const items = sidebarList.querySelectorAll(".draggable");
    items.forEach((li, index) => {
        const orderBadge = li.querySelector(".badge");
        if (orderBadge) {
            orderBadge.textContent = index + 1;
        }
    });
}
//handle File Drop
//async function handleFileDrop(e) {

//    // Prendiamo i file dal drop
//    const fileList = [...e.dataTransfer.files];
//    if (fileList.length === 0) return; // Se non ci sono file, usciamo

//    // 1. Identifichiamo il punto esatto del rilascio
//    let range;

//    // A. Prova lo standard ufficiale (Firefox e futuri)
//    if (document.caretPositionFromPoint) {
//        const pos = document.caretPositionFromPoint(e.clientX, e.clientY);
//        if (pos) {
//            range = document.createRange();
//            range.setStart(pos.offsetNode, pos.offset);
//            range.collapse(true);
//        }
//    }
//    // B. Prova l'API Webkit (Chrome, Safari, Edge)
//    else if (document.caretRangeFromPoint) {
//        range = document.caretRangeFromPoint(e.clientX, e.clientY);
//    }

//    // C. Fallback di emergenza
//    if (!range) {
//        // Se proprio non riusciamo a trovare il punto,
//        // creiamo un range che punta alla fine dell'editor
//        range = document.createRange();
//        range.selectNodeContents(editor);
//        range.collapse(false);
//    }

//    // 2. Usiamo for...of per gestire correttamente gli await
//    for (const file of fileList) {
//        const fileType = file.type.split('/')[0];
//        if (fileType !== "image" && fileType !== "video") continue;

//        // 3. Creiamo un wrapper per il media (come fatto per le sezioni)
//        const mediaWrapper = createMediaBlock(fileType, file.name, `temp-${Date.now()}`);

//        // Inseriamo il wrapper nella posizione del mouse
//        if (range) {
//            //Risoluzione bug? Ho aggiunto un br per evitare che il puntatore rimanga incastrato all'interno del blocco media
//            const br = document.createElement("br");
//            range.insertNode(br);
//            range.insertNode(mediaWrapper);
//            // Sposta il punto di inserimento dopo il blocco appena creato
//            range.setStartAfter(br);
//            range.collapse(true); // Da capire la differenza reale true/false
//        } else {
//            editor.appendChild(mediaWrapper);
//        }

//        // 4. Preparazione Upload
//        const formData = new FormData();
//        formData.append("ArticleId", articleId);
//        formData.append("file", file);

//        try {
//            const result = await commitToServer("UploadMedia", formData);

//            if (result.success) {

//                const labelText = `[img alt="${result.Alt || 'immagine'}" url="${result.Url}"]`;
//                createMediaBlock(fileType, labelText, result.ID, mediaWrapper, result.url); // Ricreiamo il blocco media con l'URL restituito dal server)

//            } else {
//                createMediaBlock(fileType, file.name, `temp-${Date.now()}`, mediaWrapper, null, true); // Passiamo error=true per mostrare l'errore
//            }
//        } catch (err) {
//            console.error("Errore critico upload:", err);
//        }
//    }
//}

async function handleFileDrop(e) {
    e.preventDefault();

    // Prendiamo i file dal drop
    const fileList = [...e.dataTransfer.files];
    if (fileList.length === 0) return;

    // 1. Identifichiamo il punto esatto del rilascio del mouse nel testo
    let range;
    if (document.caretPositionFromPoint) {
        const pos = document.caretPositionFromPoint(e.clientX, e.clientY);
        if (pos) {
            range = document.createRange();
            range.setStart(pos.offsetNode, pos.offset);
            range.collapse(true);
        }
    } else if (document.caretRangeFromPoint) {
        range = document.caretRangeFromPoint(e.clientX, e.clientY);
    }

    // Fallback di emergenza
    if (!range) {
        range = document.createRange();
        range.selectNodeContents(editor);
        range.collapse(false);
    }

    // 2. RISALITA AL PADRE (.editor-section) E ACCESSO AL CONTENUTO (.section-content)
    let targetNode = range.startContainer;
    let currentWrapper = targetNode.nodeType === 3 ?
        targetNode.parentElement.closest(".editor-section") :
        targetNode.closest(".editor-section");

    let contentArea = null;

    if (currentWrapper) {
        // Se siamo dentro una sezione esistente, recuperiamo la sua area di testo
        contentArea = currentWrapper.querySelector(".section-content");
    } else {
        // SCENARIO DI EMERGENZA: Il file è stato rilasciato fuori da una sezione
        console.log("Media rilasciato fuori da una sezione. Creo blocco di emergenza.");

        // Spacchettiamo il nuovo blocco
        const { wrapper, contentArea: newArea } = createSectionBlock(`temp-${Date.now()}`, "Nuova Sezione Media");

        editor.appendChild(wrapper);
        currentWrapper = wrapper;
        contentArea = newArea;

        range = document.createRange();
        range.selectNodeContents(contentArea);
        range.collapse(false);

        // Salviamo subito la nuova sezione per farle ottenere un ID reale dal DB
        enqueueSectionSave(currentWrapper);
    }

    // EXTRAZIONE ID SEZIONE: Recuperiamo l'attributo data-section-id dal wrapper padre
    const sectionId = currentWrapper.getAttribute("data-section-id");

    // 3. Iterazione sui file rilasciati
    for (const file of fileList) {
        const fileType = file.type.split('/')[0];
        if (fileType !== "image" && fileType !== "video") continue;

        // Creiamo il widget visivo temporaneo di "In caricamento..."
        const mediaWrapper = createMediaBlock(fileType, file.name, `temp-${Date.now()}`);

        // Inseriamo il widget visivo nel punto del rilascio
        const br = document.createElement("br");
        range.insertNode(br);
        range.insertNode(mediaWrapper);

        range.setStartAfter(br);
        range.collapse(true);

        // 4. Preparazione della richiesta FormData (Incluso l'ID della Sezione)
        const formData = new FormData();
        formData.append("file", file);
        formData.append("articleId", articleId);
        formData.append("uploadType", "section");
        formData.append("sectionId", sectionId); // <-- CORREZIONE CRUCIALE: Inviamo l'ID della sezione al C#

        try {
            updateSaveStatusIndicator("Caricamento media...", "saving");

            const result = await commitToServer("UploadMedia", formData);

            // Controlli la proprietà booleana definita nel tuo backend C#
            if (result && result.success) {
                // L'upload è andato a buon fine sul server!
                const labelText = `[img alt="${result.alt || 'immagine'}" url="${result.url}"]`;
                createMediaBlock(fileType, labelText, result.id, mediaWrapper, result.url);
                updateSaveStatusIndicator("Media aggiunto!", "success");
            } else {
                // Il server ha risposto, ma ha riscontrato un errore di validazione o un fallimento controllato
                createMediaBlock(fileType, file.name, `temp-${Date.now()}`, mediaWrapper, null, true);
                updateSaveStatusIndicator("Errore caricamento", "error");
            }


        } catch (err) {
            console.error("Errore critico upload drag&drop:", err);
            createMediaBlock(fileType, file.name, `temp-${Date.now()}`, mediaWrapper, null, true);
            updateSaveStatusIndicator("Errore caricamento", "error");
        }
    }

    // 6. Svegliamo l'autosave globale
    isDirty = true;
    triggerAutosave();
}

async function uploadArticleCover() {
    const fileInput = document.getElementById("coverFileInput");
    // Se non c'è l'input o l'utente ha annullato la selezione, usciamo subito
    if (!fileInput || fileInput.files.length === 0) return;

    const file = fileInput.files[0];

    // Prepariamo il FormData con i parametri esatti richiesti dal C# unificato
    const formData = new FormData();
    formData.append("file", file);
    formData.append("articleId", articleId); // Variabile globale del tuo editor (es. ID dell'articolo corrente)
    formData.append("uploadType", "cover");  // Diciamo al C# che questa è la copertina dell'articolo
    // Nota: sectionId non serve qui, passerà automaticamente come null al server

    updateSaveStatusIndicator("Caricamento copertina...", "saving");

    try {
        // Usiamo la tua commitToServer passando l'handler unificato "UploadMedia"
        const result = await commitToServer("UploadMedia", formData);

        if (result && result.success) {
            const imgPreview = document.getElementById("coverPreview");
            const placeholder = document.getElementById("coverPlaceholder");

            // Aggiorniamo l'interfaccia con l'URL definitivo restituito dal server
            imgPreview.src = result.url;

            // Sistemiamo i display per mostrare l'immagine e nascondere il placeholder
            placeholder.style.display = "none";
            imgPreview.style.display = "block";

            updateSaveStatusIndicator("Copertina salvata!", "success");
        } else {
            alert("Impossibile salvare la copertina. Controlla il file.");
            updateSaveStatusIndicator("Errore copertina", "error");
        }
    } catch (error) {
        console.error("Errore durante l'upload della copertina:", error);
        updateSaveStatusIndicator("Errore critico", "error");
    } finally {
        // Resettiamo l'input del file così l'utente può ricaricare lo stesso file in caso di modifiche
        fileInput.value = "";
    }
}

function createMediaBlock(fileType, labelText, ID, existingMediaBlock = null, url = null, error = false) {
    // 1. Se esiste lo usiamo, altrimenti lo creiamo
    const mediaWrapper = existingMediaBlock || document.createElement("div");

    if (!existingMediaBlock) {
        mediaWrapper.className = "media-wrapper my-3 d-block w-100 border p-2 rounded";
        mediaWrapper.setAttribute("contenteditable", "false");
        mediaWrapper.setAttribute("data-type", fileType);
        mediaWrapper.setAttribute("data-media-id", ID); // ID temporaneo finché non salviamo e otteniamo un ID reale")
    }

    // 2. Cerchiamo il badge esistente o ne creiamo uno nuovo
    let badge = mediaWrapper.querySelector(".badge");

    if (!badge) {
        badge = createBadge(fileType, ""); // Lo creiamo vuoto
        mediaWrapper.appendChild(badge);
    }

    // 3. AGGIORNAMENTO STATO (Sia per nuovi che per esistenti)
    if (error) {
        badge.textContent = `Errore: ${labelText}`;
        badge.className = "badge bg-danger me-1";
    }
    else if (!url) {
        badge.textContent = `Caricamento ${labelText}...`;
        badge.className = `badge bg-warning me-1`; // Reset classi e metti warning
    }
    else {
        badge.textContent = `${labelText}`;
        badge.className = `badge bg-success me-1`; // Passa a success
        mediaWrapper.setAttribute("data-media-id", ID); // Rimuoviamo l'ID temporaneo, ora che abbiamo un URL (e presumibilmente un ID reale dal server)
    }

    return mediaWrapper;
}

//funzione debounce per salvare dopo che utente ha smesso di digitare
function debounce(func, timeout) {
    let timer
    return (...args) => {
        clearTimeout(timer)
        timer = setTimeout(() => { func.apply(this, args); }, timeout);
    };
}

// Funzione per il salvataggio globale (chiamata dal debounce dopo 5 secondi di inattività)
async function saveFullContent() {

    // Se stiamo attivamente salvando una sezione singola (badge), 
    // aspettiamo che abbia finito prima di fare il dump globale
    if (isSavingSection || isProcessingQueue || sectionTaskQueue.length > 0) {
        console.log("Salvataggio globale posticipato: coda sezioni in corso.");
        triggerAutosave();
        return;
    }

    if (isSavingFull) return;

    isSavingFull = true;
    updateSaveStatusIndicator("Salvataggio in corso...", "saving");

    try {
        // 1. Controlliamo le sezioni cancellate SOLO ORA, non a ogni tasto premuto!
        await checkDeletedSections();

        // 2. Serializziamo il contenuto dell'editor
        //const articleData = serializedEditorContent();
        const articleData = serializedEditorContent();

        localStorage.setItem(`article_backup_${articleId}`, JSON.stringify(articleData));

        // 3. Spediamo al server
        const response = await commitToServer("SaveContent", articleData);

        if (response.success) {
            isDirty = false;
            localStorage.removeItem(`article_backup_${articleId}`);
            updateSaveStatusIndicator("Tutte le modifiche salvate", "success");
        }
        else {
            updateSaveStatusIndicator("Errore nel salvataggio server", "error");
            throw new Error("Server rejected save");
        }
    }
    catch (error) {
        console.error("Salvataggio server fallito:", error);
        isDirty = true;
        updateSaveStatusIndicator("Salvataggio locale (Offline)", "warning");
    }
    finally {
        isSavingFull = false;
    }
}


function serializedEditorContent() {
    // 1. Recupera e pulisce il Titolo Principale dell'Articolo
    // (Assicurati che 'mainTitle' sia il riferimento corretto al tuo input HTML del titolo)
    const mainTitleInput = mainTitle && mainTitle.value ? mainTitle.value.trim() : "";

    // 2. Seleziona tutti i blocchi sezione presenti nell'editor
    const sectionElements = Array.from(editor.querySelectorAll(".editor-section"));

    const sections = sectionElements.map((wrapper, index) => {
        const sectionId = wrapper.getAttribute("data-section-id");

        // Estrazione e pulizia del Titolo della Sezione dal Badge (rimuovendo ##)
        const badgeEl = wrapper.querySelector(".section-title-badge");
        let extractedSectionTitle = "Senza Titolo";
        if (badgeEl) {
            extractedSectionTitle = badgeEl.textContent.replace(/^##/, "").trim();
        }

        // Isolamento e pulizia profonda dell'area di testo editabile
        const contentArea = wrapper.querySelector(".section-content");
        let sanitizedHtml = "";

        if (contentArea) {
            let rawHtml = contentArea.innerHTML.trim();

            // Intercettiamo i "falsi vuoti" dell'editor (inclusi i tag div inseriti da Chrome/Firefox sugli invio)
            if (rawHtml === "<br>" || rawHtml === "" || rawHtml === "<p><br></p>" || rawHtml === "<div><br></div>") {
                sanitizedHtml = "";
            } else {
                sanitizedHtml = cleanHtmlContent(rawHtml);
            }
        }

        // Ritorna l'oggetto Sezione mappato sul DTO C# (SectionViewModel)
        return {
            ArticleId: parseInt(articleId), // ID di controllo per la sicurezza dei dati
            Id: sectionId,                  // Stringa (es. "temp-..." o ID reale numerico)
            Title: extractedSectionTitle,   // Titolo pulito (senza ##)
            Content: sanitizedHtml,         // HTML purificato pronto per il DB
            Order: index + 1                // Ordine strutturale basato sulla posizione reale nel DOM
        };
    });

    // Ritorna il Payload globale che mappa perfettamente su 'EditorSavePayload' in C#
    return {
        ArticleId: parseInt(articleId),
        Title: mainTitleInput, // <--- Ecco il titolo dell'articolo principale associato e ripulito
        Sections: sections
    };
}

// Funzione di supporto atomica per rimuovere scorie HTML
function cleanHtmlContent(html) {
    const tempDiv = document.createElement("div");
    tempDiv.innerHTML = html;

    // Rimuove la sequenza invisibile Zero-Width Space (\u200B) e pulisce i div vuoti residui
    let textContent = tempDiv.innerHTML.replace(/\u200B/g, '').replace(/<div><br><\/div>/g, '<br>');
    tempDiv.innerHTML = textContent;

    // Rimuoviamo eventuali badge o nodi di controllo titolo finiti erroneamente dentro l'area testo
    const rogueBadges = tempDiv.querySelectorAll(".section-title-badge, .badge");
    rogueBadges.forEach(b => b.remove());

    // Pulisce attributi spuri da tutti i blocchi di testo interni generati dall'editor editable
    const blocks = tempDiv.querySelectorAll("p, div, span");
    blocks.forEach(block => {
        // Se un blocco interno è rimasto vuoto o ha solo un br, lo rimuoviamo per non creare spazi vuoti giganti nel layout Zen
        if (block.innerHTML.trim() === "" || block.innerHTML === "<br>") {
            block.remove();
        } else {
            block.removeAttribute("contenteditable");
            block.removeAttribute("class");
            block.removeAttribute("style");
        }
    });

    return tempDiv.innerHTML.trim();
}

// Funzione per serializzare il contenuto dell'editor
function serializedEditorContentOlderVersion() {
    const sections = Array.from(editor.querySelectorAll(".editor-section")).map(wrapper => {
        return {
            id: wrapper.getAttribute("data-section-id"),
            title: wrapper.querySelector(".section-title-badge")?.textContent || "",
            content: wrapper.querySelector(".section-content").innerHTML.replace(/\u200B/g, '').replace(/<div><br><\/div>/g, '<br>'),
            order: calculateOrder(wrapper)
        };
    });

    const mainTitleInput = mainTitle.value ? mainTitle.value.trim() : "";

    return {
        articleId: articleId,
        Title: mainTitleInput,
        Sections: sections // CORRETTO: 'Sections' con la S maiuscola per il DTO C#
    };
}

//Funzione controllo LocalStorage 
async function checkAndRestoreBackup() {
    const backupKey = `article_backup_${articleId}`;
    const savedData = localStorage.getItem(backupKey);

    if (savedData) {
        const backup = JSON.parse(savedData);

        // Confrontiamo il timestamp del backup con quello dell'ultimo salvataggio server
        // (Assumendo che il server ti passi la data dell'ultima modifica al caricamento)
        const serverLastModified = new Date(articleLastModifiedFromServer); // --> DA IMPLEMENTARE
        const backupLastModified = new Date(backup.lastUpdate);

        if (backupLastModified > serverLastModified) {
            // Chiediamo conferma all'utente
            const userConfirmed = confirm(
                "Abbiamo trovato una versione più recente (non salvata) di questo articolo sul tuo computer. Vuoi ripristinarla?"
            );

            if (userConfirmed) {
                restoreEditorFromJSON(backup);
            } else {
                // Se rifiuta, puliamo il backup per non chiederglielo più
                localStorage.removeItem(backupKey);
            }
        }
    }
}

// Ricostruiamo il contenuto dentro editor
function restoreEditorFromJSON(data) {
    editor.innerHTML = ""; // Reset

    data.sections.forEach(section => {
        // 1. Creiamo il "guscio" della sezione (Titolo, pulsanti drag, etc.)
        const wrapper = createSectionBlock(section.id, section.title);

        // 2. Troviamo l'area dove va il testo
        const contentArea = wrapper.querySelector(".section-content");

        // 3. Iniettiamo l'HTML così com'era. 
        // Se c'era un badge media, tornerà ad esserci un badge media!
        contentArea.innerHTML = section.content;

        editor.appendChild(wrapper);
    });

    updateSidebar();
    isDirty = false;
    updateSaveStatusIndicator("Versione locale ripristinata", "warning");
}

/**
 * Aggiorna l'indicatore di salvataggio
 * @param {string} message - Messaggio da mostrare
 * @param {string} status - 'success', 'saving', 'error', 'warning'
 */
function updateSaveStatusIndicator(message, status = 'success') {
    const dot = document.getElementById("save-status-dot");
    const text = document.getElementById("save-status-text");

    if (!dot || !text) return;

    // Reset delle classi
    dot.className = "status-dot me-2";
    text.textContent = message;

    // Applichiamo lo stato
    switch (status) {
        case 'success':
            dot.classList.add("dot-success");
            break;
        case 'saving':
            dot.classList.add("dot-saving");
            break;
        case 'error':
            dot.classList.add("dot-danger");
            break;
        case 'warning':
            dot.classList.add("dot-warning");
            break;
    }
}

// Nel caso avessi degli eventi associati ad alcuni tag html, come potrei fare per i media
// devo delegare l'assegnazione degli eventi, altrimenti una volta iniettato un html, gli eventi sono disattivati.
// Invece di mettere onclick su ogni badge, lo metti una volta sola sull'editor
//editor.addEventListener("click", function (e) {
//    if (e.target.classList.contains("btn-delete-media")) {
//        e.target.closest(".media-wrapper").remove();
//        triggerAutoSave();
//    }
//});


//Funzione per il controllo del paste
function handlePasteEvent(e) {
    e.preventDefault();

    // 1. Recupero testo pulito
    const text = (e.clipboardData || window.clipboardData).getData('text/plain');
    if (!text) return;

    // 2. Troviamo dove sta incollando
    const selection = window.getSelection();
    if (!selection.rangeCount) return;
    let range = selection.getRangeAt(0);

    // 3. CONTROLLO SICURO: Il cursore è dentro una .section-content?
    // Usiamo una logica più robusta per evitare eccezioni se startContainer è un nodo di testo (Type 3)
    let contentArea = range.startContainer.nodeType === 3 ?
        range.startContainer.parentElement.closest(".section-content") :
        range.startContainer.closest(".section-content");

    // 4. SCENARIO: Editor vuoto o incolla fuori dalle sezioni
    if (!contentArea) {
        console.log("Incolla rilevato fuori da una sezione. Creazione blocco di emergenza.");

        // CORREZIONE CRUCIALE: Spacchettiamo l'oggetto usando le chiavi corrette { wrapper, contentArea }
        const { wrapper, contentArea: newArea } = createSectionBlock(`temp-${Date.now()}`, "Nuova Sezione");

        // Adesso appendiamo il WIDGET HTML REALE (wrapper) e non l'oggetto JS!
        editor.appendChild(wrapper);

        // FIX SCROLL: Forza l'editor a scorrere visivamente fino al nuovo blocco appena creato
        wrapper.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

        // Aggiorniamo il puntatore alla contentArea della nuova sezione appena creata
        contentArea = newArea;

        // Aggiorniamo il range per posizionarci dentro la nuova area
        range = document.createRange();
        range.selectNodeContents(contentArea);
        range.collapse(false); // Va alla fine
        selection.removeAllRanges();
        selection.addRange(range);

        // Avvisiamo la coda che questa nuova sezione va salvata sul DB
        enqueueSectionSave(wrapper);
    }

    // 5. ESECUZIONE: Inserimento del testo pulito convertendo i ritorni a capo in <br>
    // Se usi createTextNode con testi lunghi multi-linea rischi di perdere i ritorni a capo.
    // Usando un fragment e innerHTML pulito, i ritorni a capo (\n) diventano <br> reali.
    const cleanHtmlText = text.replace(/\n/g, "<br>");
    const template = document.createElement('template');
    template.innerHTML = cleanHtmlText;
    const fragment = template.content;
    const lastNode = fragment.lastChild;

    range.deleteContents();
    range.insertNode(fragment);

    // 6. Spostiamo il cursore subito dopo il testo inserito
    if (lastNode) {
        range.setStartAfter(lastNode);
        range.collapse(true);
        selection.removeAllRanges();
        selection.addRange(range);
    }

    // 7. Sincronizzazione e attivazione Autosave
    isDirty = true;
    triggerAutosave();
}

//Funzione del preview 
async function contentPreview() {
    // 1. Forziamo il salvataggio globale dello stato attuale dell'editor
    await saveFullContent();

    // 2. Prepariamo il payload per C#
    const payload = {
        articleId: articleId
    };

    try {
        // Chiediamo al server il contenuto HTML già renderizzato ed elaborato
        const response = await commitToServer("LoadPreview", payload);

        if (response.success) {
            const contentHtml = response.htmlContent;

            // 3. Recuperiamo il container (Ora l'ID esiste nell'HTML!)
            const iframeContainer = document.getElementById("sectionsBodyContent");
            iframeContainer.innerHTML = ''; // Svuota anteprime precedenti

            // Creiamo l'iframe
            const iframe = document.createElement("iframe");
            iframe.style.height = "100%";
            iframe.style.width = "100%";
            iframe.style.border = "none";

            iframeContainer.append(iframe);

            // 4. Iniettiamo l'HTML dentro l'iframe isolato
            const docIframe = iframe.contentWindow.document;
            docIframe.open();
            docIframe.write(contentHtml);
            docIframe.close();

            // 5. Mostriamo la modale Bootstrap in modo sicuro
            const modalElement = document.getElementById('DocumentPreview');
            // Recupera l'istanza della modale se già creata in precedenza, altrimenti ne crea una nuova
            let previewModal = bootstrap.Modal.getInstance(modalElement);
            if (!previewModal) {
                previewModal = new bootstrap.Modal(modalElement);
            }

            previewModal.show();
        } else {
            alert("Errore nel caricamento della preview dal server.");
        }
    }
    catch (error) {
        console.error("Errore durante il caricamento della preview:", error);
    }
}

//Function for the tags search
async function fetchTagSuggestions(query) {
    try {
        updateSaveStatusIndicator("Ricerca tag...", "saving");

        const response = await fetch(`?handler=SearchTags&query=${encodeURIComponent(query)}&articleId=${articleId}`);
        if (!response.ok) throw new Error("Errore di rete");

        const tags = await response.json();
        renderTagSuggestions(tags);
    } catch (err) {
        console.error("Errore ricerca tag:", err);
    }
}

function renderTagSuggestions(tags) {
    if (!suggestionsMenu) return; // Sicurezza extra per evitare il primo errore!
    suggestionsMenu.innerHTML = "";

    if (tags.length === 0) {
        suggestionsMenu.style.display = "none";
        return;
    }

    tags.forEach(tag => {
        const li = document.createElement("li");
        li.className = "dropdown-item cursor-pointer py-1";
        li.textContent = tag.name;
        li.setAttribute("data-id", tag.id);

        li.addEventListener("click", () => addTagAssociation(tag.id, tag.name));
        suggestionsMenu.appendChild(li);
    });

    suggestionsMenu.style.display = "block";
}

async function addTagAssociation(tagId, tagName) {
    // Sicurezza: controlliamo se questo tag è già stato inserito visivamente nel DOM
    // per evitare duplicati se l'utente clicca due volte di fila
    const exists = tagsContainer.querySelector(`[data-tag-id="${tagId}"]`);
    if (exists) {
        if (suggestionsMenu) suggestionsMenu.style.display = "none";
        if (tagInput) tagInput.value = "";
        return;
    }

    // Prepariamo i dati per il server
    const payload = {
        articleId: articleId,
        tagId: tagId
    };

    try {
        updateSaveStatusIndicator("Associazione tag...", "saving");

        // Mandiamo i dati all'handler C# (es. OnPostAddTag) usando il tuo commitToServer
        // Se commitToServer si aspetta un oggetto JSON, lo gestirà autonomamente con stringify
        const result = await commitToServer("AddTag", payload);

        if (result && result.success) {
            // 1. Creiamo l'elemento badge (la pillola)
            const badge = document.createElement("span");
            badge.className = "badge bg-secondary d-flex align-items-center gap-1 p-2";
            badge.setAttribute("data-tag-id", tagId);
            badge.innerHTML = `
                ${tagName}
                <span class="ms-1 fw-bold text-white-50" style="cursor: pointer; font-size: 1.1rem; line-height: 1;" onclick="removeTagBlock(this, ${tagId})">&times;</span>
            `;

            // 2. Appendiamo la pillola nel contenitore esterno all'editor
            tagsContainer.appendChild(badge);

            // 3. Puliamo l'interfaccia
            if (tagInput) tagInput.value = "";
            if (suggestionsMenu) suggestionsMenu.style.display = "none";

            updateSaveStatusIndicator("Tag aggiunto!", "success");
        } else {
            alert(result.message || "Impossibile associare il tag.");
            updateSaveStatusIndicator("Errore salvataggio tag", "error");
        }
    } catch (err) {
        console.error("Errore durante l'associazione del tag:", err);
        updateSaveStatusIndicator("Errore critico tag", "error");
    }
}

async function removeTagBlock(buttonElement, tagId) {
    // Identifichiamo il badge padre (lo <span>) partendo dall'icona cliccata
    const badgeElement = buttonElement.closest(".badge");
    if (!badgeElement) return;

    const payload = {
        articleId: articleId,
        tagId: tagId
    };

    try {
        updateSaveStatusIndicator("Rimozione tag...", "saving");

        // Mandiamo la richiesta di cancellazione al server (es. OnPostRemoveTag)
        const result = await commitToServer("RemoveTag", payload);

        if (result && result.success) {
            // Rimuoviamo fisicamente la pillola dal div dei tag selezionati
            badgeElement.remove();
            updateSaveStatusIndicator("Tag rimosso!", "success");
        } else {
            alert("Impossibile rimuovere il tag.");
            updateSaveStatusIndicator("Errore rimozione tag", "error");
        }
    } catch (err) {
        console.error("Errore durante la rimozione del tag:", err);
        updateSaveStatusIndicator("Errore critico rimozione", "error");
    }
}

async function changeContentStatus(action) {
    try {
        updateSaveStatusIndicator("Cambio stato in corso...", "saving");

        // Sfruttiamo il tuo helper commitToServer
        const result = await commitToServer("ChangeStatus", {
            articleId: articleId,
            action: action
        });

        if (result && result.success) {
            updateSaveStatusIndicator("Stato aggiornato!", "success");
            // Ricarichiamo la pagina per aggiornare i bottoni e i badge Razor
            window.location.reload();
        } else {
            alert("Errore durante il cambio di stato.");
        }
    } catch (err) {
        console.error("Errore pubblicazione:", err);
    }
}


const BlockConfig = {
    'Section': {tag: 'div', baseClass: 'editor-section mb-3 loading'},
    'Header': {tag: 'span', baseClass: 'badge me-1 loading'},
    'Body': { tag: 'div', baseClass: 'section-content loading' }
}

const structureDic = {
    'Section': (el, data) => { },
    'Header': (el, data) => { el.textContent = `## ${data.content}`; },
    'Body': (el, data) => { el.textContent = data.content; }
}

let data = {
    type: '',
    content: '',
    attributes: {},
    sonsList: [],
    classAdd: '',
    doOverride: false
}

function BlockFactory(dataObject) {
    if (!dataObject) return null;

    const parentEl = createAndConfigure(dataObject);

    if (dataObject.sonsList && dataObject.sonsList.length > 0) {
        for (const son of dataObject.sonsList) {
            const childEl = BlockFactory(son);

            if (childEl) {
                parentEl.appendChild(childEl);
            }
        }
    }

    return parentEl;
}

function createAndConfigure(data) {
    if (!data || !data.type) return null;

    const config = BlockConfig[data.type];
    const el = document.createElement(config.tag);

    const baseClass = config.baseClass || '';
    el.className = data.doOverride ? data.classAdd : `${baseClass} ${data.classAdd || ''}`.trim();

    Object.entries(data.attributes || {}).forEach(([key, value]) => { el.setAttribute(key, value) });

    if (structureDic[data.type]) {
        structureDic[data.type](el, data);
    }

    return el;
}