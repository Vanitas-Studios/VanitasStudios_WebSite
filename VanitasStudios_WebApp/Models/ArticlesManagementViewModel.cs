namespace VanitasStudios_WebApp.Models
{
    public class ArticlesManagementViewModel
    {
        public List<ArticleRowDto> Articles { get; set; } = new();
    }

    public class ArticleRowDto 
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string AuthorName { get; set; }
        public string Category { get; set; }
        public PublishState PublishState { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? EliminatedAt { get; set; }
        public bool IsPinned { get; set; }
    }
}
