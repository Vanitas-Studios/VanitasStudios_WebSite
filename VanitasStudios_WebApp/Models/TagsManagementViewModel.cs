namespace VanitasStudios_WebApp.Models
{
    public class TagsManagementViewModel
    {
        // Lista completa per la tabella di controllo
        public List<TagManagementRowDto> TagsList { get; set; } = new();

        // Lista di supporto per popolare la Select (tendina) dei sinonimi nel form
        public List<AvailableTagDto> AvailableTags { get; set; } = new();
    }

    public class TagManagementRowDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? CategoryGroup { get; set; }
        public List<string> Synonyms { get; set; } = new();
    }

    public class AvailableTagDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
