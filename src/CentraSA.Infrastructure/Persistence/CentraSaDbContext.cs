using CentraSA.Domain.Common;
using CentraSA.Domain.Entities;
using CentraSA.Infrastructure.Identity;
using CentraSA.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Persistence;

public sealed class CentraSaDbContext(DbContextOptions<CentraSaDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<TeamArea> TeamAreas => Set<TeamArea>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<StatusDefinition> StatusDefinitions => Set<StatusDefinition>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<PendingTask> PendingTasks => Set<PendingTask>();

    public DbSet<Smud> Smuds => Set<Smud>();

    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();

    public DbSet<WorkItemReference> WorkItemReferences => Set<WorkItemReference>();

    public DbSet<DailyMeeting> DailyMeetings => Set<DailyMeeting>();

    public DbSet<DailyMeetingItem> DailyMeetingItems => Set<DailyMeetingItem>();

    public DbSet<ActivityHistory> ActivityHistories => Set<ActivityHistory>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        IncrementConcurrencyVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        IncrementConcurrencyVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new TeamAreaConfiguration());
        builder.ApplyConfiguration(new PersonConfiguration());
        builder.ApplyConfiguration(new StatusDefinitionConfiguration());
        builder.ApplyConfiguration(new CategoryConfiguration());
        builder.ApplyConfiguration(new PendingTaskConfiguration());
        builder.ApplyConfiguration(new SmudConfiguration());
        builder.ApplyConfiguration(new SupportTicketConfiguration());
        builder.ApplyConfiguration(new WorkItemReferenceConfiguration());
        builder.ApplyConfiguration(new DailyMeetingConfiguration());
        builder.ApplyConfiguration(new DailyMeetingItemConfiguration());
        builder.ApplyConfiguration(new ActivityHistoryConfiguration());
    }

    private void IncrementConcurrencyVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IConcurrencyTracked>()
                     .Where(entry => entry.State == EntityState.Modified))
        {
            entry.Entity.Version++;
        }
    }
}
