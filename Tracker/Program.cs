using System.Security.Claims;
using Bot;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
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

builder.Services.AddIdentityServer(options =>
    {
        // // set path where to store keys
        // options.KeyManagement.KeyPath = "/app/keys";
        //
        // // new key every 30 days
        // options.KeyManagement.RotationInterval = TimeSpan.FromDays(30);
        //
        // // announce new key 2 days in advance in discovery
        // options.KeyManagement.PropagationTime = TimeSpan.FromDays(2);
        //
        // // keep old key for 7 days in discovery for validation of tokens
        // options.KeyManagement.RetentionDuration = TimeSpan.FromDays(7);
    })
    .AddDeveloperSigningCredential()
    .AddApiAuthorization<ApplicationUser, ApplicationDbContext>(opt =>
    {
        opt.Clients.Add(new Client
        {
            ClientId = "swagger",
            ClientName = "Swagger UI for demo_api",
            //ClientSecrets = {new Secret("CHANGEME".Sha256())}, // change me!

            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,

            RedirectUris = {"https://localhost:7113/swagger/oauth2-redirect.html"},
            AllowedCorsOrigins = {"https://localhost:7113"},
            AllowedScopes = {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                "Tracker"
            }
        });
    });

builder.Services.AddQuartz(q =>
{
    q.SchedulerId = "reminder-scheduler";
    q.UseMicrosoftDependencyInjectionJobFactory();

    q.ScheduleJob<SendRemindersJob>(trigger => trigger
        .WithIdentity("Send Reminders Job")
        .WithDescription("Get reminders that are past due for sending and send them out")
        .StartNow().WithDailyTimeIntervalSchedule(s => s.WithIntervalInMinutes(1))
    );

    q.ScheduleJob<ProcessTelegramUpdatesJob>(trigger => trigger
        .WithIdentity("Process Telegram Updates")
        .WithDescription("Process new replies")
        .StartNow().WithDailyTimeIntervalSchedule(s => s.WithIntervalInSeconds(5)));
});

builder.Services.AddQuartzHostedService();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.OperationFilter<AuthorizeCheckOperationFilter>();
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "V1",
        Version = "v1",
    });
    opt.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri("https://localhost:7113/connect/authorize"),
                TokenUrl = new Uri("https://localhost:7113/connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    {IdentityServerConstants.StandardScopes.OpenId, "OpenId"},
                    {IdentityServerConstants.StandardScopes.Profile, "Profile"},
                    {"Tracker", "Tracker API"}
                }
            }
        }
    });
});

builder.Services.AddSingleton(x => new TelegramSettings
    { AccessToken = builder.Configuration["Telegram:AccessToken"] });
builder.Services.AddScoped<TelegramBotService>();
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

app.Services.GetService<ApplicationDbContext>()?.Database.Migrate();

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseIdentityServer();
app.UseAuthorization();

app.UseSwagger();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");
app.MapRazorPages();

app.UseSwaggerUI(opt =>
{
    opt.OAuthClientId("swagger");
    opt.OAuthAppName("Swagger");
    opt.OAuthUsePkce();
});

app.MapFallbackToFile("index.html");

app.Run();