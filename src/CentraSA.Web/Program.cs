using System.Net;
using CentraSA.Application.Abstractions;
using CentraSA.Application.DailyMeetings;
using CentraSA.Application.Insights;
using CentraSA.Application.Lookups;
using CentraSA.Application.PendingTasks;
using CentraSA.Application.Smuds;
using CentraSA.Application.SupportTickets;
using CentraSA.Infrastructure;
using CentraSA.Infrastructure.Identity;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

LocalStoragePaths storagePaths = LocalStoragePaths.FromConfiguration(builder.Configuration);
storagePaths.EnsureDirectoriesExist();

builder.Services.AddSingleton(storagePaths);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(storagePaths.DataProtectionKeysDirectory)
    .SetApplicationName("CentraSA");
builder.Services.AddInfrastructure(storagePaths);
builder.Services.AddScoped<IDailyMeetingService, DailyMeetingService>();
builder.Services.AddScoped<IInsightService, InsightService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IPendingTaskService, PendingTaskService>();
builder.Services.AddScoped<ISmudService, SmudService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
bool useHttpsRedirection = builder.Configuration.GetValue("HttpsRedirection:Enabled", defaultValue: true);
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    if (useHttpsRedirection)
    {
        app.UseHsts();
    }
}

if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

if (app.Environment.IsDevelopment())
{
    app.MapGet(
        "/portfolio/capture-login",
        async (
            HttpContext httpContext,
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager) =>
        {
            string expectedToken = configuration["PortfolioCapture:Token"] ?? string.Empty;
            string suppliedToken = httpContext.Request.Query["token"].ToString();
            string returnPath = httpContext.Request.Query["path"].ToString();
            string[] allowedPaths =
            [
                "/",
                "/pendencias",
                "/smuds",
                "/smuds/apresentacao",
                "/chamados",
                "/reunioes/nova",
                "/historico",
            ];
            bool isLoopback = httpContext.Connection.RemoteIpAddress is { } remoteAddress
                && IPAddress.IsLoopback(remoteAddress);

            if (string.IsNullOrWhiteSpace(expectedToken)
                || !string.Equals(expectedToken, suppliedToken, StringComparison.Ordinal)
                || !isLoopback
                || !allowedPaths.Contains(returnPath, StringComparer.Ordinal))
            {
                return Results.NotFound();
            }

            ApplicationUser? administrator = await userManager.FindByNameAsync("portfolio-demo");
            if (administrator is null)
            {
                return Results.NotFound();
            }

            await signInManager.SignInAsync(administrator, isPersistent: false);
            return Results.LocalRedirect(returnPath);
        })
        .AllowAnonymous();
}

using (IServiceScope scope = app.Services.CreateScope())
{
    IDatabaseInitializer initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    bool seedDemoData = app.Environment.IsDevelopment()
        && builder.Configuration.GetValue("SeedDemoData", defaultValue: false);
    await initializer.InitializeAsync(seedDemoData);
}

if (args.Contains("--reset-admin", StringComparer.OrdinalIgnoreCase))
{
    Environment.ExitCode = await AccountMaintenance.ResetAdministratorPasswordAsync(app.Services);
    return;
}

app.Run();

public partial class Program;
