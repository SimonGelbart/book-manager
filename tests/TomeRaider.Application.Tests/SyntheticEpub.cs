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
        string? packageSourceUrl = null,
        string? chapterMarkup = null,
        string? statsText = null,
        string ao3MessageIntro = "Posted originally on the")
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
        var hasPreface = ao3Url is not null || wordCount is not null || status is not null || statsText is not null;
        var prefaceManifest = hasPreface
            ? "<item id=\"preface\" href=\"preface.xhtml\" media-type=\"application/xhtml+xml\" />"
            : string.Empty;
        var prefaceSpine = hasPreface ? "<itemref idref=\"preface\" />" : string.Empty;
        var prefaceMessage = ao3Url is null
            ? string.Empty
            : $"<p class=\"message\">{ao3MessageIntro} <a href=\"http://archiveofourown.org/\">Archive of Our Own</a> at <a href=\"{ao3Url}\">{ao3Url}</a>.</p>";
        var prefaceStats = statsText ?? string.Join(
            " ",
            new[]
            {
                wordCount is null ? null : $"Words: {wordCount}",
                status is null ? null : $"Status: {status}"
            }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
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
                $"<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><div id=\"preface\">{prefaceMessage}<dl class=\"tags\"><dt>Stats:</dt><dd>{prefaceStats}</dd></dl></div></body></html>");
        }

        AddEntry(
            archive,
            "OEBPS/chapter.xhtml",
            $"<html xmlns=\"http://www.w3.org/1999/xhtml\"><body>{chapterMarkup ?? "<p>Synthetic fixture.</p>"}</body></html>");
        return path;
    }

    public static string CreateImageFirstSpineWithFallback(
        string directory,
        string fileName,
        string title,
        string author)
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
                <dc:subject>General</dc:subject>
              </metadata>
              <manifest>
                <item id="cover-image" href="cover.jpg" media-type="image/jpeg" fallback="cover-xhtml" />
                <item id="cover-xhtml" href="cover.xhtml" media-type="application/xhtml+xml" />
              </manifest>
              <spine><itemref idref="cover-image" /></spine>
            </package>
            """);
        AddEntry(archive, "OEBPS/cover.jpg", [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10]);
        AddEntry(
            archive,
            "OEBPS/cover.xhtml",
            "<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><p>Cover fallback.</p></body></html>");
        return path;
    }

    public static string CreateSplitPreface(
        string directory,
        string fileName,
        string title,
        string author,
        string ao3Url,
        string description,
        string authorNotes,
        bool useOfficialAo3Structure = false)
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
                <dc:description></dc:description>
                <dc:subject>General</dc:subject>
              </metadata>
              <manifest>
                <item id="preface-one" href="preface-one.xhtml" media-type="application/xhtml+xml" />
                <item id="preface-two" href="preface-two.xhtml" media-type="application/xhtml+xml" />
                <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml" />
              </manifest>
              <spine>
                <itemref idref="preface-one" />
                <itemref idref="preface-two" />
                <itemref idref="chapter" />
              </spine>
            </package>
            """);
        AddEntry(
            archive,
            "OEBPS/preface-one.xhtml",
            $"""
            <html xmlns="http://www.w3.org/1999/xhtml"><body>
              <div id="preface">
                <p class="message">Posted originally on the <a href="https://archiveofourown.org/works/{ao3Url.Split('/').Last()}">{ao3Url}</a>.</p>
                <dl class="tags">
                  <dt>Fandoms:</dt><dd><a href="https://archiveofourown.org/tags/Synthetic%20Stories">Synthetic Stories</a></dd>
                  <dt>Additional Tags:</dt><dd><a href="https://archiveofourown.org/tags/Found%20Family">Found Family</a></dd>
                  <dt>Stats:</dt><dd>Words: 4,321 Chapters: 10/12 Status: Unfinished</dd>
                </dl>
              </div>
            </body></html>
            """);
        AddEntry(
            archive,
            "OEBPS/preface-two.xhtml",
            $"""
            <html xmlns="http://www.w3.org/1999/xhtml"><body>
              <div id="preface">
                {(useOfficialAo3Structure
                    ? $"<div class=\"meta\"><p>Summary</p><blockquote class=\"userstuff\">{description}</blockquote></div><div class=\"meta\"><p>Notes</p><blockquote class=\"userstuff\">{authorNotes}</blockquote></div>"
                    : $"<div class=\"meta group\"><h3 class=\"heading\">Summary:</h3><div class=\"userstuff\">{description}</div></div><div class=\"meta group\"><h3 class=\"heading\">Author's Notes:</h3><div class=\"userstuff\">{authorNotes}</div></div>")}
              </div>
            </body></html>
            """);
        AddEntry(
            archive,
            "OEBPS/chapter.xhtml",
            "<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><p>Synthetic chapter.</p></body></html>");
        return path;
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static void AddEntry(ZipArchive archive, string name, byte[] content)
    {
        using var stream = archive.CreateEntry(name).Open();
        stream.Write(content);
    }
}
