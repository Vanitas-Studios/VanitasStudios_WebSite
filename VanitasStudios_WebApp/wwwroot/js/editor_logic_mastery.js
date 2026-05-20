let editor, sidebarList, articleId, articleLastModifiedFromServer;
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


// Funzione per ottenere il token antiforgery
const getAntiforgeryToken = () => {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
};

function initMyEditor(config) {
    editor = document.getElementById(config.editorId);
    sidebarList = document.getElementById(config.sidebarId);
    articleId = config.articleId;
    articleLastModifiedFromServer = config.lastModified;

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

    editor.addEventListener("input", () => {
        checkDeletedSections();    // Aggiorna sidebar ed eliminazioni istantaneamente
        triggerAutosave(); // Prepara il salvataggio globale tra 5 secondi
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
async function syncSectionToServer(wrapper) {

    if (isSavingFull || isSavingSection) {
        console.warn("Posticipated section save: process loading")
        return;
    }

    isSavingSection = true;
    wrapper.classList.add("section-loading");

    // Definiamo il nome Handler per la creazione
    const handlerName = "SaveSection";

    // 1. Chiamiamo il metodo che determina l'ordine
    const order = calculateOrder(wrapper);

    try {

        const titleText = wrapper.querySelector(".section-title-badge")?.textContent.replace("##", "").trim() ?? "Sezione senza titolo";

        const textContent = wrapper.querySelector(".editor-content").innerHTML
            .replace(/\u200B/g, '')
            .trim();

        // Se è un invio rapido e il contenuto è solo un <br> vuoto, puliscilo
        const finalContent = textContent === "<br>" ? "" : textContent;

        // 2. Prepariamo i dati
        const payload = {
            ArticleId: articleId,
            Id: wrapper.getAttribute("data-section-id"),
            Title: titleText,
            Content: finalContent,
            Order: order,
        };

        // 3. Mandiamo al server
        const result = await commitToServer(handlerName, payload);

        if (result.success) {

            // Controlliamo che ci sia ancora effettivamente la sezione (l'utente potrebbe averla cancellata mentre aspettavamo la risposta)
            if (editor.contains(wrapper)) {
                wrapper.setAttribute("data-section-id", result.sectionId);
                wrapper.classList.remove("section-loading");

                // 4. Sincronizziamo gli ID esistenti, ora che abbiamo un nuovo ID reale
                syncExistingIds();

                console.log(`Sezione salvata! ID: ${result.sectionId}, Ordine: ${order}`);

                // 5. Aggiorniamo la sidebar laterale
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
        console.error("Errore nel salvataggio della sezione.");
        wrapper.querySelector(".badge").classList.replace("bg-primary", "bg-danger");
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

    // Definiamo handlerName per la cancellazione
    const handlerName = "DeleteSection";

    // Prendiamo tutte le sezioni
    const allWrappers = Array.from(editor.querySelectorAll(".editor-section"));
    // Prendiamo i loro ID, escludendo quelli temporanei
    const allIds = allWrappers.map(wrapper => wrapper.getAttribute("data-section-id")).filter(id => id !== null && !id.startsWith("temp-"));

    // Confrontiamo con gli ID esistenti (quelli che abbiamo sincronizzato)
    const deletedIds = existingIds.filter(id => !allIds.includes(id));

    if (deletedIds.length > 0) {
        for (const id of deletedIds) {
            // Rimuoviamo l'ID dalla memoria locale PRIMA della chiamata
            // per evitare che altri eventi 'input' chiamino di nuovo la cancellazione
            existingIds = existingIds.filter(oldId => oldId !== id);

            const payload = { SectionId: id, ArticleId: articleId };

            console.log(`Rilevata eliminazione ID: ${id}`);
            await commitToServer(handlerName, payload);
        }

        // Dopo la cancellazione , aggiorniamo la sidebar per riflettere i cambiamenti
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

async function handleFileDrop(e) {

    // Prendiamo i file dal drop
    const fileList = [...e.dataTransfer.files];
    if (fileList.length === 0) return; // Se non ci sono file, usciamo

    // 1. Identifichiamo il punto esatto del rilascio
    let range;

    // A. Prova lo standard ufficiale (Firefox e futuri)
    if (document.caretPositionFromPoint) {
        const pos = document.caretPositionFromPoint(e.clientX, e.clientY);
        if (pos) {
            range = document.createRange();
            range.setStart(pos.offsetNode, pos.offset);
            range.collapse(true);
        }
    }
    // B. Prova l'API Webkit (Chrome, Safari, Edge)
    else if (document.caretRangeFromPoint) {
        range = document.caretRangeFromPoint(e.clientX, e.clientY);
    }

    // C. Fallback di emergenza
    if (!range) {
        // Se proprio non riusciamo a trovare il punto, 
        // creiamo un range che punta alla fine dell'editor
        range = document.createRange();
        range.selectNodeContents(editor);
        range.collapse(false);
    }

    // 2. Usiamo for...of per gestire correttamente gli await
    for (const file of fileList) {
        const fileType = file.type.split('/')[0];
        if (fileType !== "image" && fileType !== "video") continue;

        // 3. Creiamo un wrapper per il media (come fatto per le sezioni)
        const mediaWrapper = createMediaBlock(fileType, file.name, `temp-${Date.now()}`);

        // Inseriamo il wrapper nella posizione del mouse
        if (range) {
            //Risoluzione bug? Ho aggiunto un br per evitare che il puntatore rimanga incastrato all'interno del blocco media
            const br = document.createElement("br");
            range.insertNode(br);
            range.insertNode(mediaWrapper);
            // Sposta il punto di inserimento dopo il blocco appena creato
            range.setStartAfter(br);
            range.collapse(true); // Da capire la differenza reale true/false 
        } else {
            editor.appendChild(mediaWrapper);
        }

        // 4. Preparazione Upload
        const formData = new FormData();
        formData.append("ArticleId", articleId);
        formData.append("file", file);

        try {
            const result = await commitToServer("UploadMedia", formData);

            if (result.success) {

                const labelText = `[img alt="${result.Alt || 'immagine'}" url="${result.Url}"]`;
                createMediaBlock(fileType, labelText, result.ID, mediaWrapper, result.url); // Ricreiamo il blocco media con l'URL restituito dal server)

            } else {
                createMediaBlock(fileType, file.name, `temp-${Date.now()}`, mediaWrapper, null, true); // Passiamo error=true per mostrare l'errore
            }
        } catch (err) {
            console.error("Errore critico upload:", err);
        }
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

// Funzione per il salvataggio prima su Client e poi Server
async function saveFullContent() {

    if (isSavingFull || isSavingSection) {
        console.warn("Posticipated Save: process loading");
        return;
    }

    isSavingFull = true;
    updateSaveStatusIndicator("Salvataggio in corso...");

    // Serializziamo contenuto editor
    const articleData = serializedEditorContent();

    //Salvataggio prima nella sessione del browser
    localStorage.setItem(`article_backup_${articleId}`, JSON.stringify(articleData));

    //Chiamata per il salvataggio sul server
    try {
        const response = await commitToServer("SaveContent", articleData)

        if (response.success) {
            isDirty = false;
            localStorage.removeItem(`article_backup_${articleId}`); // Pulizia del localStorage
            updateSaveStatusIndicator("Tutte le modifiche salvate");
            // Puliamo il backup locale se vogliamo essere pignoli, 
            // o lo teniamo finché non chiude la pagina.
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

// Funzione per serializzare il contenuto dell'editor
function serializedEditorContent() {
    const sections = Array.from(editor.querySelectorAll(".editor-section")).map(wrapper => {
        return {
            id: wrapper.getAttribute("data-section-id"),
            title: wrapper.querySelector(".section-title-badge")?.textContent || "",
            // Verificare se salvare solo testo oppure tutto contenuto HTML 
            //P.S. Meglio salvare direttamente HTML, evitiamo di ricreare tutta la struttura
            content: wrapper.querySelector(".section-content").innerHTML.replace(/\u200B/g, '').replace(/<div><br><\/div>/g, '<br>'), // .replace(/\u200B/g, '') --> puliamo eventuali punti critici per la formattazione del documento
            order: calculateOrder(wrapper)
        };
    });

    return {
        articleId: articleId,
        sections: sections
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

    // 3. CONTROLLO: Il cursore è dentro una .section-content?
    let contentArea = range.startContainer.parentElement.closest(".section-content");

    // 4. SCENARIO: Editor vuoto o incolla fuori dalle sezioni
    if (!contentArea) {
        // Se l'editor è vuoto o siamo fuori, creiamo una sezione di emergenza
        const newSection = createSectionBlock(`temp-${Date.now()}`, "Nuova Sezione");
        editor.appendChild(newSection);

        // Puntiamo il range dentro la nuova area di contenuto
        contentArea = newSection.querySelector(".section-content");
        range = document.createRange();
        range.selectNodeContents(contentArea);
        range.collapse(false); // Va alla fine
        selection.removeAllRanges();
        selection.addRange(range);
    }

    // 5. ESECUZIONE: Inserimento del testo pulito
    const textNode = document.createTextNode(text);
    range.deleteContents();
    range.insertNode(textNode);

    // 6. Spostiamo il cursore dopo il testo incollato
    updateCursorPos(textNode);

    // 7. Sincronizzazione
    triggerAutosave();
}
