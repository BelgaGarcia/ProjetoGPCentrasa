using System.ComponentModel.DataAnnotations;
using CentraSA.Application.SupportTickets;
using CentraSA.Domain.Enums;

namespace CentraSA.Web.ViewModels.SupportTickets;

public sealed class SupportTicketFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o número do chamado.")]
    [StringLength(30, ErrorMessage = "O número deve ter no máximo 30 caracteres.")]
    [Display(Name = "Número")]
    public string Number { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o título do chamado.")]
    [StringLength(200, ErrorMessage = "O título deve ter no máximo 200 caracteres.")]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "A descrição deve ter no máximo 4.000 caracteres.")]
    [Display(Name = "Descrição")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Selecione a categoria.")]
    [Display(Name = "Categoria")]
    public Guid CategoryId { get; set; }

    [Required(ErrorMessage = "Selecione a equipe responsável.")]
    [Display(Name = "Equipe responsável")]
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
    public DateOnly OpenedOn { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Prazo")]
    public DateOnly? DueDate { get; set; }

    [StringLength(1000, ErrorMessage = "A ação pendente deve ter no máximo 1.000 caracteres.")]
    [Display(Name = "Ação pendente")]
    public string? PendingAction { get; set; }

    [StringLength(4000, ErrorMessage = "As observações devem ter no máximo 4.000 caracteres.")]
    [Display(Name = "Observações")]
    public string? Notes { get; set; }

    public long Version { get; set; }

    public SupportTicketFormOptions Options { get; set; } = null!;

    public SupportTicketInput ToInput() => new()
    {
        Number = Number,
        Title = Title,
        Description = Description,
        CategoryId = CategoryId,
        ResponsibleAreaId = ResponsibleAreaId,
        ResponsiblePersonId = ResponsiblePersonId,
        StatusId = StatusId,
        Priority = Priority,
        OpenedOn = OpenedOn,
        DueDate = DueDate,
        PendingAction = PendingAction,
        Notes = Notes,
        Version = Version,
    };

    public static SupportTicketFormViewModel FromEditor(SupportTicketEditorData editor) => new()
    {
        Id = editor.Id,
        Number = editor.Input.Number,
        Title = editor.Input.Title,
        Description = editor.Input.Description,
        CategoryId = editor.Input.CategoryId,
        ResponsibleAreaId = editor.Input.ResponsibleAreaId,
        ResponsiblePersonId = editor.Input.ResponsiblePersonId,
        StatusId = editor.Input.StatusId,
        Priority = editor.Input.Priority,
        OpenedOn = editor.Input.OpenedOn,
        DueDate = editor.Input.DueDate,
        PendingAction = editor.Input.PendingAction,
        Notes = editor.Input.Notes,
        Version = editor.Input.Version,
        Options = editor.Options,
    };
}
