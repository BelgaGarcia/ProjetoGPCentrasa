using System.Security.Claims;
using CentraSA.Application.Lookups;
using CentraSA.Web.ViewModels.Lookups;
using Microsoft.AspNetCore.Mvc;

namespace CentraSA.Web.Controllers;

[Route("cadastros")]
public sealed class LookupsController(ILookupService lookupService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await lookupService.GetOverviewAsync(cancellationToken));

    [HttpGet("novo")]
    public async Task<IActionResult> Create(
        LookupKind kind = LookupKind.TeamArea,
        CancellationToken cancellationToken = default)
    {
        LookupEditorData editor = await lookupService.GetCreateEditorAsync(kind, cancellationToken);
        return View(LookupFormViewModel.FromEditor(editor));
    }

    [HttpPost("novo")]
    public async Task<IActionResult> Create(
        LookupFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            LookupOperationResult result = await lookupService.CreateAsync(
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Cadastro auxiliar criado.";
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
        }

        LookupEditorData editor = await lookupService.GetCreateEditorAsync(model.Kind, cancellationToken);
        return View(LookupFormViewModel.FromEditor(editor, model));
    }

    [HttpGet("{kind}/{id:guid}/editar")]
    public async Task<IActionResult> Edit(
        LookupKind kind,
        Guid id,
        CancellationToken cancellationToken)
    {
        LookupEditorData? editor = await lookupService.GetEditEditorAsync(kind, id, cancellationToken);
        return editor is null ? NotFound() : View(LookupFormViewModel.FromEditor(editor));
    }

    [HttpPost("{kind}/{id:guid}/editar")]
    public async Task<IActionResult> Edit(
        LookupKind kind,
        Guid id,
        LookupFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id || kind != model.Kind)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            LookupOperationResult result = await lookupService.UpdateAsync(
                kind,
                id,
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Cadastro auxiliar atualizado.";
                return RedirectToAction(nameof(Index));
            }

            if (result.Status == LookupOperationStatus.NotFound)
            {
                return NotFound();
            }

            AddErrors(result);
        }

        LookupEditorData? editor = await lookupService.GetEditEditorAsync(kind, id, cancellationToken);
        return editor is null ? NotFound() : View(LookupFormViewModel.FromEditor(editor, model));
    }

    [HttpPost("{kind}/{id:guid}/ativo")]
    public async Task<IActionResult> ToggleActive(
        LookupKind kind,
        Guid id,
        bool activate,
        CancellationToken cancellationToken)
    {
        LookupOperationResult result = await lookupService.ToggleActiveAsync(
            kind,
            id,
            activate,
            GetActorUserId(),
            cancellationToken);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = activate ? "Cadastro ativado." : "Cadastro desativado.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Errors is { Count: > 0 }
                ? result.Errors[0]
                : "Não foi possível alterar o cadastro.";
        }

        return RedirectToAction(nameof(Index));
    }

    private Guid GetActorUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out Guid id)
            ? id
            : throw new InvalidOperationException("O usuário autenticado não possui um identificador válido.");
    }

    private void AddErrors(LookupOperationResult result)
    {
        foreach (string error in result.Errors ?? ["Revise os dados informados."])
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }
}
