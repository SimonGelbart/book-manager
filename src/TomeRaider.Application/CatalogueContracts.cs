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

public enum ReaderLaunchOutcome
{
    LaunchRequested,
    CopyNotFound,
    FileUnavailable,
    LaunchFailed
}

public sealed record ReaderLaunchResult(ReaderLaunchOutcome Outcome, string Message)
{
    public bool Succeeded => Outcome == ReaderLaunchOutcome.LaunchRequested;

    public static ReaderLaunchResult LaunchRequested(string message) =>
        new(ReaderLaunchOutcome.LaunchRequested, message);

    public static ReaderLaunchResult CopyNotFound(string message) =>
        new(ReaderLaunchOutcome.CopyNotFound, message);

    public static ReaderLaunchResult FileUnavailable(string message) =>
        new(ReaderLaunchOutcome.FileUnavailable, message);

    public static ReaderLaunchResult LaunchFailed(string message) =>
        new(ReaderLaunchOutcome.LaunchFailed, message);
}

public sealed record ReadingRecord(ReadingStatus Status, string? PersonalNote);
