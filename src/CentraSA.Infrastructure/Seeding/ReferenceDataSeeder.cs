using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Seeding;

internal static class ReferenceDataSeeder
{
    public static async Task SeedAsync(CentraSaDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedAreasAsync(dbContext, cancellationToken);
        await SeedStatusesAsync(dbContext, cancellationToken);
        await SeedCategoriesAsync(dbContext, cancellationToken);
    }

    private static async Task SeedAreasAsync(CentraSaDbContext dbContext, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new AreaSeed("CentraSA", TeamAreaKind.InternalArea, "blue"),
            new AreaSeed("Fiscal", TeamAreaKind.InternalArea, "yellow"),
            new AreaSeed("Faturamento", TeamAreaKind.InternalArea, "purple"),
            new AreaSeed("Recebimento", TeamAreaKind.InternalArea, "cyan"),
            new AreaSeed("PCP", TeamAreaKind.InternalArea, "yellow"),
            new AreaSeed("Equipe Protheus", TeamAreaKind.InternalArea, "blue"),
            new AreaSeed("DSM", TeamAreaKind.ExternalTeam, "red"),
            new AreaSeed("TOTVS", TeamAreaKind.ExternalTeam, "blue"),
        };

        var existing = await dbContext.TeamAreas
            .Select(area => new { area.Kind, area.NormalizedName })
            .ToListAsync(cancellationToken);

        foreach (AreaSeed definition in definitions)
        {
            string normalizedName = NormalizeName(definition.Name);
            if (existing.Any(area => area.Kind == definition.Kind && area.NormalizedName == normalizedName))
            {
                continue;
            }

            dbContext.TeamAreas.Add(new TeamArea
            {
                Id = Guid.NewGuid(),
                Name = definition.Name,
                NormalizedName = normalizedName,
                Kind = definition.Kind,
                ColorToken = definition.ColorToken,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedStatusesAsync(CentraSaDbContext dbContext, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new StatusSeed(WorkItemScope.PendingTask, SeedCodes.PendingOpen, "Aberta", LifecycleState.Active, "blue", 10),
            new StatusSeed(WorkItemScope.PendingTask, SeedCodes.PendingInProgress, "Em andamento", LifecycleState.Active, "cyan", 20),
            new StatusSeed(WorkItemScope.PendingTask, SeedCodes.PendingBlocked, "Bloqueada", LifecycleState.Active, "red", 30),
            new StatusSeed(WorkItemScope.PendingTask, SeedCodes.PendingCompleted, "Concluída", LifecycleState.Completed, "green", 40),
            new StatusSeed(WorkItemScope.PendingTask, SeedCodes.PendingCancelled, "Cancelada", LifecycleState.Cancelled, "gray", 50),

            new StatusSeed(WorkItemScope.Smud, SeedCodes.SmudPendingCentraSa, "Pendente CentraSA", LifecycleState.Active, "yellow", 10),
            new StatusSeed(WorkItemScope.Smud, SeedCodes.SmudPendingDsm, "Pendente DSM", LifecycleState.Active, "purple", 20),
            new StatusSeed(WorkItemScope.Smud, SeedCodes.SmudAwaitingValidation, "Aguardando validação", LifecycleState.Active, "yellow", 30),
            new StatusSeed(WorkItemScope.Smud, SeedCodes.SmudInDevelopment, "Em desenvolvimento", LifecycleState.Active, "blue", 40),
            new StatusSeed(WorkItemScope.Smud, SeedCodes.SmudCompleted, "Concluído", LifecycleState.Completed, "green", 50),
            new StatusSeed(WorkItemScope.Smud, SeedCodes.SmudCancelled, "Cancelado", LifecycleState.Cancelled, "gray", 60),

            new StatusSeed(WorkItemScope.SupportTicket, SeedCodes.TicketOpen, "Aberto", LifecycleState.Active, "blue", 10),
            new StatusSeed(WorkItemScope.SupportTicket, SeedCodes.TicketInProgress, "Em andamento", LifecycleState.Active, "cyan", 20),
            new StatusSeed(WorkItemScope.SupportTicket, SeedCodes.TicketAwaitingExternal, "Aguardando retorno externo", LifecycleState.Active, "purple", 30),
            new StatusSeed(WorkItemScope.SupportTicket, SeedCodes.TicketAwaitingTests, "Aguardando testes", LifecycleState.Active, "yellow", 40),
            new StatusSeed(WorkItemScope.SupportTicket, SeedCodes.TicketCompleted, "Concluído", LifecycleState.Completed, "green", 50),
            new StatusSeed(WorkItemScope.SupportTicket, SeedCodes.TicketCancelled, "Cancelado", LifecycleState.Cancelled, "gray", 60),
        };

        var existing = await dbContext.StatusDefinitions
            .Select(status => new { status.Scope, status.Code })
            .ToListAsync(cancellationToken);

        foreach (StatusSeed definition in definitions)
        {
            if (existing.Any(status => status.Scope == definition.Scope && status.Code == definition.Code))
            {
                continue;
            }

            dbContext.StatusDefinitions.Add(new StatusDefinition
            {
                Id = Guid.NewGuid(),
                Scope = definition.Scope,
                Code = definition.Code,
                Name = definition.Name,
                LifecycleState = definition.LifecycleState,
                ColorToken = definition.ColorToken,
                SortOrder = definition.SortOrder,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCategoriesAsync(CentraSaDbContext dbContext, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new CategorySeed(WorkItemScope.PendingTask, SeedCodes.PendingOperational, "Operacional", "blue", 10),
            new CategorySeed(WorkItemScope.PendingTask, SeedCodes.PendingValidation, "Validação", "yellow", 20),
            new CategorySeed(WorkItemScope.PendingTask, SeedCodes.PendingReport, "Relatório", "cyan", 30),
            new CategorySeed(WorkItemScope.SupportTicket, SeedCodes.TicketPe, "Chamado P.E.", "red", 10),
            new CategorySeed(WorkItemScope.SupportTicket, SeedCodes.TicketFrozen, "Congelado", "cyan", 20),
            new CategorySeed(WorkItemScope.SupportTicket, SeedCodes.TicketPendingCentraSaTests, "Pendente de testes CentraSA", "yellow", 30),
            new CategorySeed(WorkItemScope.SupportTicket, SeedCodes.TicketNonPe, "Chamado não P.E.", "purple", 40),
        };

        var existing = await dbContext.Categories
            .Select(category => new { category.Scope, category.Code })
            .ToListAsync(cancellationToken);

        foreach (CategorySeed definition in definitions)
        {
            if (existing.Any(category => category.Scope == definition.Scope && category.Code == definition.Code))
            {
                continue;
            }

            dbContext.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Scope = definition.Scope,
                Code = definition.Code,
                Name = definition.Name,
                ColorToken = definition.ColorToken,
                SortOrder = definition.SortOrder,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private sealed record AreaSeed(string Name, TeamAreaKind Kind, string ColorToken);

    private sealed record StatusSeed(
        WorkItemScope Scope,
        string Code,
        string Name,
        LifecycleState LifecycleState,
        string ColorToken,
        int SortOrder);

    private sealed record CategorySeed(
        WorkItemScope Scope,
        string Code,
        string Name,
        string ColorToken,
        int SortOrder);
}
