using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TomeRaider.Application;

namespace TomeRaider.Infrastructure;

public sealed class EpubMetadataExtractor : IEpubMetadataExtractor
{
    private static readonly XNamespace ContainerNamespace = "urn:oasis:names:tc:opendocument:xmlns:container";
    private static readonly XNamespace DublinCoreNamespace = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace PackageNamespace = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace XhtmlNamespace = "http://www.w3.org/1999/xhtml";
    private static readonly Regex Ao3WorkUrlPattern = new(
        @"https?://(?:www\.)?archiveofourown\.org/works/(\d+)(?:[/?#\s]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WordCountPattern = new(
        @"\b(?:word\s+count|words?)\s*:\s*([\d,]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex StatusPattern = new(
        @"\bstatus\s*:\s*(complete|unfinished|in[\s-]?progress|work\s+in\s+progress)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CompletedDatePattern = new(
        @"\bcompleted\s*:\s*\d{4}-\d{2}-\d{2}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex OriginalPostedMessagePattern = new(
        @"\b(?:originally\s+posted|posted\s+originally)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ChapterCountPattern = new(
        @"\bchapters?\s*:\s*(\d+)\s*/\s*(\d+|\?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Task<EpubMetadata> ExtractAsync(
        Stream epub,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Extract(epub, cancellationToken));
    }

    private static EpubMetadata Extract(Stream epub, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(epub, ZipArchiveMode.Read, leaveOpen: true);
        var containerEntry = archive.GetEntry("META-INF/container.xml")
            ?? throw new InvalidDataException("The EPUB is missing META-INF/container.xml.");
        var container = LoadXml(containerEntry, cancellationToken);
        var packagePath = container
            .Descendants(ContainerNamespace + "rootfile")
            .Select(rootFile => (string?)rootFile.Attribute("full-path"))
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        if (packagePath is null)
        {
            throw new InvalidDataException("The EPUB does not declare a package document.");
        }

        var packageEntry = archive.GetEntry(packagePath)
            ?? throw new InvalidDataException($"The EPUB package document '{packagePath}' was not found.");
        var package = LoadXml(packageEntry, cancellationToken);
        var metadata = package.Root?.Element(PackageNamespace + "metadata")
            ?? throw new InvalidDataException("The EPUB package document is missing metadata.");

        var title = metadata.Element(DublinCoreNamespace + "title")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidDataException("The EPUB metadata is missing a title.");
        }

        var author = metadata.Element(DublinCoreNamespace + "creator")?.Value.Trim();
        var description = metadata.Element(DublinCoreNamespace + "description")?.Value.Trim();
        var packageSourceUrl = metadata.Elements(DublinCoreNamespace + "identifier")
            .Select(identifier => identifier.Value)
            .Concat(
                metadata.Elements(PackageNamespace + "meta")
                    .Where(meta => IsSourceMetadata(meta))
                    .Select(meta => (string?)meta.Attribute("content") ?? meta.Value))
            .Select(FindAo3WorkUrl)
            .FirstOrDefault(url => url is not null);
        var spineDocuments = LoadSpineDocuments(archive, package, packagePath, cancellationToken).ToArray();
        var prefaceSourceUrl = spineDocuments
            .Select(FindAo3WorkUrlFromPreface)
            .FirstOrDefault(url => url is not null);
        var sourceUrl = prefaceSourceUrl ?? packageSourceUrl;
        var ao3WorkId = FindAo3WorkId(sourceUrl);
        var tags = metadata
            .Elements(DublinCoreNamespace + "subject")
            .Select(subject => subject.Value.Trim())
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var prefaceStatsText = string.Join(
            " ",
            spineDocuments
                .Select(GetPrefaceStatsText)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        var wordCount = FindIntegerMeta(metadata, "word-count", "ao3:word-count", "words")
            ?? FindPrefaceWordCount(prefaceStatsText);
        var (downloadedChapterCount, totalChapterCount) = FindPrefaceChapterCounts(prefaceStatsText);
        var status = FindMeta(metadata, "status", "ao3:status", "completion-status")
            ?? FindPrefaceStatus(prefaceStatsText);
        bool? isComplete = null;
        if (status is not null)
        {
            var normalizedStatus = status.Replace(' ', '-').ToLowerInvariant();
            isComplete = normalizedStatus.Equals("complete", StringComparison.Ordinal)
                ? true
                : normalizedStatus.Equals("unfinished", StringComparison.Ordinal)
                    || normalizedStatus.Equals("in-progress", StringComparison.Ordinal)
                    || normalizedStatus.Equals("work-in-progress", StringComparison.Ordinal)
                    ? false
                    : null;
        }

        var categorizedTags = spineDocuments
            .SelectMany(GetCategorizedTags)
            .Concat(tags.Select(tag => new CategorizedTag("Subject", tag, null)))
            .Distinct()
            .ToArray();
        var prefaceDescription = spineDocuments
            .Select(FindPrefaceDescription)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var authorNotes = spineDocuments
            .Select(FindAuthorNotes)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return new EpubMetadata(
            title,
            string.IsNullOrWhiteSpace(author) ? null : author,
            string.IsNullOrWhiteSpace(description) ? prefaceDescription : description,
            sourceUrl,
            ao3WorkId,
            tags,
            wordCount,
            isComplete)
        {
            CategorizedTags = categorizedTags,
            AuthorNotes = authorNotes,
            DownloadedChapterCount = downloadedChapterCount,
            TotalChapterCount = totalChapterCount
        };
    }

    private static XDocument LoadXml(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        using var stream = entry.Open();
        cancellationToken.ThrowIfCancellationRequested();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static IEnumerable<XDocument> LoadSpineDocuments(
        ZipArchive archive,
        XDocument package,
        string packagePath,
        CancellationToken cancellationToken)
    {
        var spineIds = package
            .Root?
            .Element(PackageNamespace + "spine")?
            .Elements(PackageNamespace + "itemref")
            .Select(itemref => (string?)itemref.Attribute("idref"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToArray() ?? [];

        var manifestItems = package
            .Root?
            .Element(PackageNamespace + "manifest")?
            .Elements(PackageNamespace + "item")
            .ToArray();
        if (manifestItems is null)
        {
            yield break;
        }

        foreach (var spineId in spineIds)
        {
            var document = LoadSpineDocument(
                archive,
                manifestItems,
                spineId,
                packagePath,
                cancellationToken);
            if (document is not null)
            {
                yield return document;
            }
        }
    }

    private static XDocument? LoadSpineDocument(
        ZipArchive archive,
        IReadOnlyList<XElement> manifestItems,
        string spineId,
        string packagePath,
        CancellationToken cancellationToken)
    {
        var item = manifestItems.FirstOrDefault(candidate =>
            string.Equals((string?)candidate.Attribute("id"), spineId, StringComparison.Ordinal));
        if (item is null)
        {
            return null;
        }

        var visitedIds = new HashSet<string>(StringComparer.Ordinal);
        while (item is not null)
        {
            var itemId = (string?)item.Attribute("id");
            if (string.IsNullOrWhiteSpace(itemId) || !visitedIds.Add(itemId))
            {
                return null;
            }

            if (string.Equals(
                    (string?)item.Attribute("media-type"),
                    "application/xhtml+xml",
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var fallbackId = (string?)item.Attribute("fallback");
            item = string.IsNullOrWhiteSpace(fallbackId)
                ? null
                : manifestItems.FirstOrDefault(candidate =>
                    string.Equals((string?)candidate.Attribute("id"), fallbackId, StringComparison.Ordinal));
        }

        var href = (string?)item?.Attribute("href");
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var entry = archive.GetEntry(ResolveArchivePath(packagePath, href));
        return entry is null ? null : LoadXml(entry, cancellationToken);
    }

    private static string ResolveArchivePath(string packagePath, string href)
    {
        var cleanHref = Uri.UnescapeDataString(href.Split(['#', '?'], 2)[0]).Replace('\\', '/');
        var packageDirectory = packagePath.Replace('\\', '/');
        var separator = packageDirectory.LastIndexOf('/');
        packageDirectory = separator < 0 ? string.Empty : packageDirectory[..separator];
        var parts = $"{packageDirectory}/{cleanHref}"
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var resolved = new List<string>();
        foreach (var part in parts)
        {
            if (part == ".")
            {
                continue;
            }

            if (part == ".." && resolved.Count > 0)
            {
                resolved.RemoveAt(resolved.Count - 1);
                continue;
            }

            resolved.Add(part);
        }

        return string.Join('/', resolved);
    }

    private static bool IsSourceMetadata(XElement meta)
    {
        var name = (string?)meta.Attribute("name") ?? (string?)meta.Attribute("property");
        return name is not null
            && new[] { "source", "source-url", "canonical", "ao3:work-url" }
                .Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static string? FindAo3WorkUrlFromPreface(XDocument? preface)
    {
        var prefaceElement = FindAo3Preface(preface);
        var message = prefaceElement?
            .Descendants(XhtmlNamespace + "p")
            .Where(paragraph => HasClass(paragraph, "message"))
            .FirstOrDefault(paragraph => OriginalPostedMessagePattern.IsMatch(paragraph.Value));
        return message?
            .Descendants(XhtmlNamespace + "a")
            .Select(anchor => (string?)anchor.Attribute("href"))
            .Select(FindAo3WorkUrl)
            .FirstOrDefault(url => url is not null);
    }

    private static IEnumerable<CategorizedTag> GetCategorizedTags(XDocument preface)
    {
        var prefaceElement = FindAo3Preface(preface);
        if (prefaceElement is null)
        {
            yield break;
        }

        foreach (var definitionList in prefaceElement
                     .Descendants(XhtmlNamespace + "dl")
                     .Where(element => HasClass(element, "tags")))
        {
            var children = definitionList.Elements().ToArray();
            for (var index = 0; index < children.Length; index++)
            {
                var term = children[index];
                if (term.Name != XhtmlNamespace + "dt")
                {
                    continue;
                }

                var category = GetElementText(term).Trim().TrimEnd(':');
                var definition = children
                    .Skip(index + 1)
                    .FirstOrDefault(element => element.Name == XhtmlNamespace + "dd");
                if (definition is null || string.Equals(category, "Stats", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var linkedTags = definition
                    .Descendants(XhtmlNamespace + "a")
                    .Select(anchor => new CategorizedTag(
                        category,
                        GetElementText(anchor),
                        (string?)anchor.Attribute("href")))
                    .Where(tag => !string.IsNullOrWhiteSpace(tag.Value));
                foreach (var tag in linkedTags)
                {
                    yield return tag;
                }

                if (!definition.Descendants(XhtmlNamespace + "a").Any())
                {
                    var value = GetElementText(definition);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return new CategorizedTag(category, value, null);
                    }
                }
            }
        }
    }

    private static string? FindPrefaceDescription(XDocument preface)
    {
        var summary = FindElementById(preface, "summary");
        return summary is null ? null : GetElementText(summary);
    }

    private static string? FindAuthorNotes(XDocument preface)
    {
        var notes = preface
            .Descendants(XhtmlNamespace + "div")
            .FirstOrDefault(element =>
                string.Equals((string?)element.Attribute("id"), "notes", StringComparison.OrdinalIgnoreCase)
                || string.Equals((string?)element.Attribute("id"), "author-notes", StringComparison.OrdinalIgnoreCase)
                || HasClass(element, "notes"));
        return notes is null ? null : GetElementText(notes);
    }

    private static XElement? FindElementById(XDocument document, string id)
    {
        return document
            .Descendants()
            .FirstOrDefault(element => string.Equals(
                (string?)element.Attribute("id"),
                id,
                StringComparison.OrdinalIgnoreCase));
    }

    private static XElement? FindAo3Preface(XDocument? document)
    {
        return document?
            .Descendants(XhtmlNamespace + "div")
            .FirstOrDefault(element => string.Equals(
                (string?)element.Attribute("id"),
                "preface",
                StringComparison.Ordinal));
    }

    private static string? GetPrefaceStatsText(XDocument? preface)
    {
        var statsValue = FindAo3Preface(preface)?
            .Descendants(XhtmlNamespace + "dl")
            .Where(element => HasClass(element, "tags"))
            .SelectMany(element => element.Elements(XhtmlNamespace + "dt"))
            .Where(element => string.Equals(
                element.Value.Trim().TrimEnd(':'),
                "Stats",
                StringComparison.OrdinalIgnoreCase))
            .Select(element => element.ElementsAfterSelf(XhtmlNamespace + "dd").FirstOrDefault())
            .FirstOrDefault(value => value is not null);
        return statsValue is null
            ? null
            : GetElementText(statsValue);
    }

    private static bool HasClass(XElement element, string className)
    {
        return element.Attributes("class").Any(attribute =>
            attribute.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(className, StringComparer.OrdinalIgnoreCase));
    }

    private static string GetElementText(XElement element)
    {
        return string.Join(
            " ",
            element
                .DescendantNodes()
                .OfType<XText>()
                .Select(node => node.Value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? FindAo3WorkUrl(string? value)
    {
        var match = value is null ? null : Ao3WorkUrlPattern.Match(value);
        return match?.Success == true ? match.Value : null;
    }

    private static string? FindAo3WorkId(string? sourceUrl)
    {
        var match = sourceUrl is null ? null : Ao3WorkUrlPattern.Match(sourceUrl);
        return match?.Success == true ? match.Groups[1].Value : null;
    }

    private static int? FindPrefaceWordCount(string? prefaceText)
    {
        var match = prefaceText is null ? null : WordCountPattern.Match(prefaceText);
        if (match?.Success != true)
        {
            return null;
        }

        return int.TryParse(
            match.Groups[1].Value.Replace(",", string.Empty, StringComparison.Ordinal),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var wordCount)
            ? wordCount
            : null;
    }

    private static (int? Downloaded, int? Total) FindPrefaceChapterCounts(string? prefaceText)
    {
        var match = prefaceText is null ? null : ChapterCountPattern.Match(prefaceText);
        if (match?.Success != true)
        {
            return (null, null);
        }

        var downloaded = int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var downloadedCount)
            ? downloadedCount
            : (int?)null;
        var total = int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var totalCount)
            ? totalCount
            : (int?)null;
        return (downloaded, total);
    }

    private static string? FindPrefaceStatus(string? prefaceText)
    {
        var match = prefaceText is null ? null : StatusPattern.Match(prefaceText);
        return match?.Success == true
            ? match.Groups[1].Value
            : prefaceText is not null && CompletedDatePattern.IsMatch(prefaceText)
                ? "Complete"
                : null;
    }

    private static string? FindMeta(XElement metadata, params string[] names)
    {
        return metadata
            .Elements(PackageNamespace + "meta")
            .Select(meta => new
            {
                Name = (string?)meta.Attribute("name") ?? (string?)meta.Attribute("property"),
                Value = (string?)meta.Attribute("content") ?? meta.Value
            })
            .Where(meta => meta.Name is not null && names.Contains(meta.Name, StringComparer.OrdinalIgnoreCase))
            .Select(meta => meta.Value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static int? FindIntegerMeta(XElement metadata, params string[] names)
    {
        var value = FindMeta(metadata, names);
        return int.TryParse(value, out var result) ? result : null;
    }
}
