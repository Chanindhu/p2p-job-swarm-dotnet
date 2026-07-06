using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Swarm.Server.Data;
using Swarm.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// ------- LOGGING -------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DB: EF Core + SQLite (with fallback)
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection")
             ?? "Data Source=swarm.db";
    opt.UseSqlite(cs);
});

// CORS: allow the dashboard (different origin) to call the API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Background cleanup (tolerant if tables aren’t created yet)
builder.Services.AddHostedService<CleanupService>();

var app = builder.Build();

// ----- DB INIT (migrations if present; otherwise EnsureCreated + validate) -----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    var resetOnStart = builder.Configuration.GetValue<bool>("ResetDbOnStart", false);

    try
    {
        if (resetOnStart)
        {
            db.Database.EnsureDeleted();
            logger.LogInformation("ResetDbOnStart=true → database dropped.");
        }

        var migrations = db.Database.GetMigrations();
        var hasMigrations = migrations.Any();

        if (hasMigrations)
        {
            db.Database.Migrate();
            logger.LogInformation("Applied {MigrationCount} migrations.", migrations.Count());
        }
        else
        {
            var created = db.Database.EnsureCreated();
            // Keep message template constant; vary only the argument
            logger.LogInformation("No migrations found → {Outcome}",
                created ? "EnsureCreated() built the schema." : "Database existed; validating schema.");
        }

        // Validate required tables exist; if not, repair (drop + create)
        ValidateAndRepairSchema(db, logger, SchemaConstants.RequiredTables);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Keep HTTP in local dev (uncomment HTTPS if you’ve set up dev certs)
// app.UseHttpsRedirection();

app.UseCors("AllowDashboard");
app.MapControllers();

// Convenience: root → Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

static void ValidateAndRepairSchema(AppDbContext db, ILogger logger, string[] requiredTables)
{
    var existing = GetTables(db);
    var missing = requiredTables
        .Where(t => !existing.Contains(t, StringComparer.OrdinalIgnoreCase))
        .ToList();

    if (missing.Count == 0)
    {
        logger.LogInformation("SQLite tables ready: {Tables}", string.Join(", ", existing));
        return;
    }

    logger.LogWarning("SQLite missing tables: {Missing}. Resetting database and creating schema.",
        string.Join(", ", missing));

    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();

    existing = GetTables(db);
    var stillMissing = requiredTables
        .Where(t => !existing.Contains(t, StringComparer.OrdinalIgnoreCase))
        .ToList();

    if (stillMissing.Count > 0)
        throw new InvalidOperationException("After EnsureCreated, still missing tables: " + string.Join(", ", stillMissing));

    logger.LogInformation("SQLite tables ready after repair: {Tables}", string.Join(", ", existing));
}

static List<string> GetTables(AppDbContext db)
{
    var names = new List<string>();
    var conn = db.Database.GetDbConnection();
    var wasOpen = conn.State == System.Data.ConnectionState.Open;

    if (!wasOpen) conn.Open();
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
            names.Add(rd.GetString(0));
    }
    finally
    {
        if (!wasOpen) conn.Close();
    }
    return names;
}

// Keep constants/readonlys here to avoid per-call allocations & template variance
internal static class SchemaConstants
{
    // Prefer static readonly array over allocating `new[] { ... }` at call sites
    internal static readonly string[] RequiredTables = ["Clients", "JobCompletions"];
}
