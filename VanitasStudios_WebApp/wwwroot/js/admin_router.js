let menuContainer = null;
let contentContainer = null;
let menuLinks = [];
let defaultHandler = "GeneralDashboard";

// Estrattore dinamico del Token
const getAntiforgeryToken = () => {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
};

// 🚀 Funzione Inizializzatrice (Sostituisce il vecchio .init)
function VanitasAdminRouter(config) {
    menuContainer = document.getElementById(config.menuContainerId);
    contentContainer = document.getElementById(config.contentContainerId);

    if (menuContainer) {
        menuLinks = menuContainer.querySelectorAll(".nav-link");
    }

    if (config.defaultHandler) {
        defaultHandler = config.defaultHandler;
    }

    setupEventListeners();
    loadAdminView(defaultHandler);
}

// 🔄 AGGANCIO DINAMICO: Permette di fare VanitasAdminRouter.refreshCurrentView(...)
VanitasAdminRouter.refreshCurrentView = function (handlerName) {
    loadAdminView(handlerName);
};

function setupEventListeners() {
    menuLinks.forEach(link => {
        link.addEventListener("click", function (e) {
            e.preventDefault();

            // Feedback visivo dei menu (Attivo / Inattivo)
            menuLinks.forEach(l => {
                l.classList.remove("active", "text-white");
                l.classList.add("text-white-50");
            });
            this.classList.add("active", "text-white");
            this.classList.remove("text-white-50");

            // Eseguiamo lo switch della vista recuperando il nome dell'handler C#
            const handler = this.getAttribute("data-target-handler");
            if (handler) {
                loadAdminView(handler);
            }
        });
    });
}

async function loadAdminView(handlerName) {
    if (!contentContainer) return;

    contentContainer.innerHTML = `<div class="text-center py-5"><div class="spinner-border text-secondary"></div></div>`;

    // Richiediamo l'HTML usando il metodo unificato
    const html = await commitToServer(handlerName, null, 'GET');

    // Controllo corretto sulla natura del dato (stringa HTML)
    if (html && typeof html === "string") {
        contentContainer.innerHTML = html;
    } else {
        contentContainer.innerHTML = `<div class="alert alert-danger">Errore nel caricamento della sezione.</div>`;
    }
}

// 🔄 Funzione di comunicazione flessibile (Supporta sia GET che POST, sia HTML che JSON)
async function commitToServer(handlerName, payload = null, method = 'GET') {
    try {
        let url = `?handler=${handlerName}`;
        const options = { method: method, headers: {} };

        if (method === 'GET' && payload) {
            // Se è una GET, appendiamo il payload all'URL
            url += payload;
        } else if (method === 'POST') {
            // Se è una POST, gestiamo il body (se presente)
            if (payload) {
                const isFormData = payload instanceof FormData;
                options.body = isFormData ? payload : JSON.stringify(payload);
                if (!isFormData) {
                    options.headers['Content-Type'] = 'application/json';
                }
            }

            // Inseriamo il Token di sicurezza recuperato dinamicamente
            const token = getAntiforgeryToken();
            if (token) {
                options.headers['RequestVerificationToken'] = token;
            }
        }

        const response = await fetch(url, options);
        if (!response.ok) throw new Error('Network response was not ok');

        // 💡 CONTROLLO DINAMICO SUL TIPO DI RISPOSTA
        const contentType = response.headers.get("content-type");
        if (contentType && contentType.includes("application/json")) {
            return await response.json(); // Restituisce l'oggetto JSON per i POST
        } else {
            return await response.text(); // Restituisce la stringa HTML per i GET
        }
    }
    catch (error) {
        console.error('Error communicating with server:', error);
        return null;
    }
}

async function deleteArticle(articleId, articleTitle) {
    if (!confirm(`Vuoi spostare nel cestino "${articleTitle}"?`)) return;

    // 💡 Passiamo l'ID come oggetto strutturato nel payload (il secondo parametro)
    const result = await commitToServer('DeleteArticle', { id: articleId }, 'POST');

    if (result && result.success) {
        alert(result.message);
        VanitasAdminRouter.refreshCurrentView('ArticlesList');
    } else {
        alert(result ? result.message : "Errore di connessione.");
    }
}

async function restoreArticle(articleId, articleTitle) {
    if (!confirm(`Vuoi ripristinare e aggiornare la data di "${articleTitle}"?`)) return;

    // Inviamo l'ID come oggetto JSON nel Body (stesso identico approccio dell'eliminazione)
    const result = await commitToServer('RestoreArticle', { id: articleId }, 'POST');

    if (result && result.success) {
        alert(result.message);
        // Rinfreschiamo la parziale degli articoli per vedere il cambio di stato istantaneo
        VanitasAdminRouter.refreshCurrentView('ArticlesList');
    } else {
        alert(result ? result.message : "Errore di connessione durante il ripristino.");
    }
}

// Funzione per intercettare l'invio asincrono del modulo Tag
async function submitTagForm(event, handlerName) {
    event.preventDefault(); // Sganciamo il comportamento nativo del browser

    const form = event.target;
    const formData = new FormData(form);

    // Convertiamo i dati del form in un oggetto JSON piatto
    const payload = Object.fromEntries(formData.entries());

    // Se l'ID del tag selezionato è una stringa numerica, la convertiamo in Int per C#
    if (payload.TargetTagId) {
        payload.TargetTagId = parseInt(payload.TargetTagId);
    }

    const result = await commitToServer(handlerName, payload, 'POST');

    if (result && result.success) {
        alert(result.message);
        form.reset(); // Svuota i campi del form in caso di successo
        VanitasAdminRouter.refreshCurrentView('TagsManagement'); // Rinfresca la parziale corrente
    } else {
        alert(result ? result.message : "Errore di connessione durante l'operazione.");
    }
}

// Funzione per eliminare un Tag
async function deleteTag(tagId) {
    if (!confirm("Sei sicuro di voler eliminare questo tag? I sinonimi e le associazioni correlate potrebbero rompersi.")) return;

    const result = await commitToServer('DeleteTag', { id: tagId }, 'POST');

    if (result && result.success) {
        alert(result.message);
        VanitasAdminRouter.refreshCurrentView('TagsManagement');
    } else {
        alert(result ? result.message : "Errore durante l'eliminazione del tag.");
    }
}
// 1. Questa si attiva quando clicchi l'icona del lucchetto sulla tabella
function openPromotionModal(userId, username) {
    document.getElementById('modalUserId').value = userId;
    document.getElementById('modalUsername').value = `@${username}`;

    // Accende il modal di Bootstrap
    const modalElement = document.getElementById('promotionModal');
    const modalInstance = bootstrap.Modal.getOrCreateInstance(modalElement);
    modalInstance.show();
}

// 2. Questa si attiva quando premi "Assegna Strato" dentro il modal
async function submitRoleForm(event) {
    event.preventDefault(); // Blocca il refresh nativo

    const form = event.target;
    const formData = new FormData(form);
    const payload = Object.fromEntries(formData.entries());

    payload.UserId = parseInt(payload.UserId); // Converte l'ID per il C#

    // Spedisce la POST asincrona all'handler OnPostUpdateRole
    const result = await commitToServer('UpdateRole', payload, 'POST');

    if (result && result.success) {
        alert(result.message);

        // Chiude il modal automaticamente
        const modalElement = document.getElementById('promotionModal');
        const modalInstance = bootstrap.Modal.getInstance(modalElement);
        if (modalInstance) modalInstance.hide();

        // Rinfresca la vista usando il tuo router statico
        VanitasAdminRouter.refreshCurrentView('StaffManagement');
    } else {
        alert(result ? result.message : "Errore durante la modifica del ruolo.");
    }
}
// Attiva il modal di risoluzione pre-compilando il termine fantasma rilevato
function quickCreateTag(ghostTerm) {
    document.getElementById('modalGhostTerm').value = ghostTerm;
    document.getElementById('modalGhostDisplay').value = `"${ghostTerm}"`;
    // Suggerisce lo stesso termine come nome tag di partenza pulendolo da apici
    document.getElementById('modalTargetTagName').value = ghostTerm;

    const modalElement = document.getElementById('ghostModal');
    const modalInstance = bootstrap.Modal.getOrCreateInstance(modalElement);
    modalInstance.show();
}

// Invia i dati strutturati al server via POST JSON
async function submitGhostResolution(event) {
    event.preventDefault();

    const form = event.target;
    const formData = new FormData(form);
    const payload = Object.fromEntries(formData.entries());

    // Spediamo all'handler OnPostResolveGhostTerm
    const result = await commitToServer('ResolveGhostTerm', payload, 'POST');

    if (result && result.success) {
        alert(result.message);

        // Chiudiamo il modal
        const modalElement = document.getElementById('ghostModal');
        const modalInstance = bootstrap.Modal.getInstance(modalElement);
        if (modalInstance) modalInstance.hide();

        // Rinfreschiamo l'intera vista Analytics per aggiornare metriche e lista fantasmi!
        VanitasAdminRouter.refreshCurrentView('AkinatorAnalytics');
    } else {
        alert(result ? result.message : "Errore durante la risoluzione del termine.");
    }
}