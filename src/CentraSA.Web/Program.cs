using CentraSA.Application.Abstractions;
using CentraSA.Infrastructure;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

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
