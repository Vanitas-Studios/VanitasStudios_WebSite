using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using VanitasStudios_WebApp.Data;
using VanitasStudios_WebApp.Models;
using VanitasStudios_WebApp.Service;
using WebMarkupMin.AspNetCore6;
using WebMarkupMin.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlServer(connectionString));
// Nuovo blocco configurato per PostgreSQL
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.AddScoped<CloudinaryService>();

// Attiva l'ottimizzatore di codice
builder.Services.AddWebOptimizer(pipeline =>
{
    // Minifica tutti i file JavaScript e CSS rimuovendo commenti e spazi
    pipeline.MinifyJsFiles();
    pipeline.MinifyCssFiles();
});

builder.Services.AddWebMarkupMin(options =>
{
    options.AllowMinificationInDevelopmentEnvironment = true;
    options.AllowCompressionInDevelopmentEnvironment = true;
})
.AddHtmlMinification(options =>
{
    var settings = options.MinificationSettings;
    settings.RemoveHtmlComments = true;
    settings.RemoveRedundantAttributes = true;
    settings.MinifyEmbeddedCssCode = true;
    settings.MinifyEmbeddedJsCode = true;
});

// Registra l'HttpClient e dice a Identity di usare il tuo EmailService per ApplicationUser
builder.Services.AddHttpClient<IEmailSender<ApplicationUser>, EmailService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options => {
    options.SignIn.RequireConfirmedAccount = true; // Da impostare a vero, per maggiore sicurezza
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

if (!app.Environment.IsDevelopment())
{
    app.UseWebOptimizer();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseWebMarkupMin();

string folderPath = builder.Configuration["ExternalAssetsPath"] ?? "C:\\Temp\\DefaultAssets";

//app.UseStaticFiles(new StaticFileOptions
//{
//    FileProvider = new PhysicalFileProvider(folderPath),
//    RequestPath = "/media"
//});

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
