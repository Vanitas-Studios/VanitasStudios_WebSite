// Definiamo il modulo router in un raggio d'azione isolato
const VanitasAdminRouter = (function () {
    // 1. STATO INTERNO (Variabili private del modulo)
    let menuContainer = null;
    let contentContainer = null;
    let menuLinks = [];
    let defaultHandler = "GeneralDashboard";

    // 2. LOGICA FUNZIONALE (Metodi interni)
    async function loadAdminView(handlerName) {
        if (!contentContainer) return;

        // Visualizziamo il loader asincrono nella spalla destra
        contentContainer.innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border text-secondary" role="status"></div>
            </div>`;

        try {
            const response = await fetch(`?handler=${handlerName}`);
            if (!response.ok) throw new Error("Impossibile caricare la vista.");

            const html = await response.text();
            contentContainer.innerHTML = html;
        } catch (err) {
            console.error("Errore router admin:", err);
            contentContainer.innerHTML = `
                <div class="alert alert-danger" role="alert">
                    Errore nel caricamento della sezione. Riprova.
                </div>`;
        }
    }

    // 3. ASSOCIAZIONE EVENTI (Metodo dedicato ai listener)
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

    // 4. L'UNICO METODO ESPORTO (L'inizializzatore)
    return {
        init: function (config) {
            // Mappiamo gli elementi del DOM passati dalla configurazione
            menuContainer = document.getElementById(config.menuContainerId);
            contentContainer = document.getElementById(config.contentContainerId);

            if (menuContainer) {
                menuLinks = menuContainer.querySelectorAll(".nav-link");
            }

            if (config.defaultHandler) {
                defaultHandler = config.defaultHandler;
            }

            // Attiviamo i listener
            setupEventListeners();

            // Carichiamo la prima vista di default all'avvio (es. la Dashboard Generale)
            loadAdminView(defaultHandler);
        }
    };
})();