namespace TomeRaider.Domain;

public sealed class Work
{
    public Work(
        Guid id,
        string title,
        string? author,
        string? sourceUrl,
        string? ao3WorkId,
        ReadingStatus readingStatus = ReadingStatus.Unknown,
        string? personalNote = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A work must have an internal identity.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A work must have a title.", nameof(title));
        }

        Id = id;
        Title = title;
        Author = author;
        SourceUrl = sourceUrl;
        Ao3WorkId = ao3WorkId;
        ReadingStatus = readingStatus;
        PersonalNote = personalNote;
    }

    public Guid Id { get; }

    public string Title { get; }

    public string? Author { get; }

    public string? SourceUrl { get; }

    public string? Ao3WorkId { get; }

    public ReadingStatus ReadingStatus { get; private set; }

    public string? PersonalNote { get; private set; }

    public void RecordReading(ReadingStatus status, string? personalNote)
    {
        ReadingStatus = status;
        PersonalNote = personalNote;
    }
}
