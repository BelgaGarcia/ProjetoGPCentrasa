using CentraSA.Application.Common;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;

namespace CentraSA.Application.Lookups;

public sealed class LookupService(
    ILookupRepository repository,
    TimeProvider timeProvider) : ILookupService
{
    private static readonly HashSet<string> AllowedColors = new(
        ["blue", "yellow", "purple", "cyan", "red", "green", "gray"],
        StringComparer.Ordinal);

    public async Task<LookupOverviewData> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        LookupReferenceData data = await repository.GetAllAsync(track: false, cancellationToken);
        return new LookupOverviewData(
            data.Areas.Select(MapArea).OrderBy(item => item.Context).ThenBy(item => item.Name).ToList(),
            data.People.Select(MapPerson).OrderBy(item => item.Context).ThenBy(item => item.Name).ToList(),
            data.Statuses.Select(MapStatus).OrderBy(item => item.Context).ThenBy(item => item.SortOrder).ToList(),
            data.Categories.Select(MapCategory).OrderBy(item => item.Context).ThenBy(item => item.SortOrder).ToList());
    }

    public async Task<LookupEditorData> GetCreateEditorAsync(
        LookupKind kind,
        CancellationToken cancellationToken = default)
    {
        LookupReferenceData data = await repository.GetAllAsync(track: false, cancellationToken);
        return CreateEditor(new LookupInput { Kind = kind }, data);
    }

    public async Task<LookupEditorData?> GetEditEditorAsync(
        LookupKind kind,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        LookupReferenceData data = await repository.GetAllAsync(track: false, cancellationToken);
        LookupInput? input = kind switch
        {
            LookupKind.TeamArea => data.Areas.Where(item => item.Id == id).Select(ToInput).SingleOrDefault(),
            LookupKind.Person => data.People.Where(item => item.Id == id).Select(ToInput).SingleOrDefault(),
            LookupKind.Status => data.Statuses.Where(item => item.Id == id).Select(ToInput).SingleOrDefault(),
            LookupKind.Category => data.Categories.Where(item => item.Id == id).Select(ToInput).SingleOrDefault(),
            _ => null,
        };
        return input is null ? null : CreateEditor(input, data);
    }

    public async Task<LookupOperationResult> CreateAsync(
        LookupInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        LookupReferenceData data = await repository.GetAllAsync(track: true, cancellationToken);
        Normalize(input);
        List<string> errors = await ValidateAsync(input, data, existing: null, cancellationToken);
        if (errors.Count > 0)
        {
            return LookupOperationResult.Invalid(errors.ToArray());
        }

        Guid id = Guid.NewGuid();
        switch (input.Kind)
        {
            case LookupKind.TeamArea:
                repository.Add(new TeamArea
                {
                    Id = id,
                    Name = input.Name,
                    NormalizedName = NormalizeName(input.Name),
                    Kind = input.AreaKind,
                    ColorToken = input.ColorToken,
                });
                break;
            case LookupKind.Person:
                repository.Add(new Person
                {
                    Id = id,
                    DisplayName = input.Name,
                    NormalizedName = NormalizeName(input.Name),
                    TeamAreaId = input.TeamAreaId,
                });
                break;
            case LookupKind.Status:
                repository.Add(new StatusDefinition
                {
                    Id = id,
                    Scope = input.Scope,
                    Code = input.Code!,
                    Name = input.Name,
                    LifecycleState = input.LifecycleState,
                    ColorToken = input.ColorToken,
                    SortOrder = input.SortOrder,
                });
                break;
            case LookupKind.Category:
                repository.Add(new Category
                {
                    Id = id,
                    Scope = input.Scope,
                    Code = input.Code!,
                    Name = input.Name,
                    ColorToken = input.ColorToken,
                    SortOrder = input.SortOrder,
                });
                break;
            default:
                return LookupOperationResult.Invalid("Tipo de cadastro inválido.");
        }

        AddHistory(input.Kind, id, actorUserId, ActivityActionType.Created, $"{KindLabel(input.Kind)} '{input.Name}' criado.");
        return await SaveAsync(id, cancellationToken);
    }

    public async Task<LookupOperationResult> UpdateAsync(
        LookupKind kind,
        Guid id,
        LookupInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (kind != input.Kind || input.Id != id)
        {
            return LookupOperationResult.Invalid("O cadastro informado não corresponde à rota.");
        }

        LookupReferenceData data = await repository.GetAllAsync(track: true, cancellationToken);
        object? existing = Find(data, kind, id);
        if (existing is null)
        {
            return LookupOperationResult.NotFound();
        }

        Normalize(input);
        List<string> errors = await ValidateAsync(input, data, existing, cancellationToken);
        if (errors.Count > 0)
        {
            return LookupOperationResult.Invalid(errors.ToArray());
        }

        switch (existing)
        {
            case TeamArea area:
                area.Name = input.Name;
                area.NormalizedName = NormalizeName(input.Name);
                area.Kind = input.AreaKind;
                area.ColorToken = input.ColorToken;
                break;
            case Person person:
                person.DisplayName = input.Name;
                person.NormalizedName = NormalizeName(input.Name);
                person.TeamAreaId = input.TeamAreaId;
                break;
            case StatusDefinition status:
                status.Scope = input.Scope;
                status.Code = input.Code!;
                status.Name = input.Name;
                status.LifecycleState = input.LifecycleState;
                status.ColorToken = input.ColorToken;
                status.SortOrder = input.SortOrder;
                break;
            case Category category:
                category.Scope = input.Scope;
                category.Code = input.Code!;
                category.Name = input.Name;
                category.ColorToken = input.ColorToken;
                category.SortOrder = input.SortOrder;
                break;
        }

        AddHistory(kind, id, actorUserId, ActivityActionType.Updated, $"{KindLabel(kind)} '{input.Name}' atualizado.");
        return await SaveAsync(id, cancellationToken);
    }

    public async Task<LookupOperationResult> ToggleActiveAsync(
        LookupKind kind,
        Guid id,
        bool activate,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        LookupReferenceData data = await repository.GetAllAsync(track: true, cancellationToken);
        object? existing = Find(data, kind, id);
        if (existing is null)
        {
            return LookupOperationResult.NotFound();
        }

        if (existing is StatusDefinition status && status.LifecycleState == LifecycleState.Completed)
        {
            bool otherActiveCompleted = data.Statuses.Any(item => item.Id != id
                && item.Scope == status.Scope
                && item.LifecycleState == LifecycleState.Completed
                && item.IsActive);
            if ((!activate && !otherActiveCompleted) || (activate && otherActiveCompleted))
            {
                return LookupOperationResult.Invalid(
                    "Cada módulo deve manter exatamente um status de conclusão ativo.");
            }
        }

        SetActive(existing, activate);
        string name = GetName(existing);
        AddHistory(
            kind,
            id,
            actorUserId,
            activate ? ActivityActionType.Restored : ActivityActionType.Archived,
            $"{KindLabel(kind)} '{name}' {(activate ? "ativado" : "desativado")}.");
        return await SaveAsync(id, cancellationToken);
    }

    private async Task<List<string>> ValidateAsync(
        LookupInput input,
        LookupReferenceData data,
        object? existing,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        int maxNameLength = input.Kind == LookupKind.Person ? 120 : 100;
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add("Informe o nome.");
        }
        else if (input.Name.Length > maxNameLength)
        {
            errors.Add($"O nome deve ter no máximo {maxNameLength} caracteres.");
        }

        if (input.Kind != LookupKind.Person && !AllowedColors.Contains(input.ColorToken))
        {
            errors.Add("Selecione uma cor válida.");
        }

        if (input.Kind is LookupKind.Status or LookupKind.Category)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
            {
                errors.Add("Informe o código.");
            }
            else if (input.Code.Length > 50 || input.Code.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            {
                errors.Add("O código deve ter até 50 caracteres e usar apenas letras, números e sublinhado.");
            }

            if (input.SortOrder < 0)
            {
                errors.Add("A ordem não pode ser negativa.");
            }
        }

        if (input.Kind == LookupKind.Category && input.Scope == WorkItemScope.Smud)
        {
            errors.Add("SMUDs não utilizam categorias neste modelo.");
        }

        if (input.Kind == LookupKind.Person && input.TeamAreaId.HasValue
            && !data.Areas.Any(area => area.Id == input.TeamAreaId && area.IsActive))
        {
            errors.Add("Selecione uma área ou equipe ativa.");
        }

        Guid? existingId = GetId(existing);
        string normalizedName = NormalizeName(input.Name);
        bool duplicate = input.Kind switch
        {
            LookupKind.TeamArea => data.Areas.Any(item => item.Id != existingId
                && item.Kind == input.AreaKind
                && item.NormalizedName == normalizedName),
            LookupKind.Person => data.People.Any(item => item.Id != existingId
                && item.NormalizedName == normalizedName),
            LookupKind.Status => data.Statuses.Any(item => item.Id != existingId
                && item.Scope == input.Scope
                && item.Code == input.Code),
            LookupKind.Category => data.Categories.Any(item => item.Id != existingId
                && item.Scope == input.Scope
                && item.Code == input.Code),
            _ => false,
        };
        if (duplicate)
        {
            errors.Add("Já existe um cadastro com o mesmo nome ou código no escopo informado.");
        }

        if (input.Kind == LookupKind.Status)
        {
            var current = existing as StatusDefinition;
            if (current is not null
                && (current.Scope != input.Scope || current.LifecycleState != input.LifecycleState)
                && await repository.IsStatusInUseAsync(current.Id, cancellationToken))
            {
                errors.Add("O escopo e o ciclo de um status em uso não podem ser alterados.");
            }

            bool willBeActive = current?.IsActive ?? true;
            if (willBeActive && input.LifecycleState == LifecycleState.Completed
                && data.Statuses.Any(item => item.Id != existingId
                    && item.Scope == input.Scope
                    && item.LifecycleState == LifecycleState.Completed
                    && item.IsActive))
            {
                errors.Add("O módulo já possui um status de conclusão ativo.");
            }
        }

        if (input.Kind == LookupKind.Category
            && existing is Category currentCategory
            && currentCategory.Scope != input.Scope
            && await repository.IsCategoryInUseAsync(currentCategory.Id, cancellationToken))
        {
            errors.Add("O escopo de uma categoria em uso não pode ser alterado.");
        }

        return errors;
    }

    private async Task<LookupOperationResult> SaveAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return LookupOperationResult.Success(id);
        }
        catch (LookupConflictException)
        {
            return LookupOperationResult.Duplicate();
        }
    }

    private void AddHistory(
        LookupKind kind,
        Guid id,
        Guid actorUserId,
        ActivityActionType action,
        string summary) =>
        repository.AddHistory(new ActivityHistory
        {
            Id = Guid.NewGuid(),
            EntityType = EntityType(kind),
            EntityId = id,
            ActionType = action,
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            ActorUserId = actorUserId,
            Summary = summary,
        });

    private static LookupEditorData CreateEditor(LookupInput input, LookupReferenceData data) => new(
        input,
        data.Areas.Where(area => area.IsActive || area.Id == input.TeamAreaId)
            .OrderBy(area => area.Name)
            .Select(area => new LookupAreaOption(area.Id, area.Name))
            .ToList());

    private static object? Find(LookupReferenceData data, LookupKind kind, Guid id) => kind switch
    {
        LookupKind.TeamArea => data.Areas.SingleOrDefault(item => item.Id == id),
        LookupKind.Person => data.People.SingleOrDefault(item => item.Id == id),
        LookupKind.Status => data.Statuses.SingleOrDefault(item => item.Id == id),
        LookupKind.Category => data.Categories.SingleOrDefault(item => item.Id == id),
        _ => null,
    };

    private static Guid? GetId(object? item) => item switch
    {
        TeamArea area => area.Id,
        Person person => person.Id,
        StatusDefinition status => status.Id,
        Category category => category.Id,
        _ => null,
    };

    private static string GetName(object item) => item switch
    {
        TeamArea area => area.Name,
        Person person => person.DisplayName,
        StatusDefinition status => status.Name,
        Category category => category.Name,
        _ => "Cadastro",
    };

    private static void SetActive(object item, bool active)
    {
        switch (item)
        {
            case TeamArea area:
                area.IsActive = active;
                break;
            case Person person:
                person.IsActive = active;
                break;
            case StatusDefinition status:
                status.IsActive = active;
                break;
            case Category category:
                category.IsActive = active;
                break;
        }
    }

    private static LookupListItem MapArea(TeamArea area) => new(
        area.Id,
        LookupKind.TeamArea,
        area.Name,
        null,
        area.Kind == TeamAreaKind.ExternalTeam ? "Equipe externa" : "Área interna",
        area.ColorToken,
        null,
        area.IsActive);

    private static LookupListItem MapPerson(Person person) => new(
        person.Id,
        LookupKind.Person,
        person.DisplayName,
        null,
        person.TeamArea?.Name ?? "Sem área/equipe",
        "blue",
        null,
        person.IsActive);

    private static LookupListItem MapStatus(StatusDefinition status) => new(
        status.Id,
        LookupKind.Status,
        status.Name,
        status.Code,
        $"{ScopeLabel(status.Scope)} · {LifecycleLabel(status.LifecycleState)}",
        status.ColorToken,
        status.SortOrder,
        status.IsActive);

    private static LookupListItem MapCategory(Category category) => new(
        category.Id,
        LookupKind.Category,
        category.Name,
        category.Code,
        ScopeLabel(category.Scope),
        category.ColorToken,
        category.SortOrder,
        category.IsActive);

    private static LookupInput ToInput(TeamArea item) => new()
    {
        Id = item.Id,
        Kind = LookupKind.TeamArea,
        Name = item.Name,
        AreaKind = item.Kind,
        ColorToken = item.ColorToken,
    };

    private static LookupInput ToInput(Person item) => new()
    {
        Id = item.Id,
        Kind = LookupKind.Person,
        Name = item.DisplayName,
        TeamAreaId = item.TeamAreaId,
    };

    private static LookupInput ToInput(StatusDefinition item) => new()
    {
        Id = item.Id,
        Kind = LookupKind.Status,
        Name = item.Name,
        Code = item.Code,
        Scope = item.Scope,
        LifecycleState = item.LifecycleState,
        ColorToken = item.ColorToken,
        SortOrder = item.SortOrder,
    };

    private static LookupInput ToInput(Category item) => new()
    {
        Id = item.Id,
        Kind = LookupKind.Category,
        Name = item.Name,
        Code = item.Code,
        Scope = item.Scope,
        ColorToken = item.ColorToken,
        SortOrder = item.SortOrder,
    };

    private static void Normalize(LookupInput input)
    {
        input.Name = input.Name?.Trim() ?? string.Empty;
        input.Code = string.IsNullOrWhiteSpace(input.Code)
            ? null
            : input.Code.Trim().ToUpperInvariant().Replace(' ', '_').Replace('-', '_');
        input.ColorToken = input.ColorToken?.Trim().ToLowerInvariant() ?? "blue";
    }

    private static string NormalizeName(string value) => value.Trim().ToUpperInvariant();

    private static TrackedEntityType EntityType(LookupKind kind) => kind switch
    {
        LookupKind.TeamArea => TrackedEntityType.TeamArea,
        LookupKind.Person => TrackedEntityType.Person,
        LookupKind.Status => TrackedEntityType.StatusDefinition,
        LookupKind.Category => TrackedEntityType.Category,
        _ => throw new InvalidOperationException("Tipo de cadastro inválido."),
    };

    private static string KindLabel(LookupKind kind) => kind switch
    {
        LookupKind.TeamArea => "Área/equipe",
        LookupKind.Person => "Pessoa",
        LookupKind.Status => "Status",
        LookupKind.Category => "Categoria",
        _ => "Cadastro",
    };

    private static string ScopeLabel(WorkItemScope scope) => scope switch
    {
        WorkItemScope.PendingTask => "Pendências",
        WorkItemScope.Smud => "SMUDs",
        WorkItemScope.SupportTicket => "Chamados",
        _ => scope.ToString(),
    };

    private static string LifecycleLabel(LifecycleState lifecycle) => lifecycle switch
    {
        LifecycleState.Active => "Ativo",
        LifecycleState.Completed => "Concluído",
        LifecycleState.Cancelled => "Cancelado",
        _ => lifecycle.ToString(),
    };
}
