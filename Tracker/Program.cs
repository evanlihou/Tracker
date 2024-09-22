using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Tracker;
using Tracker.Data;
using Tracker.Models;
using Tracker.Services;

var startupWarnings = new List<string>();

var builder = WebApplication.CreateBuilder(args);

var startupOptions = builder.Configuration.Get<TrackerOptions>();
if (startupOptions is null) throw new ApplicationException("Failed to bind configuration");
builder.Services.AddOptions<TrackerOptions>().BindConfiguration("");

// Add services to the container.
var dbProvider = startupOptions.DbProvider;
var connectionString = startupOptions.ConnectionStrings[ConnectionStrings.DefaultConnection];

if (string.IsNullOrEmpty(connectionString))
{
    throw new ValidationException("DB connection info is required");
}

switch (dbProvider)
{
    case DbProvider.MySQL:
        builder.Services.AddDbContext<ApplicationDbContext, MysqlApplicationDbContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        });
        break;
    case DbProvider.SQLite:
        builder.Services.AddDbContext<ApplicationDbContext, SqliteApplicationDbContext>(options =>
            options.UseSqlite(connectionString));
        break;
    default:
        throw new ApplicationException($"Unknown DB Provider {dbProvider}");
}

builder.Services.AddMemoryCache();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromHours(24);
});
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddPasswordlessLoginTokenProvider();

builder.Services.Configure<PasswordlessLoginTokenProviderOptions>(opt => opt.TokenLifespan = TimeSpan.FromMinutes(15));

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

if (!string.IsNullOrEmpty(startupOptions.Telegram.AccessToken))
{
    builder.Services.AddQuartzHostedService();

    builder.Services.AddQuartz(q =>
    {
        q.SchedulerId = "reminder-scheduler";

        q.ScheduleJob<SendRemindersJob>(trigger => trigger
            .WithIdentity("Send Reminders Job")
            .WithDescription("Get reminders that are past due for sending and send them out")
            .StartAt(DateBuilder.EvenMinuteDateBefore(null))
            .WithSimpleSchedule(x => x.WithIntervalInSeconds(30).RepeatForever())
        );
    });

    if (!string.IsNullOrEmpty(startupOptions.Telegram.AccessToken) &&
        !string.IsNullOrEmpty(startupOptions.Telegram.BaseUrl))
    {
        builder.Services.AddSingleton(x => new TelegramSettings
        {
            AccessToken = startupOptions.Telegram.AccessToken,
            BaseUrl = startupOptions.Telegram.BaseUrl
        });
        builder.Services.AddSingleton(_ => new TelegramBotClient(startupOptions.Telegram.AccessToken));
        builder.Services.AddScoped<TelegramBotService>();
        builder.Services.AddHostedService<TelegramPollingService>();
        builder.Services.AddScoped<IUpdateHandler, TelegramUpdateHandler>();
    }
}
else
{
    startupWarnings.Add(
        "Running without Telegram credentials. Reminders job, bot, and logging in disabled. Things may act very weird");
}

builder.Services.AddScoped<ReminderService>();
builder.Services.AddScoped<PersistentConfigRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Ensure we're booting up with the latest migrations
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetService<ApplicationDbContext>()?.Database.Migrate();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

if (startupWarnings.Count != 0)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    foreach (var warning in startupWarnings)
    {
        logger.LogWarning("Startup warning: {Warning}", warning);
    }
}

app.Run();
