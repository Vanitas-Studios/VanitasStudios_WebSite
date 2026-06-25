namespace VanitasStudios_WebApp.Models
{
    public class StaffManagementViewModel
    {
        public List<UserRowDto> StaffMembers { get; set; } = new();

        // Elenco degli ultimi log delle azioni eseguite
        public List<AdminLogRowDto> RecentLogs { get; set; } = new();
    }

    public class UserRowDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string Role { get; set; } = "Utente Base"; // Recuperato da ASP.NET Core Identity Roles
        public int ArticlesWritten { get; set; }
    }

    public class AdminLogRowDto
    {
        public int Id { get; set; }
        public string OperatorUsername { get; set; } = null!;
        public string ActionType { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime ExecutedAt { get; set; }
    }
}
