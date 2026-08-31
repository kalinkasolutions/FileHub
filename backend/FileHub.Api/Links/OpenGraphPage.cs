using System.Globalization;
using System.Text.Encodings.Web;

namespace FileHub.Links;

/// <summary>
/// The tiny HTML page behind <c>og/share/{id}</c>: Open Graph tags for whatever chat client is
/// unfurling the link, and an immediate redirect to the SPA for a human.
/// <para>
/// This page is served to the public internet and both values on it are attacker-controlled — the
/// id comes from the URL and the title is a file name off a disk somebody else mounted. The Go
/// build used <c>html/template</c> for context-aware escaping; the same job is done here by
/// encoding each value for the context it lands in <em>before</em> it reaches the template:
/// <see cref="HtmlEncoder"/> for the attribute values in the head, <see cref="JavaScriptEncoder"/>
/// for the string literal in the script (which also escapes <c>&lt;</c>, so a name containing
/// <c>&lt;/script&gt;</c> cannot break out). Never interpolate a raw value into this string.
/// </para>
/// </summary>
public static class OpenGraphPage
{
    public static string Render(string title, string description, string imageUrl, string shareLink, string landingPath)
    {
        var html = HtmlEncoder.Default;
        var script = JavaScriptEncoder.Default;

        var encodedTitle = html.Encode(title);
        var encodedDescription = html.Encode(description);
        var encodedImageUrl = html.Encode(imageUrl);
        var encodedShareLink = html.Encode(shareLink);
        var encodedHref = html.Encode(landingPath);
        var encodedTarget = script.Encode(landingPath);

        return $"""
                <!DOCTYPE html>
                <html>
                <head>
                    <link rel="icon" type="image/x-icon" href="favicon.ico">
                    <meta property="og:title" content="{encodedTitle}" />
                    <meta property="og:description" content="{encodedDescription}" />
                    <meta property="og:image" content="{encodedImageUrl}" />
                    <meta property="og:type" content="website" />
                    <meta property="og:url" content="{encodedShareLink}" />
                </head>
                <body>
                    <a href="{encodedHref}">share link</a>
                    <script>location.href = "{encodedTarget}"</script>
                </body>
                </html>
                """;
    }

    /// <summary>
    /// The Go build's size formatter, kept as it was so an already-shared link's preview reads the
    /// same: powers of 1000 rather than 1024, two decimals, and an empty string below one byte
    /// (which is what an empty file, and a share whose size was never measured, produce).
    /// </summary>
    public static string FormatSize(long size)
    {
        var gigabytes = size / 1_000_000_000d;
        if (gigabytes >= 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{gigabytes:F2} Gb");
        }

        var megabytes = size / 1_000_000d;
        if (megabytes >= 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{megabytes:F2} Mb");
        }

        var kilobytes = size / 1_000d;
        if (kilobytes >= 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{kilobytes:F2} Kb");
        }

        if (size >= 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{size} bytes");
        }

        return string.Empty;
    }
}
