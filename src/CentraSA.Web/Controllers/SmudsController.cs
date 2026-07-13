using System.Security.Claims;
using CentraSA.Application.Smuds;
using CentraSA.Web.ViewModels.Smuds;
using Microsoft.AspNetCore.Mvc;

namespace CentraSA.Web.Controllers;

[Route("smuds")]
public sealed class SmudsController(
    ISmudService smudService,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] SmudIndexViewModel model,
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
        SmudEditorData editor = await smudService.GetCreateEditorAsync(cancellationToken);
        return View(SmudFormViewModel.FromEditor(editor));
    }

    [HttpPost("novo")]
    public async Task<IActionResult> Create(
        SmudFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            SmudOperationResult result = await smudService.CreateAsync(
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "SMUD criado com sucesso.";
                return RedirectToAction(nameof(Details), new { id = result.Id });
            }

            AddOperationErrors(result);
        }

        SmudEditorData editor = await smudService.GetCreateEditorAsync(cancellationToken);
        model.Options = editor.Options;
        return View(model);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        SmudDetailsData? data = await smudService.GetDetailsAsync(
            id,
            includeArchived: true,
            cancellationToken);
        return data is null ? NotFound() : View(data);
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        SmudEditorData? editor = await smudService.GetEditEditorAsync(id, cancellationToken);
        return editor is null ? NotFound() : View(SmudFormViewModel.FromEditor(editor));
    }

    [HttpPost("{id:guid}/editar")]
    public async Task<IActionResult> Edit(
        Guid id,
        SmudFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            SmudOperationResult result = await smudService.UpdateAsync(
                id,
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "SMUD atualizado com sucesso.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (result.Status == SmudOperationStatus.NotFound)
            {
                return NotFound();
            }

            AddOperationErrors(result);
        }

        SmudEditorData? editor = await smudService.GetEditEditorAsync(id, cancellationToken);
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
        SmudOperationResult result = await smudService.ArchiveAsync(
            id,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "SMUD arquivado.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/restaurar")]
    public async Task<IActionResult> Restore(
        Guid id,
        long version,
        CancellationToken cancellationToken)
    {
        SmudOperationResult result = await smudService.RestoreAsync(
            id,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "SMUD restaurado.");
        return RedirectToAction(nameof(Archived));
    }

    [HttpGet("arquivados")]
    public async Task<IActionResult> Archived(
        [FromQuery] SmudIndexViewModel model,
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
        SmudBoardData data = await smudService.GetPresentationAsync(showFinalized, cancellationToken);
        ViewData["ShowFinalized"] = showFinalized;
        return View(data);
    }

    private Task<SmudBoardData> SearchAsync(
        SmudIndexViewModel model,
        bool archivedOnly,
        CancellationToken cancellationToken) =>
        smudService.SearchAsync(
            new SmudSearch(
                model.Search,
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

    private void SetOperationMessage(SmudOperationResult result, string successMessage)
    {
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = successMessage;
            return;
        }

        TempData["ErrorMessage"] = result.Status switch
        {
            SmudOperationStatus.NotFound => "O SMUD não foi encontrado.",
            SmudOperationStatus.Conflict => "O SMUD foi alterado em outra aba. Recarregue a página e tente novamente.",
            SmudOperationStatus.DuplicateCode => "Já existe um SMUD com esse código.",
            _ => result.Errors is { Count: > 0 } ? result.Errors[0] : "Revise os dados informados.",
        };
    }

    private void AddOperationErrors(SmudOperationResult result)
    {
        if (result.Status == SmudOperationStatus.Conflict)
        {
            ModelState.AddModelError(
                string.Empty,
                "O SMUD foi alterado em outra aba. Recarregue a página antes de salvar novamente.");
            return;
        }

        foreach (string error in result.Errors ?? [])
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }
}
