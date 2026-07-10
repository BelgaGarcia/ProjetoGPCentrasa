using System.Security.Claims;
using CentraSA.Application.PendingTasks;
using CentraSA.Web.ViewModels.PendingTasks;
using Microsoft.AspNetCore.Mvc;

namespace CentraSA.Web.Controllers;

[Route("pendencias")]
public sealed class PendingTasksController(
    IPendingTaskService pendingTaskService,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] PendingTaskIndexViewModel model,
        CancellationToken cancellationToken)
    {
        model.Data = await SearchAsync(model, archivedOnly: false, cancellationToken);
        if (Request.Headers["X-Requested-With"] == "fetch")
        {
            return PartialView("_TaskList", model);
        }

        return View(model);
    }

    [HttpPost("criacao-rapida")]
    public async Task<IActionResult> QuickCreate(
        PendingTaskQuickCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = FirstModelError();
            return RedirectToAction(nameof(Index));
        }

        PendingTaskOperationResult result = await pendingTaskService.QuickCreateAsync(
            new PendingTaskQuickInput(model.Title, model.ResponsibleAreaId, model.DueDate),
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Pendência criada e adicionada à lista.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("nova")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        PendingTaskEditorData editor = await pendingTaskService.GetCreateEditorAsync(cancellationToken);
        return View(PendingTaskFormViewModel.FromEditor(editor));
    }

    [HttpPost("nova")]
    public async Task<IActionResult> Create(
        PendingTaskFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            PendingTaskOperationResult result = await pendingTaskService.CreateAsync(
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Pendência criada com sucesso.";
                return RedirectToAction(nameof(Details), new { id = result.Id });
            }

            AddOperationErrors(result);
        }

        PendingTaskEditorData editor = await pendingTaskService.GetCreateEditorAsync(cancellationToken);
        model.Options = editor.Options;
        return View(model);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        PendingTaskDetailsData? data = await pendingTaskService.GetDetailsAsync(
            id,
            includeArchived: true,
            cancellationToken);
        return data is null ? NotFound() : View(data);
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        PendingTaskEditorData? editor = await pendingTaskService.GetEditEditorAsync(id, cancellationToken);
        return editor is null ? NotFound() : View(PendingTaskFormViewModel.FromEditor(editor));
    }

    [HttpPost("{id:guid}/editar")]
    public async Task<IActionResult> Edit(
        Guid id,
        PendingTaskFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            PendingTaskOperationResult result = await pendingTaskService.UpdateAsync(
                id,
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Pendência atualizada com sucesso.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (result.Status == PendingTaskOperationStatus.NotFound)
            {
                return NotFound();
            }

            AddOperationErrors(result);
        }

        PendingTaskEditorData? editor = await pendingTaskService.GetEditEditorAsync(id, cancellationToken);
        if (editor is null)
        {
            return NotFound();
        }

        model.Options = editor.Options;
        return View(model);
    }

    [HttpPost("{id:guid}/concluir")]
    public async Task<IActionResult> Complete(
        Guid id,
        long version,
        bool presentation,
        CancellationToken cancellationToken)
    {
        PendingTaskOperationResult result = await pendingTaskService.CompleteAsync(
            id,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Pendência concluída.");
        return presentation
            ? RedirectToAction(nameof(Presentation))
            : RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/reabrir")]
    public async Task<IActionResult> Reopen(Guid id, long version, CancellationToken cancellationToken)
    {
        PendingTaskOperationResult result = await pendingTaskService.ReopenAsync(
            id,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Pendência reaberta.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/arquivar")]
    public async Task<IActionResult> Archive(Guid id, long version, CancellationToken cancellationToken)
    {
        PendingTaskOperationResult result = await pendingTaskService.ArchiveAsync(
            id,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Pendência arquivada.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/restaurar")]
    public async Task<IActionResult> Restore(Guid id, long version, CancellationToken cancellationToken)
    {
        PendingTaskOperationResult result = await pendingTaskService.RestoreAsync(
            id,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Pendência restaurada.");
        return RedirectToAction(nameof(Archived));
    }

    [HttpPost("{id:guid}/mover")]
    public async Task<IActionResult> Move(
        Guid id,
        long version,
        PendingTaskMoveDirection direction,
        CancellationToken cancellationToken)
    {
        PendingTaskOperationResult result = await pendingTaskService.MoveAsync(
            id,
            version,
            direction,
            GetActorUserId(),
            cancellationToken);
        if (!result.Succeeded)
        {
            SetOperationMessage(result, string.Empty);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("arquivadas")]
    public async Task<IActionResult> Archived(
        [FromQuery] PendingTaskIndexViewModel model,
        CancellationToken cancellationToken)
    {
        model.HideCompleted = false;
        model.Data = await SearchAsync(model, archivedOnly: true, cancellationToken);
        if (Request.Headers["X-Requested-With"] == "fetch")
        {
            return PartialView("_TaskList", model);
        }

        return View(model);
    }

    [HttpGet("apresentacao")]
    public async Task<IActionResult> Presentation(
        bool showCompleted,
        CancellationToken cancellationToken)
    {
        PendingTaskListData data = await pendingTaskService.GetPresentationAsync(
            showCompleted,
            cancellationToken);
        ViewData["ShowCompleted"] = showCompleted;
        return View(data);
    }

    private Task<PendingTaskListData> SearchAsync(
        PendingTaskIndexViewModel model,
        bool archivedOnly,
        CancellationToken cancellationToken) =>
        pendingTaskService.SearchAsync(
            new PendingTaskSearch(
                model.Search,
                model.AreaId,
                model.PersonId,
                model.StatusId,
                model.DueFilter,
                model.HideCompleted,
                archivedOnly,
                DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
                Math.Max(1, model.Page)),
            cancellationToken);

    private Guid GetActorUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out Guid id)
            ? id
            : throw new InvalidOperationException("O usuário autenticado não possui um identificador válido.");
    }

    private void SetOperationMessage(PendingTaskOperationResult result, string successMessage)
    {
        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                TempData["SuccessMessage"] = successMessage;
            }

            return;
        }

        TempData["ErrorMessage"] = result.Status switch
        {
            PendingTaskOperationStatus.NotFound => "A pendência não foi encontrada.",
            PendingTaskOperationStatus.Conflict => "A pendência foi alterada em outra aba. Recarregue a página e tente novamente.",
            _ => result.Errors is { Count: > 0 } ? result.Errors[0] : "Revise os dados informados.",
        };
    }

    private void AddOperationErrors(PendingTaskOperationResult result)
    {
        if (result.Status == PendingTaskOperationStatus.Conflict)
        {
            ModelState.AddModelError(string.Empty, "A pendência foi alterada em outra aba. Recarregue a página antes de salvar novamente.");
            return;
        }

        foreach (string error in result.Errors ?? [])
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    private string FirstModelError() => ModelState.Values
        .SelectMany(value => value.Errors)
        .Select(error => error.ErrorMessage)
        .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
        ?? "Revise os dados informados.";
}
