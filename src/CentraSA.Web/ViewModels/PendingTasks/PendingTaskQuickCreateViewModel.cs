using System.ComponentModel.DataAnnotations;
using CentraSA.Application.PendingTasks;

namespace CentraSA.Web.ViewModels.PendingTasks;

public sealed class PendingTaskQuickCreateViewModel
{
    [Required(ErrorMessage = "Informe o título da pendência.")]
    [StringLength(200, ErrorMessage = "O título deve ter no máximo 200 caracteres.")]
    [Display(Name = "Nova pendência")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione a área responsável.")]
    [Display(Name = "Área responsável")]
    public Guid? ResponsibleAreaId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Prazo")]
    public DateOnly? DueDate { get; set; }

    public IReadOnlyList<LookupOption> Areas { get; set; } = [];
}
