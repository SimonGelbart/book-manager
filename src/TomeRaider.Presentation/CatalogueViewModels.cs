using TomeRaider.Application;

namespace TomeRaider.Presentation;

public sealed class WorkSummaryViewModel(CatalogueWorkSummary summary)
{
    public Guid Id => summary.Id;

    public string Title => summary.Title;

    public string? Author => summary.Author;

    public string ReadingStatus => summary.ReadingStatus;

    public string WordCount => summary.WordCount?.ToString("N0") ?? "Unknown";

    public CatalogueWorkSummary Summary => summary;
}

public sealed class WorkDetailsViewModel(CatalogueWorkDetails details)
{
    public string Title => details.Summary.Title;

    public string? Author => details.Summary.Author;

    public string? SourceUrl => details.Summary.SourceUrl;

    public string? PersonalNote => details.Summary.PersonalNote;

    public string Completion => details.Summary.IsComplete switch
    {
        true => "Complete",
        false => "Unfinished",
        _ => "Unknown"
    };

    public string WordCount => details.Summary.WordCount?.ToString("N0") ?? "Unknown";

    public string Chapters => details.Summary.DownloadedChapterCount is null
        ? "Unknown"
        : $"{details.Summary.DownloadedChapterCount} / {details.Summary.TotalChapterCount?.ToString() ?? "?"}";

    public string? Description => details.Description;

    public string? AuthorNotes => details.AuthorNotes;

    public IReadOnlyList<CategorizedTag> CategorizedTags => details.CategorizedTags;

    public IReadOnlyList<CatalogueCopy> Copies => details.Copies;
}
