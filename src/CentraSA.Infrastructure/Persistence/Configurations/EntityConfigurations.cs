using CentraSA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CentraSA.Infrastructure.Persistence.Configurations;

internal sealed class TeamAreaConfiguration : IEntityTypeConfiguration<TeamArea>
{
    public void Configure(EntityTypeBuilder<TeamArea> builder)
    {
        builder.ToTable("TeamAreas");
        builder.HasKey(area => area.Id);
        builder.Property(area => area.Name).HasMaxLength(100).IsRequired();
        builder.Property(area => area.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(area => area.Kind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(area => area.ColorToken).HasMaxLength(30).IsRequired();
        builder.HasIndex(area => new { area.Kind, area.NormalizedName }).IsUnique();
        builder.HasIndex(area => area.IsActive);
    }
}

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People");
        builder.HasKey(person => person.Id);
        builder.Property(person => person.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(person => person.NormalizedName).HasMaxLength(120).IsRequired();
        builder.HasIndex(person => person.NormalizedName).IsUnique();
        builder.HasIndex(person => person.IsActive);
        builder.HasOne(person => person.TeamArea)
            .WithMany(area => area.People)
            .HasForeignKey(person => person.TeamAreaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StatusDefinitionConfiguration : IEntityTypeConfiguration<StatusDefinition>
{
    public void Configure(EntityTypeBuilder<StatusDefinition> builder)
    {
        builder.ToTable("StatusDefinitions", table => table.HasCheckConstraint("CK_StatusDefinition_SortOrder", "SortOrder >= 0"));
        builder.HasKey(status => status.Id);
        builder.Property(status => status.Scope).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(status => status.Code).HasMaxLength(50).IsRequired();
        builder.Property(status => status.Name).HasMaxLength(100).IsRequired();
        builder.Property(status => status.LifecycleState).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(status => status.ColorToken).HasMaxLength(30).IsRequired();
        builder.HasIndex(status => new { status.Scope, status.Code }).IsUnique();
        builder.HasIndex(status => new { status.Scope, status.IsActive, status.SortOrder });
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", table => table.HasCheckConstraint("CK_Category_SortOrder", "SortOrder >= 0"));
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Scope).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(category => category.Code).HasMaxLength(50).IsRequired();
        builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
        builder.Property(category => category.ColorToken).HasMaxLength(30).IsRequired();
        builder.HasIndex(category => new { category.Scope, category.Code }).IsUnique();
        builder.HasIndex(category => new { category.Scope, category.IsActive, category.SortOrder });
    }
}

internal sealed class PendingTaskConfiguration : IEntityTypeConfiguration<PendingTask>
{
    public void Configure(EntityTypeBuilder<PendingTask> builder)
    {
        builder.ToTable("PendingTasks", table =>
        {
            table.HasCheckConstraint("CK_PendingTask_PresentationOrder", "PresentationOrder >= 0");
            table.HasCheckConstraint("CK_PendingTask_Version", "Version >= 1");
        });
        builder.HasKey(task => task.Id);
        ConfigureText(builder);
        builder.Property(task => task.Origin).HasMaxLength(120);
        builder.Property(task => task.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(task => task.Version).IsConcurrencyToken();
        builder.HasOne(task => task.ResponsiblePerson).WithMany().HasForeignKey(task => task.ResponsiblePersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(task => task.ResponsibleArea).WithMany().HasForeignKey(task => task.ResponsibleAreaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(task => task.StatusDefinition).WithMany().HasForeignKey(task => task.StatusDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(task => task.Category).WithMany().HasForeignKey(task => task.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(task => new { task.ArchivedAtUtc, task.StatusDefinitionId, task.DueDate });
        builder.HasIndex(task => new { task.ResponsibleAreaId, task.PresentationOrder });
    }

    private static void ConfigureText(EntityTypeBuilder<PendingTask> builder)
    {
        builder.Property(task => task.Title).HasMaxLength(200).IsRequired();
        builder.Property(task => task.Description).HasMaxLength(4000);
        builder.Property(task => task.Notes).HasMaxLength(4000);
    }
}

internal sealed class SmudConfiguration : IEntityTypeConfiguration<Smud>
{
    public void Configure(EntityTypeBuilder<Smud> builder)
    {
        builder.ToTable("Smuds", table => table.HasCheckConstraint("CK_Smud_Version", "Version >= 1"));
        builder.HasKey(smud => smud.Id);
        builder.Property(smud => smud.Code).HasMaxLength(30).IsRequired();
        builder.Property(smud => smud.NormalizedCode).HasMaxLength(30).IsRequired();
        builder.Property(smud => smud.Title).HasMaxLength(200).IsRequired();
        builder.Property(smud => smud.Description).HasMaxLength(4000);
        builder.Property(smud => smud.RequiredAction).HasMaxLength(1000);
        builder.Property(smud => smud.Notes).HasMaxLength(4000);
        builder.Property(smud => smud.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(smud => smud.Version).IsConcurrencyToken();
        builder.HasOne(smud => smud.ResponsiblePerson).WithMany().HasForeignKey(smud => smud.ResponsiblePersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(smud => smud.ResponsibleArea).WithMany().HasForeignKey(smud => smud.ResponsibleAreaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(smud => smud.StatusDefinition).WithMany().HasForeignKey(smud => smud.StatusDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(smud => smud.NormalizedCode).IsUnique();
        builder.HasIndex(smud => new { smud.ArchivedAtUtc, smud.StatusDefinitionId, smud.DueDate });
        builder.HasIndex(smud => smud.ResponsibleAreaId);
    }
}

internal sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("SupportTickets", table => table.HasCheckConstraint("CK_SupportTicket_Version", "Version >= 1"));
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.TicketNumber).HasMaxLength(30).IsRequired();
        builder.Property(ticket => ticket.NormalizedNumber).HasMaxLength(30).IsRequired();
        builder.Property(ticket => ticket.Title).HasMaxLength(200).IsRequired();
        builder.Property(ticket => ticket.Description).HasMaxLength(4000);
        builder.Property(ticket => ticket.PendingAction).HasMaxLength(1000);
        builder.Property(ticket => ticket.Notes).HasMaxLength(4000);
        builder.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(ticket => ticket.Version).IsConcurrencyToken();
        builder.HasOne(ticket => ticket.ResponsiblePerson).WithMany().HasForeignKey(ticket => ticket.ResponsiblePersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ticket => ticket.ResponsibleArea).WithMany().HasForeignKey(ticket => ticket.ResponsibleAreaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ticket => ticket.StatusDefinition).WithMany().HasForeignKey(ticket => ticket.StatusDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ticket => ticket.Category).WithMany().HasForeignKey(ticket => ticket.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(ticket => ticket.NormalizedNumber).IsUnique();
        builder.HasIndex(ticket => new { ticket.ArchivedAtUtc, ticket.StatusDefinitionId, ticket.DueDate });
        builder.HasIndex(ticket => new { ticket.CategoryId, ticket.ResponsibleAreaId });
    }
}

internal sealed class WorkItemReferenceConfiguration : IEntityTypeConfiguration<WorkItemReference>
{
    public void Configure(EntityTypeBuilder<WorkItemReference> builder)
    {
        builder.ToTable("WorkItemReferences", table => table.HasCheckConstraint(
            "CK_WorkItemReference_ExactlyOneTarget",
            "(SmudId IS NOT NULL AND SupportTicketId IS NULL) OR (SmudId IS NULL AND SupportTicketId IS NOT NULL)"));
        builder.HasKey(reference => reference.Id);
        builder.HasOne(reference => reference.PendingTask).WithMany(task => task.References).HasForeignKey(reference => reference.PendingTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(reference => reference.Smud).WithMany().HasForeignKey(reference => reference.SmudId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reference => reference.SupportTicket).WithMany().HasForeignKey(reference => reference.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(reference => new { reference.PendingTaskId, reference.SmudId }).IsUnique();
        builder.HasIndex(reference => new { reference.PendingTaskId, reference.SupportTicketId }).IsUnique();
    }
}

internal sealed class DailyMeetingConfiguration : IEntityTypeConfiguration<DailyMeeting>
{
    public void Configure(EntityTypeBuilder<DailyMeeting> builder)
    {
        builder.ToTable("DailyMeetings", table => table.HasCheckConstraint("CK_DailyMeeting_Version", "Version >= 1"));
        builder.HasKey(meeting => meeting.Id);
        builder.Property(meeting => meeting.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(meeting => meeting.GeneralNotes).HasMaxLength(4000);
        builder.Property(meeting => meeting.Version).IsConcurrencyToken();
        builder.HasIndex(meeting => new { meeting.MeetingDate, meeting.Status });
        builder.HasIndex(meeting => meeting.ArchivedAtUtc);
    }
}

internal sealed class DailyMeetingItemConfiguration : IEntityTypeConfiguration<DailyMeetingItem>
{
    public void Configure(EntityTypeBuilder<DailyMeetingItem> builder)
    {
        builder.ToTable("DailyMeetingItems", table =>
        {
            table.HasCheckConstraint("CK_DailyMeetingItem_SortOrder", "SortOrder >= 0");
            table.HasCheckConstraint(
                "CK_DailyMeetingItem_ExactlyOneSource",
                "(PendingTaskId IS NOT NULL AND SmudId IS NULL AND SupportTicketId IS NULL) OR " +
                "(PendingTaskId IS NULL AND SmudId IS NOT NULL AND SupportTicketId IS NULL) OR " +
                "(PendingTaskId IS NULL AND SmudId IS NULL AND SupportTicketId IS NOT NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Section).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.PresentationNotes).HasMaxLength(2000);
        builder.Property(item => item.SnapshotTitle).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SnapshotStatus).HasMaxLength(100).IsRequired();
        builder.Property(item => item.SnapshotResponsible).HasMaxLength(120);
        builder.HasOne(item => item.DailyMeeting).WithMany(meeting => meeting.Items).HasForeignKey(item => item.DailyMeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.PendingTask).WithMany().HasForeignKey(item => item.PendingTaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Smud).WithMany().HasForeignKey(item => item.SmudId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SupportTicket).WithMany().HasForeignKey(item => item.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.DailyMeetingId, item.PendingTaskId }).IsUnique();
        builder.HasIndex(item => new { item.DailyMeetingId, item.SmudId }).IsUnique();
        builder.HasIndex(item => new { item.DailyMeetingId, item.SupportTicketId }).IsUnique();
        builder.HasIndex(item => new { item.DailyMeetingId, item.Section, item.SortOrder });
    }
}

internal sealed class ActivityHistoryConfiguration : IEntityTypeConfiguration<ActivityHistory>
{
    public void Configure(EntityTypeBuilder<ActivityHistory> builder)
    {
        builder.ToTable("ActivityHistories");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.EntityType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(history => history.ActionType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(history => history.Summary).HasMaxLength(500).IsRequired();
        builder.Property(history => history.ChangesJson).HasMaxLength(8000);
        builder.HasIndex(history => new { history.EntityType, history.EntityId, history.OccurredAtUtc });
        builder.HasIndex(history => history.OccurredAtUtc);
    }
}
