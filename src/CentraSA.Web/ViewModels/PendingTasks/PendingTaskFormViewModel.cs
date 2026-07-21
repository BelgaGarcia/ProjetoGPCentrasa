using System.ComponentModel.DataAnnotations;
using CentraSA.Application.PendingTasks;
using CentraSA.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CentraSA.Web.ViewModels.PendingTasks;

public sealed class PendingTaskFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o título da pendência.")]
    [StringLength(200, ErrorMessage = "O título deve ter no máximo 200 caracteres.")]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "A descrição deve ter no máximo 4.000 caracteres.")]
    [Display(Name = "Descrição")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Selecione a área responsável.")]
    [Display(Name = "Área responsável")]
    public Guid? ResponsibleAreaId { get; set; }

    [Display(Name = "Pessoa responsável")]
    public Guid? ResponsiblePersonId { get; set; }

    [Required(ErrorMessage = "Selecione o status.")]
    [Display(Name = "Status")]
    public Guid? StatusId { get; set; }

    [Display(Name = "Categoria")]
    public Guid? CategoryId { get; set; }

    [Display(Name = "Prioridade")]
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    [DataType(DataType.Date)]
    [Display(Name = "Prazo")]
    public DateOnly? DueDate { get; set; }

    [StringLength(120, ErrorMessage = "A origem deve ter no máximo 120 caracteres.")]
    [Display(Name = "Origem")]
    public string? Origin { get; set; }

    [StringLength(4000, ErrorMessage = "As observações devem ter no máximo 4.000 caracteres.")]
    [Display(Name = "Observações")]
    public string? Notes { get; set; }

    [Display(Name = "SMUD relacionado")]
    public Guid? RelatedSmudId { get; set; }

    [Display(Name = "Chamado relacionado")]
    public Guid? RelatedSupportTicketId { get; set; }

    public long Version { get; set; }

    [ValidateNever]
    public PendingTaskFormOptions Options { get; set; } = null!;

    public PendingTaskInput ToInput() => new()
    {
        Title = Title,
        Description = Description,
        ResponsibleAreaId = ResponsibleAreaId ?? Guid.Empty,
        ResponsiblePersonId = ResponsiblePersonId,
        StatusId = StatusId ?? Guid.Empty,
        CategoryId = CategoryId,
        Priority = Priority,
        DueDate = DueDate,
        Origin = Origin,
        Notes = Notes,
        RelatedSmudId = RelatedSmudId,
        RelatedSupportTicketId = RelatedSupportTicketId,
        Version = Version,
    };

    public static PendingTaskFormViewModel FromEditor(PendingTaskEditorData editor) => new()
    {
        Id = editor.Id,
        Title = editor.Input.Title,
        Description = editor.Input.Description,
        ResponsibleAreaId = editor.Input.ResponsibleAreaId,
        ResponsiblePersonId = editor.Input.ResponsiblePersonId,
        StatusId = editor.Input.StatusId,
        CategoryId = editor.Input.CategoryId,
        Priority = editor.Input.Priority,
        DueDate = editor.Input.DueDate,
        Origin = editor.Input.Origin,
        Notes = editor.Input.Notes,
        RelatedSmudId = editor.Input.RelatedSmudId,
        RelatedSupportTicketId = editor.Input.RelatedSupportTicketId,
        Version = editor.Input.Version,
        Options = editor.Options,
    };
}
