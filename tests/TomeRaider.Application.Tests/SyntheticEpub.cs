using System.IO.Compression;
using System.Text;

namespace TomeRaider.Application.Tests;

internal static class SyntheticEpub
{
    public static string Create(
        string directory,
        string fileName,
        string title,
        string author,
        string? ao3Url = null,
        int? wordCount = null,
        string? status = null,
        string? description = null,
        string? packageSourceUrl = null)
    {
        var path = Path.Combine(directory, fileName);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        AddEntry(
            archive,
            "META-INF/container.xml",
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
              <rootfiles>
                <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml" />
              </rootfiles>
            </container>
            """);

        var sourceMetadata = packageSourceUrl is null
            ? string.Empty
            : $"<meta name=\"source\" content=\"{packageSourceUrl}\" />";
        var descriptionMetadata = description is null
            ? string.Empty
            : $"<dc:description>{description}</dc:description>";
        var subjects = ao3Url is null
            ? "<dc:subject>General</dc:subject>"
            : "<dc:subject>Fandom: Synthetic Stories</dc:subject><dc:subject>Found Family</dc:subject>";
        var hasPreface = ao3Url is not null || wordCount is not null || status is not null;
        var prefaceManifest = hasPreface
            ? "<item id=\"preface\" href=\"preface.xhtml\" media-type=\"application/xhtml+xml\" />"
            : string.Empty;
        var prefaceSpine = hasPreface ? "<itemref idref=\"preface\" />" : string.Empty;
        var prefaceMessage = ao3Url is null
            ? string.Empty
            : $"<p class=\"message\"><a href=\"{ao3Url}\">Originally posted on AO3</a></p>";
        var prefaceStats = string.Concat(
            wordCount is null ? string.Empty : $"<dt>Words:</dt><dd>{wordCount}</dd>",
            status is null ? string.Empty : $"<dt>Status:</dt><dd>{status}</dd>");
        AddEntry(
            archive,
            "OEBPS/content.opf",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:identifier>urn:uuid:{Guid.NewGuid():D}</dc:identifier>
                <dc:title>{title}</dc:title>
                <dc:creator>{author}</dc:creator>
                {descriptionMetadata}
                {subjects}
                {sourceMetadata}
              </metadata>
              <manifest>
                {prefaceManifest}
                <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml" />
              </manifest>
              <spine>{prefaceSpine}<itemref idref="chapter" /></spine>
            </package>
            """);
        if (hasPreface)
        {
            AddEntry(
                archive,
                "OEBPS/preface.xhtml",
                $"<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><div id=\"preface\">{prefaceMessage}<dl class=\"stats\">{prefaceStats}</dl></div></body></html>");
        }

        AddEntry(
            archive,
            "OEBPS/chapter.xhtml",
            "<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><p>Synthetic fixture.</p></body></html>");
        return path;
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
