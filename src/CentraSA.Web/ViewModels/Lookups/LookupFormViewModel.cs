using System.ComponentModel.DataAnnotations;
using CentraSA.Application.Lookups;
using CentraSA.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CentraSA.Web.ViewModels.Lookups;

public sealed class LookupFormViewModel
{
    public Guid? Id { get; set; }

    public LookupKind Kind { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "O código deve ter no máximo 50 caracteres.")]
    [Display(Name = "Código")]
    public string? Code { get; set; }

    [Display(Name = "Módulo")]
    public WorkItemScope Scope { get; set; }

    [Display(Name = "Tipo")]
    public TeamAreaKind AreaKind { get; set; }

    [Display(Name = "Área/equipe")]
    public Guid? TeamAreaId { get; set; }

    [Display(Name = "Ciclo")]
    public LifecycleState LifecycleState { get; set; } = LifecycleState.Active;

    [Display(Name = "Cor")]
    public string ColorToken { get; set; } = "blue";

    [Range(0, 100000, ErrorMessage = "A ordem deve estar entre 0 e 100.000.")]
    [Display(Name = "Ordem")]
    public int SortOrder { get; set; } = 10;

    [ValidateNever]
    public IReadOnlyList<LookupAreaOption> Areas { get; set; } = [];

    public LookupInput ToInput() => new()
    {
        Id = Id,
        Kind = Kind,
        Name = Name,
        Code = Code,
        Scope = Scope,
        AreaKind = AreaKind,
        TeamAreaId = TeamAreaId,
        LifecycleState = LifecycleState,
        ColorToken = ColorToken,
        SortOrder = SortOrder,
    };

    public static LookupFormViewModel FromEditor(
        LookupEditorData editor,
        LookupFormViewModel? posted = null) => new()
        {
            Id = editor.Input.Id,
            Kind = editor.Input.Kind,
            Name = posted?.Name ?? editor.Input.Name,
            Code = posted?.Code ?? editor.Input.Code,
            Scope = posted?.Scope ?? editor.Input.Scope,
            AreaKind = posted?.AreaKind ?? editor.Input.AreaKind,
            TeamAreaId = posted?.TeamAreaId ?? editor.Input.TeamAreaId,
            LifecycleState = posted?.LifecycleState ?? editor.Input.LifecycleState,
            ColorToken = posted?.ColorToken ?? editor.Input.ColorToken,
            SortOrder = posted?.SortOrder ?? editor.Input.SortOrder,
            Areas = editor.Areas,
        };
}
