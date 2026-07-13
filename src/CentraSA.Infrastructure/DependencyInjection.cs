using CentraSA.Application.Abstractions;
using CentraSA.Application.DailyMeetings;
using CentraSA.Application.Insights;
using CentraSA.Application.Lookups;
using CentraSA.Application.PendingTasks;
using CentraSA.Application.Smuds;
using CentraSA.Application.SupportTickets;
using CentraSA.Infrastructure.Identity;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Infrastructure.Repositories;
using CentraSA.Infrastructure.Seeding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CentraSA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        LocalStoragePaths storagePaths)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = storagePaths.DatabaseFile.FullName,
            ForeignKeys = true,
            Pooling = true,
        }.ToString();

        services.AddDbContext<CentraSaDbContext>(options =>
            options.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsAssembly(typeof(CentraSaDbContext).Assembly.FullName)));

        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IDailyMeetingRepository, DailyMeetingRepository>();
        services.AddScoped<IInsightRepository, InsightRepository>();
        services.AddScoped<ILookupRepository, LookupRepository>();
        services.AddScoped<IPendingTaskRepository, PendingTaskRepository>();
        services.AddScoped<ISmudRepository, SmudRepository>();
        services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = false;
                options.SignIn.RequireConfirmedAccount = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<CentraSaDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/conta/entrar";
            options.AccessDeniedPath = "/conta/acesso-negado";
            options.Cookie.Name = "CentraSA.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        return services;
    }
}
