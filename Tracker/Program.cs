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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var dbProvider = builder.Configuration["DbProvider"];
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(dbProvider) || string.IsNullOrEmpty(connectionString))
{
    throw new ArgumentNullException(nameof(connectionString), "DB connection info is required");
}

switch (dbProvider)
{
    case "MySQL":
        builder.Services.AddDbContext<ApplicationDbContext, MysqlApplicationDbContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }, ServiceLifetime.Transient);
        break;
    case "SQLite":
        builder.Services.AddDbContext<ApplicationDbContext, SqliteApplicationDbContext>(options =>
            options.UseSqlite(connectionString));
        break;
    default:
        throw new ApplicationException($"Unknown DB Provider {dbProvider}");
}

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

if (!string.IsNullOrEmpty(builder.Configuration["Telegram:AccessToken"]))
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

    if (!string.IsNullOrEmpty(builder.Configuration["Telegram:AccessToken"]) &&
        !string.IsNullOrEmpty(builder.Configuration["BaseUrl"]))
    {
        builder.Services.AddSingleton(x => new TelegramSettings
        {
            AccessToken = builder.Configuration["Telegram:AccessToken"]!,
            BaseUrl = builder.Configuration["BaseUrl"]!
        });
        builder.Services.AddSingleton(x => new TelegramBotClient(builder.Configuration["Telegram:AccessToken"]!));
        builder.Services.AddScoped<TelegramBotService>();
        builder.Services.AddHostedService<TelegramPollingService>();
        builder.Services.AddScoped<IUpdateHandler, TelegramUpdateHandler>();

    }
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

app.Run();
