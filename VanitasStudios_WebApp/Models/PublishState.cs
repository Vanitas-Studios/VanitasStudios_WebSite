namespace VanitasStudios_WebApp.Models
{
    public enum PublishState
    {
        Bozza = 0,      // L'articolo è in lavorazione, visibile solo nel pannello Admin/Editor
        Pubblico = 1,   // L'articolo è online e visibile a tutti i lettori sul sito
        Eliminato = 2   // Soft-delete: l'articolo è nel "cestino" per il countdown di 30 giorni
    }
}
