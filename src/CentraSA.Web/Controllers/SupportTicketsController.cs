using System.Security.Claims;
using CentraSA.Application.SupportTickets;
using CentraSA.Web.ViewModels.SupportTickets;
using Microsoft.AspNetCore.Mvc;

namespace CentraSA.Web.Controllers;

[Route("chamados")]
public sealed class SupportTicketsController(
    ISupportTicketService ticketService,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] SupportTicketIndexViewModel model,
        CancellationToken cancellationToken)
    {
        model.Data = await SearchAsync(model, archivedOnly: false, cancellationToken);
        if (Request.Headers["X-Requested-With"] == "fetch")
        {
            return PartialView("_Board", model);
        }

        return View(model);
    }

    [HttpGet("novo")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        SupportTicketEditorData editor = await ticketService.GetCreateEditorAsync(cancellationToken);
        return View(SupportTicketFormViewModel.FromEditor(editor));
    }

    [HttpPost("novo")]
    public async Task<IActionResult> Create(
        SupportTicketFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            SupportTicketOperationResult result = await ticketService.CreateAsync(
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Chamado criado com sucesso.";
                return RedirectToAction(nameof(Details), new { id = result.Id });
            }

            AddOperationErrors(result);
        }

        SupportTicketEditorData editor = await ticketService.GetCreateEditorAsync(cancellationToken);
        model.Options = editor.Options;
        return View(model);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        SupportTicketDetailsData? data = await ticketService.GetDetailsAsync(
            id,
            includeArchived: true,
            cancellationToken);
        return data is null ? NotFound() : View(data);
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        SupportTicketEditorData? editor = await ticketService.GetEditEditorAsync(id, cancellationToken);
        return editor is null ? NotFound() : View(SupportTicketFormViewModel.FromEditor(editor));
    }

    [HttpPost("{id:guid}/editar")]
    public async Task<IActionResult> Edit(
        Guid id,
        SupportTicketFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            SupportTicketOperationResult result = await ticketService.UpdateAsync(
                id,
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Chamado atualizado com sucesso.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (result.Status == SupportTicketOperationStatus.NotFound)
            {
                return NotFound();
            }

            AddOperationErrors(result);
        }

        SupportTicketEditorData? editor = await ticketService.GetEditEditorAsync(id, cancellationToken);
        if (editor is null)
        {
            return NotFound();
        }

        model.Options = editor.Options;
        return View(model);
    }

    [HttpPost("{id:guid}/arquivar")]
    public async Task<IActionResult> Archive(
        Guid id,
        long version,
        CancellationToken cancellationToken)
    {
        SupportTicketOperationResult result = await ticketService.ArchiveAsync(
            id,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Chamado arquivado.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/restaurar")]
    public async Task<IActionResult> Restore(
        Guid id,
        long version,
        CancellationToken cancellationToken)
    {
        SupportTicketOperationResult result = await ticketService.RestoreAsync(
            id,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Chamado restaurado.");
        return RedirectToAction(nameof(Archived));
    }

    [HttpGet("arquivados")]
    public async Task<IActionResult> Archived(
        [FromQuery] SupportTicketIndexViewModel model,
        CancellationToken cancellationToken)
    {
        model.HideFinalized = false;
        model.Data = await SearchAsync(model, archivedOnly: true, cancellationToken);
        if (Request.Headers["X-Requested-With"] == "fetch")
        {
            return PartialView("_Board", model);
        }

        return View(model);
    }

    [HttpGet("apresentacao")]
    public async Task<IActionResult> Presentation(
        bool showFinalized,
        CancellationToken cancellationToken)
    {
        SupportTicketBoardData data = await ticketService.GetPresentationAsync(showFinalized, cancellationToken);
        ViewData["ShowFinalized"] = showFinalized;
        return View(data);
    }

    private Task<SupportTicketBoardData> SearchAsync(
        SupportTicketIndexViewModel model,
        bool archivedOnly,
        CancellationToken cancellationToken) =>
        ticketService.SearchAsync(
            new SupportTicketSearch(
                model.Search,
                model.CategoryId,
                model.AreaId,
                model.PersonId,
                model.StatusId,
                model.DueFilter,
                model.ActionRequiredOnly,
                model.HideFinalized,
                archivedOnly,
                DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime)),
            cancellationToken);

    private Guid GetActorUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out Guid id)
            ? id
            : throw new InvalidOperationException("O usuário autenticado não possui um identificador válido.");
    }

    private void SetOperationMessage(SupportTicketOperationResult result, string successMessage)
    {
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = successMessage;
            return;
        }

        TempData["ErrorMessage"] = result.Status switch
        {
            SupportTicketOperationStatus.NotFound => "O chamado não foi encontrado.",
            SupportTicketOperationStatus.Conflict => "O chamado foi alterado em outra aba. Recarregue a página e tente novamente.",
            SupportTicketOperationStatus.DuplicateNumber => "Já existe um chamado com esse número.",
            _ => result.Errors is { Count: > 0 } ? result.Errors[0] : "Revise os dados informados.",
        };
    }

    private void AddOperationErrors(SupportTicketOperationResult result)
    {
        if (result.Status == SupportTicketOperationStatus.Conflict)
        {
            ModelState.AddModelError(
                string.Empty,
                "O chamado foi alterado em outra aba. Recarregue a página antes de salvar novamente.");
            return;
        }

        foreach (string error in result.Errors ?? [])
        {
            string key = error switch
            {
                "Informe o número do chamado." => nameof(SupportTicketFormViewModel.Number),
                "O número do chamado deve conter somente dígitos." => nameof(SupportTicketFormViewModel.Number),
                "O número deve ter no máximo 30 caracteres." => nameof(SupportTicketFormViewModel.Number),
                "Já existe um chamado com esse número." => nameof(SupportTicketFormViewModel.Number),
                "Informe o título do chamado." => nameof(SupportTicketFormViewModel.Title),
                "O título deve ter no máximo 200 caracteres." => nameof(SupportTicketFormViewModel.Title),
                "A descrição deve ter no máximo 4.000 caracteres." => nameof(SupportTicketFormViewModel.Description),
                "Selecione uma categoria válida para chamados." => nameof(SupportTicketFormViewModel.CategoryId),
                "Selecione uma equipe responsável válida." => nameof(SupportTicketFormViewModel.ResponsibleAreaId),
                "Selecione uma pessoa responsável válida." => nameof(SupportTicketFormViewModel.ResponsiblePersonId),
                "Selecione um status válido para chamados." => nameof(SupportTicketFormViewModel.StatusId),
                "Selecione uma prioridade válida." => nameof(SupportTicketFormViewModel.Priority),
                "Informe a data de abertura." => nameof(SupportTicketFormViewModel.OpenedOn),
                "A ação pendente deve ter no máximo 1.000 caracteres." => nameof(SupportTicketFormViewModel.PendingAction),
                "As observações devem ter no máximo 4.000 caracteres." => nameof(SupportTicketFormViewModel.Notes),
                _ => string.Empty,
            };

            ModelState.AddModelError(key, error);
        }
    }
}
