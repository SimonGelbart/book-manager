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
        var preface = LoadFirstSpineDocument(archive, package, packagePath, cancellationToken);
        var prefaceSourceUrl = FindAo3WorkUrlFromPreface(preface);
        var sourceUrl = prefaceSourceUrl ?? packageSourceUrl;
        var ao3WorkId = FindAo3WorkId(sourceUrl);
        var tags = metadata
            .Elements(DublinCoreNamespace + "subject")
            .Select(subject => subject.Value.Trim())
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var prefaceText = GetPrefaceText(preface);
        var wordCount = FindIntegerMeta(metadata, "word-count", "ao3:word-count", "words")
            ?? FindPrefaceWordCount(prefaceText);
        var status = FindMeta(metadata, "status", "ao3:status", "completion-status")
            ?? FindPrefaceStatus(prefaceText);
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

        return new EpubMetadata(
            title,
            string.IsNullOrWhiteSpace(author) ? null : author,
            string.IsNullOrWhiteSpace(description) ? null : description,
            sourceUrl,
            ao3WorkId,
            tags,
            wordCount,
            isComplete);
    }

    private static XDocument LoadXml(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        using var stream = entry.Open();
        cancellationToken.ThrowIfCancellationRequested();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static XDocument? LoadFirstSpineDocument(
        ZipArchive archive,
        XDocument package,
        string packagePath,
        CancellationToken cancellationToken)
    {
        var firstId = package
            .Root?
            .Element(PackageNamespace + "spine")?
            .Elements(PackageNamespace + "itemref")
            .Select(itemref => (string?)itemref.Attribute("idref"))
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        if (firstId is null)
        {
            return null;
        }

        var href = package
            .Root?
            .Element(PackageNamespace + "manifest")?
            .Elements(PackageNamespace + "item")
            .Where(item => string.Equals((string?)item.Attribute("id"), firstId, StringComparison.Ordinal))
            .Select(item => (string?)item.Attribute("href"))
            .FirstOrDefault(href => !string.IsNullOrWhiteSpace(href));
        if (href is null)
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
        var message = preface?
            .Descendants(XhtmlNamespace + "p")
            .Where(paragraph => paragraph.Attributes("class").Any(attribute =>
                attribute.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Contains("message", StringComparer.OrdinalIgnoreCase)))
            .FirstOrDefault();
        return message?
            .Descendants(XhtmlNamespace + "a")
            .Select(anchor => (string?)anchor.Attribute("href"))
            .Select(FindAo3WorkUrl)
            .FirstOrDefault(url => url is not null);
    }

    private static string? GetPrefaceText(XDocument? preface)
    {
        var prefaceElement = preface?
            .Descendants(XhtmlNamespace + "div")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("id"), "preface", StringComparison.Ordinal));
        var textSource = prefaceElement ?? preface?.Root;
        return textSource is null
            ? null
            : string.Join(
                " ",
                textSource
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

    private static string? FindPrefaceStatus(string? prefaceText)
    {
        var match = prefaceText is null ? null : StatusPattern.Match(prefaceText);
        return match?.Success == true ? match.Groups[1].Value : null;
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
