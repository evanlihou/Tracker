using System.Security.Claims;
using Bot;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Tracker;
using Tracker.Data;
using Tracker.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddQuartz(q =>
{
    q.SchedulerId = "reminder-scheduler";
    q.UseMicrosoftDependencyInjectionJobFactory();

    q.ScheduleJob<SendRemindersJob>(trigger => trigger
        .WithIdentity("Send Reminders Job")
        .WithDescription("Get reminders that are past due for sending and send them out")
        .StartNow().WithDailyTimeIntervalSchedule(s => s.WithIntervalInSeconds(30))
    );

    q.ScheduleJob<ProcessTelegramUpdatesJob>(trigger => trigger
        .WithIdentity("Process Telegram Updates")
        .WithDescription("Process new replies")
        .StartNow().WithDailyTimeIntervalSchedule(s => s.WithIntervalInSeconds(5)));
});

builder.Services.AddQuartzHostedService();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddSingleton(x => new TelegramSettings
    { AccessToken = builder.Configuration["Telegram:AccessToken"] });
builder.Services.AddScoped<TelegramBotService>();
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
