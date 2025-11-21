using System.Text.Json;
using System.Text.Json.Serialization;
using CMCS.Data;
using CMCS.Models;
using CMCS.Services;
using CMCS.Validators;
using CMCS.Hubs;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Entity Framework with SQL Server and EnableRetryOnFailure
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null
        )
    ));

// Add Identity services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Simple configuration for development
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 3;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<ClaimValidator>();

// Register services
builder.Services.AddScoped<IDataService, EfDataService>();

// Add SignalR
builder.Services.AddSignalR();

// Add session for user management
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "CMCS.Session";
});

// Configure Application Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});

// Configure Cookie settings
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false; // Simplified for development
    options.MinimumSameSitePolicy = SameSiteMode.None;
});

// Add authorization with policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("LecturerOnly", policy => policy.RequireRole("Lecturer"));
    options.AddPolicy("CoordinatorOnly", policy => policy.RequireRole("Coordinator"));
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager"));
    options.AddPolicy("HROnly", policy => policy.RequireRole("HR"));
    options.AddPolicy("ApproversOnly", policy => policy.RequireRole("Coordinator", "Manager"));
});

// Add API controllers with JSON options to handle circular references
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.MaxDepth = 32;
});

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Database seeding with comprehensive error handling
try
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;

        // Get required services
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting database initialization...");

        // Ensure database is created
        try
        {
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("Database ensured created successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred ensuring the database is created.");
            // Continue - the application might still work
        }

        // Seed the database
        try
        {
            await DatabaseSeeder.Initialize(context, userManager, roleManager);
            logger.LogInformation("Database seeding completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            // Don't stop the application if seeding fails
        }
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred during application startup database initialization.");
    // Continue running the application even if initialization fails
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // Development error handling
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANT: UseAuthentication must come before UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

// Add session middleware
app.UseSession();

// Add cookie policy
app.UseCookiePolicy();

// Configure endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

// Add SignalR hub mapping
app.MapHub<ClaimHub>("/claimHub");

// Custom middleware for session initialization (only for demo purposes)
app.Use(async (context, next) =>
{
    // Initialize session with demo data only if no user is logged in
    if (context.User.Identity?.IsAuthenticated != true)
    {
        // Set default demo user session for development
        if (string.IsNullOrEmpty(context.Session.GetString("UserId")))
        {
            context.Session.SetString("UserRole", "Lecturer");
            context.Session.SetString("UserName", "Demo User");
            context.Session.SetString("UserId", "1");
            context.Session.SetString("UserEmail", "demo@university.ac.za");
        }
    }
    else
    {
        // If user is authenticated, sync session with Identity
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);

        if (user != null)
        {
            context.Session.SetString("UserId", user.Id);
            context.Session.SetString("UserRole", user.Role ?? "Lecturer");
            context.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
            context.Session.SetString("UserEmail", user.Email ?? "");
        }
    }

    await next();
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

// Application startup message
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var startupLogger = loggerFactory.CreateLogger<Program>();
startupLogger.LogInformation("CMCS Application started successfully at {Time}", DateTime.Now);
startupLogger.LogInformation("Application Name: Contract Monthly Claim System");
startupLogger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
startupLogger.LogInformation("Database Connection: {ConnectionString}",
    builder.Configuration.GetConnectionString("DefaultConnection")?.Contains("Server") == true ? "Configured" : "Not Configured");

app.Run();

// Make the Program class accessible for testing
public partial class Program { }