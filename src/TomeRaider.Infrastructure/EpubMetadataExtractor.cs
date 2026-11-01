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
    private static readonly Regex Ao3WorkUrlPattern = new(
        @"https?://(?:www\.)?archiveofourown\.org/(?:works|chapters)/(\d+)",
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
        var metadataText = string.Join(
            "\n",
            metadata.Descendants()
                .Select(element => element.Value)
                .Concat(metadata.DescendantsAndSelf().Attributes().Select(attribute => attribute.Value)));
        var ao3Match = Ao3WorkUrlPattern.Match(metadataText);
        var sourceUrl = ao3Match.Success ? ao3Match.Value : null;
        var ao3WorkId = ao3Match.Success ? ao3Match.Groups[1].Value : null;
        var tags = metadata
            .Elements(DublinCoreNamespace + "subject")
            .Select(subject => subject.Value.Trim())
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var wordCount = FindIntegerMeta(metadata, "word-count", "ao3:word-count", "words");
        var status = FindMeta(metadata, "status", "ao3:status", "completion-status");
        bool? isComplete = null;
        if (status is not null)
        {
            isComplete = status.Equals("complete", StringComparison.OrdinalIgnoreCase)
                ? true
                : status.Equals("unfinished", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("in-progress", StringComparison.OrdinalIgnoreCase)
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
