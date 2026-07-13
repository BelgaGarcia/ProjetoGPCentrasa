using System.Security.Claims;
using CentraSA.Application.DailyMeetings;
using CentraSA.Web.ViewModels.DailyMeetings;
using Microsoft.AspNetCore.Mvc;

namespace CentraSA.Web.Controllers;

[Route("reunioes")]
public sealed class DailyMeetingsController(IDailyMeetingService meetingService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await meetingService.GetOverviewAsync(cancellationToken));

    [HttpGet("nova")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        DailyMeetingBuilderData data = await meetingService.GetCreateBuilderAsync(cancellationToken);
        return View(DailyMeetingBuilderViewModel.FromBuilder(data));
    }

    [HttpPost("nova")]
    public async Task<IActionResult> Create(
        DailyMeetingBuilderViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            DailyMeetingOperationResult result = await meetingService.CreateDraftAsync(
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Rascunho da reunião preparado.";
                return RedirectToAction(nameof(Details), new { id = result.Id });
            }

            AddOperationErrors(result);
        }

        DailyMeetingBuilderData data = await meetingService.GetCreateBuilderAsync(cancellationToken);
        return View(DailyMeetingBuilderViewModel.FromBuilder(data, model));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        DailyMeetingDetailsData? data = await meetingService.GetDetailsAsync(id, cancellationToken);
        return data is null ? NotFound() : View(data);
    }

    [HttpGet("{id:guid}/preparar")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        DailyMeetingBuilderData? data = await meetingService.GetEditBuilderAsync(id, cancellationToken);
        return data is null ? NotFound() : View(DailyMeetingBuilderViewModel.FromBuilder(data));
    }

    [HttpPost("{id:guid}/preparar")]
    public async Task<IActionResult> Edit(
        Guid id,
        DailyMeetingBuilderViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            DailyMeetingOperationResult result = await meetingService.UpdateDraftAsync(
                id,
                model.ToInput(),
                GetActorUserId(),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Roteiro da reunião atualizado.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (result.Status == DailyMeetingOperationStatus.NotFound)
            {
                return NotFound();
            }

            AddOperationErrors(result);
        }

        DailyMeetingBuilderData? data = await meetingService.GetEditBuilderAsync(id, cancellationToken);
        return data is null
            ? NotFound()
            : View(DailyMeetingBuilderViewModel.FromBuilder(data, model));
    }

    [HttpGet("{id:guid}/apresentacao")]
    public async Task<IActionResult> Presentation(Guid id, CancellationToken cancellationToken)
    {
        DailyMeetingDetailsData? data = await meetingService.GetDetailsAsync(id, cancellationToken);
        return data is null ? NotFound() : View(data);
    }

    [HttpPost("{id:guid}/itens/{itemId:guid}/apresentado")]
    public async Task<IActionResult> MarkPresented(
        Guid id,
        Guid itemId,
        long version,
        bool wasPresented,
        bool presentation,
        CancellationToken cancellationToken)
    {
        DailyMeetingOperationResult result = await meetingService.MarkPresentedAsync(
            id,
            itemId,
            version,
            wasPresented,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, wasPresented ? "Item marcado como apresentado." : "Marcação removida.");
        return RedirectToMeeting(id, presentation);
    }

    [HttpPost("{id:guid}/itens/{itemId:guid}/notas")]
    public async Task<IActionResult> UpdateNotes(
        Guid id,
        Guid itemId,
        long version,
        string? notes,
        bool presentation,
        CancellationToken cancellationToken)
    {
        DailyMeetingOperationResult result = await meetingService.UpdateItemNotesAsync(
            id,
            itemId,
            version,
            notes,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Notas do item atualizadas.");
        return RedirectToMeeting(id, presentation);
    }

    [HttpPost("{id:guid}/itens/{itemId:guid}/concluir-original")]
    public async Task<IActionResult> CompleteOriginal(
        Guid id,
        Guid itemId,
        long version,
        bool presentation,
        CancellationToken cancellationToken)
    {
        DailyMeetingOperationResult result = await meetingService.CompleteOriginalAsync(
            id,
            itemId,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Item original concluído e snapshot da reunião preservado.");
        return RedirectToMeeting(id, presentation);
    }

    [HttpPost("{id:guid}/finalizar")]
    public async Task<IActionResult> Finish(
        Guid id,
        long version,
        CancellationToken cancellationToken)
    {
        DailyMeetingOperationResult result = await meetingService.FinishAsync(
            id,
            version,
            GetActorUserId(),
            cancellationToken);
        SetOperationMessage(result, "Reunião finalizada e disponível para consulta.");
        return RedirectToAction(nameof(Details), new { id });
    }

    private RedirectToActionResult RedirectToMeeting(Guid id, bool presentation) =>
        RedirectToAction(presentation ? nameof(Presentation) : nameof(Details), new { id });

    private Guid GetActorUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out Guid id)
            ? id
            : throw new InvalidOperationException("O usuário autenticado não possui um identificador válido.");
    }

    private void SetOperationMessage(DailyMeetingOperationResult result, string successMessage)
    {
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = successMessage;
            return;
        }

        TempData["ErrorMessage"] = result.Errors is { Count: > 0 }
            ? result.Errors[0]
            : result.Status == DailyMeetingOperationStatus.NotFound
                ? "A reunião ou o item não foi encontrado."
                : "Não foi possível atualizar a reunião.";
    }

    private void AddOperationErrors(DailyMeetingOperationResult result)
    {
        foreach (string error in result.Errors ?? ["Revise a preparação da reunião."])
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }
}
