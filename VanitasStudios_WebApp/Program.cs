using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;
using VanitasStudios_WebApp.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options => {
    options.SignIn.RequireConfirmedAccount = false; // Da impostare a vero, per maggiore sicurezza
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultUI()
.AddDefaultTokenProviders();
builder.Services.AddRazorPages();
// Registriamo il servizio di ricerca (Scoped significa che vive per la durata della richiesta HTTP)
builder.Services.AddScoped<IAkinatorSearchService, AkinatorSearchService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

string folderPath = builder.Configuration["ExternalAssetsPath"] ?? "C:\\Temp\\DefaultAssets";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(folderPath),
    RequestPath = "/media"
});

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // TEST 1: Il DbContext si inizializza?
        var context = services.GetRequiredService<ApplicationDbContext>();
        Console.WriteLine("Connessione al DB OK.");

        // TEST 2: Il RoleManager viene creato?
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<int>>>();

        string[] roleNames = { "Admin", "Editor", "User" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(roleName));
            }
        }
        Console.WriteLine("Seeding completato con successo.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("--------- ERRORE IDENTITY ---------");
        Console.WriteLine($"Messaggio: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Errore Interno (Il vero motivo): {ex.InnerException.Message}");
        }
        Console.WriteLine("-----------------------------------");
        // Non bloccare l'app se il seeding fallisce, o gestiscilo come preferisci
    }
}

app.Run();
