namespace VanitasStudios_WebApp.Models
{
    public class ArticlesManagementViewModel
    {
        public List<ArticleRowDto> Articles { get; set; } = new();
    }

    public class ArticleRowDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public PublishState PublishState { get; set; } // La tua Enum (Bozza / Pubblico)
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string AuthorName { get; set; } = "Vanitas Staff"; // Gestibile in futuro con Identity/Users
        public string Category { get; set; } = "Nessuna";
    }
}
