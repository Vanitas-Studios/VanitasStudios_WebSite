namespace VanitasStudios_WebApp.Models
{
    public enum MediaType
    {
        Image = 0,         // Immagine classica (jpg, png, webp)
        YouTubeVideo = 1,  // Video incorporato da YouTube (tramite iframe)
        LocalVideo = 2     // Video caricato direttamente sul server (mp4)
    }
}
