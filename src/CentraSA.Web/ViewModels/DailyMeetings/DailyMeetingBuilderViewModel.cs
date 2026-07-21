using System.ComponentModel.DataAnnotations;
using CentraSA.Application.DailyMeetings;
using CentraSA.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CentraSA.Web.ViewModels.DailyMeetings;

public sealed class DailyMeetingBuilderViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe a data da reunião.")]
    [DataType(DataType.Date)]
    [Display(Name = "Data da reunião")]
    public DateOnly MeetingDate { get; set; }

    [StringLength(4000, ErrorMessage = "As notas gerais devem ter no máximo 4.000 caracteres.")]
    [Display(Name = "Notas gerais")]
    public string? GeneralNotes { get; set; }

    public long Version { get; set; }

    public List<DailyMeetingBuilderItemViewModel> Items { get; set; } = [];

    public DailyMeetingInput ToInput() => new()
    {
        MeetingDate = MeetingDate,
        GeneralNotes = GeneralNotes,
        Version = Version,
        Items = Items.Select(item => item.ToInput()).ToList(),
    };

    public static DailyMeetingBuilderViewModel FromBuilder(
        DailyMeetingBuilderData data,
        DailyMeetingBuilderViewModel? posted = null)
    {
        Dictionary<string, DailyMeetingBuilderItemViewModel> postedItems = posted?.Items
            .GroupBy(item => SourceKey(item.SourceType, item.SourceId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)
            ?? new Dictionary<string, DailyMeetingBuilderItemViewModel>(StringComparer.Ordinal);

        return new DailyMeetingBuilderViewModel
        {
            Id = data.Id,
            MeetingDate = posted?.MeetingDate ?? data.MeetingDate,
            GeneralNotes = posted?.GeneralNotes ?? data.GeneralNotes,
            Version = data.Version,
            Items = data.Rows.Select(row =>
            {
                string key = SourceKey(row.SourceType, row.SourceId);
                postedItems.TryGetValue(key, out DailyMeetingBuilderItemViewModel? previous);
                return DailyMeetingBuilderItemViewModel.FromRow(row, previous);
            }).ToList(),
        };
    }

    private static string SourceKey(TrackedEntityType sourceType, Guid sourceId) =>
        $"{sourceType}:{sourceId:N}";
}

public sealed class DailyMeetingBuilderItemViewModel
{
    public Guid? ItemId { get; set; }

    public TrackedEntityType SourceType { get; set; }

    public Guid SourceId { get; set; }

    [ValidateNever]
    public string SourceLabel { get; set; } = string.Empty;

    [ValidateNever]
    public string Title { get; set; } = string.Empty;

    [ValidateNever]
    public string Status { get; set; } = string.Empty;

    [ValidateNever]
    public DateOnly? DueDate { get; set; }

    [ValidateNever]
    public string? Responsible { get; set; }

    public bool Selected { get; set; }

    [ValidateNever]
    public MeetingSection RecommendedSection { get; set; }

    public MeetingSection Section { get; set; }

    [Range(0, 100000, ErrorMessage = "A ordem deve estar entre 0 e 100.000.")]
    public int SortOrder { get; set; }

    [StringLength(2000, ErrorMessage = "As notas do item devem ter no máximo 2.000 caracteres.")]
    public string? PresentationNotes { get; set; }

    [ValidateNever]
    public string SuggestionReason { get; set; } = string.Empty;

    public DailyMeetingSelectionInput ToInput() => new()
    {
        ItemId = ItemId,
        SourceType = SourceType,
        SourceId = SourceId,
        Selected = Selected,
        Section = Section,
        SortOrder = SortOrder,
        PresentationNotes = PresentationNotes,
    };

    public static DailyMeetingBuilderItemViewModel FromRow(
        DailyMeetingBuilderRow row,
        DailyMeetingBuilderItemViewModel? posted = null) => new()
        {
            ItemId = row.ItemId,
            SourceType = row.SourceType,
            SourceId = row.SourceId,
            SourceLabel = row.SourceLabel,
            Title = row.Title,
            Status = row.Status,
            DueDate = row.DueDate,
            Responsible = row.Responsible,
            Selected = posted?.Selected ?? row.Selected,
            RecommendedSection = row.RecommendedSection,
            Section = posted?.Section ?? row.Section,
            SortOrder = posted?.SortOrder ?? row.SortOrder,
            PresentationNotes = posted?.PresentationNotes ?? row.PresentationNotes,
            SuggestionReason = row.SuggestionReason,
        };
}
