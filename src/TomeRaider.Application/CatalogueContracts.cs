using TomeRaider.Domain;

namespace TomeRaider.Application;

public sealed record CatalogueWorkSummary(
    Guid Id,
    string Title,
    string? Author,
    string? SourceUrl,
    string? Ao3WorkId,
    ReadingStatus ReadingStatus,
    string? PersonalNote,
    int? WordCount,
    bool? IsComplete,
    int? DownloadedChapterCount,
    int? TotalChapterCount,
    int CopyCount)
{
    public string ReadingStatusText => ReadingStatus.ToString();
}

public sealed record CatalogueCopy(
    Guid Id,
    string Reference,
    string DisplayName);

public sealed record CatalogueWorkDetails(
    CatalogueWorkSummary Summary,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<CategorizedTag> CategorizedTags,
    string? AuthorNotes,
    int? DownloadedChapterCount,
    int? TotalChapterCount,
    IReadOnlyList<CatalogueCopy> Copies);

public sealed record ReadingRecord(ReadingStatus Status, string? PersonalNote);
