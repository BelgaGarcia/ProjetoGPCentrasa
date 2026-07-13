using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;
using CentraSA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Seeding;

internal static class DemoDataSeeder
{
    private static readonly DateTime SeedTimestamp = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    public static async Task SeedAsync(CentraSaDbContext dbContext, CancellationToken cancellationToken)
    {
        bool hasOperationalData = await dbContext.PendingTasks.AnyAsync(cancellationToken)
            || await dbContext.Smuds.AnyAsync(cancellationToken)
            || await dbContext.SupportTickets.AnyAsync(cancellationToken);

        if (hasOperationalData)
        {
            return;
        }

        Dictionary<string, TeamArea> areas = (await dbContext.TeamAreas.ToListAsync(cancellationToken))
            .ToDictionary(area => area.NormalizedName, StringComparer.OrdinalIgnoreCase);
        Dictionary<(WorkItemScope Scope, string Code), StatusDefinition> statuses =
            (await dbContext.StatusDefinitions.ToListAsync(cancellationToken))
            .ToDictionary(status => (status.Scope, status.Code));
        Dictionary<(WorkItemScope Scope, string Code), Category> categories =
            (await dbContext.Categories.ToListAsync(cancellationToken))
            .ToDictionary(category => (category.Scope, category.Code));

        TeamArea centraSa = areas["CENTRASA"];
        TeamArea operations = areas["OPERAÇÕES"];
        TeamArea support = areas["ATENDIMENTO"];
        TeamArea quality = areas["QUALIDADE"];
        TeamArea planning = areas["PLANEJAMENTO"];
        TeamArea platformTeam = areas["EQUIPE PLATAFORMA"];
        TeamArea supplierAlpha = areas["FORNECEDOR ALFA"];
        TeamArea supplierBeta = areas["FORNECEDOR BETA"];

        var people = new[]
        {
            CreatePerson("Pessoa Demo A", centraSa.Id),
            CreatePerson("Pessoa Demo B", centraSa.Id),
            CreatePerson("Pessoa Demo C", centraSa.Id),
        };
        dbContext.People.AddRange(people);

        StatusDefinition pendingOpen = statuses[(WorkItemScope.PendingTask, SeedCodes.PendingOpen)];
        StatusDefinition smudPendingSupplier = statuses[(WorkItemScope.Smud, SeedCodes.SmudPendingSupplier)];
        StatusDefinition smudAwaitingValidation = statuses[(WorkItemScope.Smud, SeedCodes.SmudAwaitingValidation)];
        StatusDefinition ticketAwaitingTests = statuses[(WorkItemScope.SupportTicket, SeedCodes.TicketAwaitingTests)];
        StatusDefinition ticketAwaitingExternal = statuses[(WorkItemScope.SupportTicket, SeedCodes.TicketAwaitingExternal)];

        var smud083 = CreateSmud("SMUD083", "Melhoria na impressão do relatório operacional", centraSa.Id, people[0].Id, smudAwaitingValidation.Id, new DateOnly(2026, 7, 9), "Validar desenvolvimento");
        var smud081 = CreateSmud("SMUD081", "Nova integração de arquivo", supplierAlpha.Id, people[1].Id, smudPendingSupplier.Id, new DateOnly(2026, 7, 13), "Aguardar desenvolvimento");
        var smud077 = CreateSmud("SMUD077", "Segunda validação de cadastro", supplierAlpha.Id, people[1].Id, smudPendingSupplier.Id, new DateOnly(2026, 7, 13), "Aguardar retorno técnico");
        var smud085 = CreateSmud("SMUD085", "Complemento de regras de aprovação", supplierAlpha.Id, people[1].Id, smudPendingSupplier.Id, new DateOnly(2026, 7, 20), "Aguardar desenvolvimento");
        var smud084 = CreateSmud("SMUD084", "Validação de quantidade no apontamento", supplierAlpha.Id, people[2].Id, smudPendingSupplier.Id, new DateOnly(2026, 7, 13), "Aguardar desenvolvimento");
        var smud086 = CreateSmud("SMUD086", "Ajuste demonstrativo de relatório", supplierAlpha.Id, null, smudPendingSupplier.Id, new DateOnly(2026, 7, 24), "Aguardar análise técnica");
        var smud087 = CreateSmud("SMUD087", "Validação demonstrativa de integração", centraSa.Id, null, smudAwaitingValidation.Id, new DateOnly(2026, 7, 17), "Executar validação funcional");
        var smuds = new[] { smud083, smud081, smud077, smud085, smud084, smud086, smud087 };
        dbContext.Smuds.AddRange(smuds);

        Category incident = categories[(WorkItemScope.SupportTicket, SeedCodes.TicketIncident)];
        Category pendingTests = categories[(WorkItemScope.SupportTicket, SeedCodes.TicketPendingInternalTests)];
        var ticket14779 = CreateTicket("14779", "Validação rejeitada no fluxo de pedidos", new DateOnly(2026, 5, 12), centraSa.Id, ticketAwaitingTests.Id, pendingTests.Id, new DateOnly(2026, 7, 13));
        var ticket14896 = CreateTicket("14896", "Campos demonstrativos ausentes no recebimento", new DateOnly(2026, 6, 3), supplierBeta.Id, ticketAwaitingExternal.Id, incident.Id, new DateOnly(2026, 7, 31));
        var ticket14899 = CreateTicket("14899", "Inconsistência no relatório operacional", new DateOnly(2026, 6, 8), centraSa.Id, ticketAwaitingTests.Id, pendingTests.Id, new DateOnly(2026, 7, 13));
        var ticket14973 = CreateTicket("14973", "Reprocessamento de registro de entrada", new DateOnly(2026, 6, 19), centraSa.Id, ticketAwaitingTests.Id, pendingTests.Id, new DateOnly(2026, 7, 13));
        var ticket14995 = CreateTicket("14995", "Erro ao desfazer movimentação", new DateOnly(2026, 6, 19), supplierBeta.Id, ticketAwaitingExternal.Id, incident.Id, new DateOnly(2026, 7, 13));
        var ticket15056 = CreateTicket("15056", "Falha ao transmitir arquivo de integração", new DateOnly(2026, 7, 2), supplierAlpha.Id, ticketAwaitingExternal.Id, pendingTests.Id, new DateOnly(2026, 7, 13));
        var ticket15052 = CreateTicket("15052", "Erro de transmissão no ambiente de testes", new DateOnly(2026, 7, 6), supplierBeta.Id, ticketAwaitingExternal.Id, incident.Id, new DateOnly(2026, 7, 20));
        var tickets = new[] { ticket14779, ticket14896, ticket14899, ticket14973, ticket14995, ticket15056, ticket15052 };
        dbContext.SupportTickets.AddRange(tickets);

        Category operational = categories[(WorkItemScope.PendingTask, SeedCodes.PendingOperational)];
        Category validation = categories[(WorkItemScope.PendingTask, SeedCodes.PendingValidation)];
        Category report = categories[(WorkItemScope.PendingTask, SeedCodes.PendingReport)];
        var tasks = new[]
        {
            CreateTask("Validar impressão de etiquetas após processamento", platformTeam.Id, pendingOpen.Id, operational.Id, 10, null),
            CreateTask("Validar SMUD081", quality.Id, pendingOpen.Id, validation.Id, 20, null),
            CreateTask("Validar SMUD083", planning.Id, pendingOpen.Id, validation.Id, 30, new DateOnly(2026, 7, 13)),
            CreateTask("Validar chamado 14779", support.Id, pendingOpen.Id, validation.Id, 40, null),
            CreateTask("Validar chamado 14973", operations.Id, pendingOpen.Id, validation.Id, 50, null),
            CreateTask("Validar chamado 14899", platformTeam.Id, pendingOpen.Id, validation.Id, 60, null),
            CreateTask("Revisar parâmetro demonstrativo de processamento", centraSa.Id, pendingOpen.Id, operational.Id, 70, new DateOnly(2026, 7, 10)),
            CreateTask("Mapear relatórios internos", centraSa.Id, pendingOpen.Id, report.Id, 80, new DateOnly(2026, 7, 13)),
            CreateTask("Registrar feedback dos relatórios", planning.Id, pendingOpen.Id, report.Id, 90, new DateOnly(2026, 7, 17)),
            CreateTask("Validar fluxo de atendimento após entrega da SMUD081", support.Id, pendingOpen.Id, validation.Id, 100, null),
        };
        dbContext.PendingTasks.AddRange(tasks);

        dbContext.WorkItemReferences.AddRange(
            CreateReference(tasks[1].Id, smudId: smud081.Id),
            CreateReference(tasks[2].Id, smudId: smud083.Id),
            CreateReference(tasks[3].Id, ticketId: ticket14779.Id),
            CreateReference(tasks[4].Id, ticketId: ticket14973.Id),
            CreateReference(tasks[5].Id, ticketId: ticket14899.Id),
            CreateReference(tasks[9].Id, smudId: smud081.Id));

        foreach (Smud smud in smuds)
        {
            dbContext.ActivityHistories.Add(CreateHistory(TrackedEntityType.Smud, smud.Id, $"{smud.Code} criado pelo seed de demonstração."));
        }

        foreach (SupportTicket ticket in tickets)
        {
            dbContext.ActivityHistories.Add(CreateHistory(TrackedEntityType.SupportTicket, ticket.Id, $"Chamado {ticket.TicketNumber} criado pelo seed de demonstração."));
        }

        foreach (PendingTask task in tasks)
        {
            dbContext.ActivityHistories.Add(CreateHistory(TrackedEntityType.PendingTask, task.Id, "Pendência criada pelo seed de demonstração."));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Person CreatePerson(string name, Guid areaId) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = name,
        NormalizedName = name.ToUpperInvariant(),
        TeamAreaId = areaId,
    };

    private static Smud CreateSmud(
        string code,
        string title,
        Guid areaId,
        Guid? personId,
        Guid statusId,
        DateOnly dueDate,
        string requiredAction)
    {
        string normalizedCode = SmudCodeNormalizer.Normalize(code);
        return new Smud
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            NormalizedCode = normalizedCode,
            Title = title,
            ResponsibleAreaId = areaId,
            ResponsiblePersonId = personId,
            StatusDefinitionId = statusId,
            Priority = PriorityLevel.Medium,
            DueDate = dueDate,
            RequiredAction = requiredAction,
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
        };
    }

    private static SupportTicket CreateTicket(
        string number,
        string title,
        DateOnly openedOn,
        Guid areaId,
        Guid statusId,
        Guid categoryId,
        DateOnly dueDate)
    {
        string normalizedNumber = TicketNumberNormalizer.Normalize(number);
        return new SupportTicket
        {
            Id = Guid.NewGuid(),
            TicketNumber = normalizedNumber,
            NormalizedNumber = normalizedNumber,
            Title = title,
            OpenedOn = openedOn,
            ResponsibleAreaId = areaId,
            StatusDefinitionId = statusId,
            CategoryId = categoryId,
            Priority = PriorityLevel.Medium,
            DueDate = dueDate,
            PendingAction = "Acompanhar retorno e validar a solução.",
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
        };
    }

    private static PendingTask CreateTask(
        string title,
        Guid areaId,
        Guid statusId,
        Guid categoryId,
        int order,
        DateOnly? dueDate) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            ResponsibleAreaId = areaId,
            StatusDefinitionId = statusId,
            CategoryId = categoryId,
            Priority = PriorityLevel.Medium,
            DueDate = dueDate,
            Origin = "Seed demonstrativo sanitizado",
            PresentationOrder = order,
            CreatedAtUtc = SeedTimestamp,
            UpdatedAtUtc = SeedTimestamp,
        };

    private static WorkItemReference CreateReference(Guid taskId, Guid? smudId = null, Guid? ticketId = null) => new()
    {
        Id = Guid.NewGuid(),
        PendingTaskId = taskId,
        SmudId = smudId,
        SupportTicketId = ticketId,
    };

    private static ActivityHistory CreateHistory(TrackedEntityType entityType, Guid entityId, string summary) => new()
    {
        Id = Guid.NewGuid(),
        EntityType = entityType,
        EntityId = entityId,
        ActionType = ActivityActionType.Created,
        OccurredAtUtc = SeedTimestamp,
        Summary = summary,
    };
}
