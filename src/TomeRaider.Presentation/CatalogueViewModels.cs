using System.Net;
using System.Text.RegularExpressions;
using TomeRaider.Application;

namespace TomeRaider.Presentation;

public sealed class WorkSummaryViewModel(CatalogueWorkSummary summary)
{
    public Guid Id => summary.Id;

    public string Title => summary.Title;

    public string? Author => summary.Author;

    public string ReadingStatus => summary.ReadingStatusText;

    public string WordCount => summary.WordCount?.ToString("N0") ?? "Unknown";

    public CatalogueWorkSummary Summary => summary;
}

public sealed class WorkDetailsViewModel(CatalogueWorkDetails details)
{
    private static readonly Regex BlockElementPattern = new(
        @"</?(?:p|div|br|li|h[1-6]|blockquote)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HtmlTagPattern = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HorizontalWhitespacePattern = new(
        @"[ \t]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<TagGroupViewModel> tagGroups = details.CategorizedTags
        .GroupBy(tag => tag.Category, StringComparer.OrdinalIgnoreCase)
        .Select(group => new TagGroupViewModel(group.Key, group.Select(tag => tag.Value).ToArray()))
        .ToArray();

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

    public string? Description => ToPlainText(details.Description);

    public string? AuthorNotes => ToPlainText(details.AuthorNotes);

    public IReadOnlyList<TagGroupViewModel> TagGroups => tagGroups;

    public IReadOnlyList<CatalogueCopy> Copies => details.Copies;

    private static string? ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var decoded = WebUtility.HtmlDecode(html);
        decoded = BlockElementPattern.Replace(decoded, "\n");
        decoded = HtmlTagPattern.Replace(decoded, string.Empty);
        decoded = WebUtility.HtmlDecode(decoded);

        var lines = decoded
            .Split('\n')
            .Select(line => HorizontalWhitespacePattern.Replace(line.Trim(), " "))
            .ToArray();
        var compactLines = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            compactLines.Add(line);
        }

        return string.Join('\n', compactLines).Trim();
    }
}

public sealed class ImportReviewViewModel(ImportReview review)
{
    public Guid Id => review.Id;

    public string FileName => review.File.DisplayName;

    public string Location => review.File.Reference;

    public string Title => review.Metadata.Title;

    public string? Author => review.Metadata.Author;

    public string? SourceUrl => review.Metadata.SourceUrl;

    public string? Ao3WorkId => review.Metadata.Ao3WorkId;

    public string? Description => review.Metadata.Description;

    public string WordCount => review.Metadata.WordCount?.ToString("N0") ?? "Unknown";

    public string Chapters => review.Metadata.DownloadedChapterCount is null
        ? "Unknown"
        : $"{review.Metadata.DownloadedChapterCount} / {review.Metadata.TotalChapterCount?.ToString() ?? "?"}";

    public string Completion => review.Metadata.IsComplete switch
    {
        true => "Complete",
        false => "Unfinished",
        _ => "Unknown"
    };

    public string Tags => string.Join(", ", review.Metadata.Tags);

    public string? AuthorNotes => review.Metadata.AuthorNotes;

    public IReadOnlyList<TagGroupViewModel> TagGroups => review.Metadata.CategorizedTags
        .GroupBy(tag => tag.Category, StringComparer.OrdinalIgnoreCase)
        .Select(group => new TagGroupViewModel(group.Key, group.Select(tag => tag.Value).ToArray()))
        .ToArray();

    public string Reason => review.Reason;

    public IReadOnlyList<ImportReviewCandidateViewModel> Candidates => review.Candidates
        .Select(candidate => new ImportReviewCandidateViewModel(review.Id, candidate))
        .ToArray();
}

public sealed class ImportReviewCandidateViewModel(
    Guid reviewId,
    ImportReviewCandidate candidate)
{
    public Guid ReviewId => reviewId;

    public Guid WorkId => candidate.WorkId;

    public string Title => candidate.Title;

    public string? Author => candidate.Author;

    public string ReadingStatus => candidate.ReadingStatusText;

    public string PersonalNoteDisplay => string.IsNullOrWhiteSpace(candidate.PersonalNote)
        ? "No personal note"
        : $"Note: {candidate.PersonalNote}";

    public int CopyCount => candidate.CopyCount;
}

public sealed record TagGroupViewModel(string Category, IReadOnlyList<string> Values)
{
    public string Header => $"{Category} ({Values.Count})";
}
