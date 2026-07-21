using System.ComponentModel.DataAnnotations;
using CentraSA.Application.Smuds;
using CentraSA.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CentraSA.Web.ViewModels.Smuds;

public sealed class SmudFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o código do SMUD.")]
    [StringLength(30, ErrorMessage = "O código deve ter no máximo 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o título do SMUD.")]
    [StringLength(200, ErrorMessage = "O título deve ter no máximo 200 caracteres.")]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "A descrição deve ter no máximo 4.000 caracteres.")]
    [Display(Name = "Descrição")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Selecione a área responsável.")]
    [Display(Name = "Área responsável")]
    public Guid ResponsibleAreaId { get; set; }

    [Display(Name = "Pessoa responsável")]
    public Guid? ResponsiblePersonId { get; set; }

    [Required(ErrorMessage = "Selecione o status.")]
    [Display(Name = "Status")]
    public Guid StatusId { get; set; }

    [Display(Name = "Prioridade")]
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    [DataType(DataType.Date)]
    [Display(Name = "Data de abertura")]
    public DateOnly? OpenedOn { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Prazo")]
    public DateOnly? DueDate { get; set; }

    [StringLength(1000, ErrorMessage = "A ação necessária deve ter no máximo 1.000 caracteres.")]
    [Display(Name = "Ação necessária")]
    public string? RequiredAction { get; set; }

    [StringLength(4000, ErrorMessage = "As observações devem ter no máximo 4.000 caracteres.")]
    [Display(Name = "Observações")]
    public string? Notes { get; set; }

    public long Version { get; set; }

    [ValidateNever]
    public SmudFormOptions Options { get; set; } = null!;

    public SmudInput ToInput() => new()
    {
        Code = Code,
        Title = Title,
        Description = Description,
        ResponsibleAreaId = ResponsibleAreaId,
        ResponsiblePersonId = ResponsiblePersonId,
        StatusId = StatusId,
        Priority = Priority,
        OpenedOn = OpenedOn,
        DueDate = DueDate,
        RequiredAction = RequiredAction,
        Notes = Notes,
        Version = Version,
    };

    public static SmudFormViewModel FromEditor(SmudEditorData editor) => new()
    {
        Id = editor.Id,
        Code = editor.Input.Code,
        Title = editor.Input.Title,
        Description = editor.Input.Description,
        ResponsibleAreaId = editor.Input.ResponsibleAreaId,
        ResponsiblePersonId = editor.Input.ResponsiblePersonId,
        StatusId = editor.Input.StatusId,
        Priority = editor.Input.Priority,
        OpenedOn = editor.Input.OpenedOn,
        DueDate = editor.Input.DueDate,
        RequiredAction = editor.Input.RequiredAction,
        Notes = editor.Input.Notes,
        Version = editor.Input.Version,
        Options = editor.Options,
    };
}
