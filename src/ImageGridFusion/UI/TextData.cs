using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;

namespace ImageGridFusion.UI;

/// <summary>
/// A text on the clipboard or dragged from another app, in every form it comes in. Read on the UI
/// thread, where the data object lives; parsed off it.
/// </summary>
internal sealed record TextData(string? Rtf, string? Html, string? Plain)
{
    private static readonly string[] Formats = [DataFormats.Rtf, DataFormats.Html, DataFormats.UnicodeText, DataFormats.Text];

    public static bool IsIn(IDataObject data) => Formats.Any(data.GetDataPresent);

    public static TextData? TryGet(IDataObject data)
    {
        var text = new TextData(Get(data, DataFormats.Rtf), Get(data, DataFormats.Html), Get(data, DataFormats.UnicodeText) ?? Get(data, DataFormats.Text));
        return text is { Rtf: null, Html: null, Plain: null } ? null : text;
    }

    /// <summary>
    /// The richest form holding visible text: RTF, else HTML, else plain text — so an HTML holding
    /// only an image gives way to its plain text, the image's address — with the form it was read
    /// from, to be saved as is. Null when all are blank.
    /// </summary>
    public (StyledText Text, TextOrigin Origin)? Parse()
    {
        if (Rtf is not null && RtfReader.TryRead(Rtf) is { IsBlank: false } rtf)
        {
            return (rtf, new TextOrigin(Rtf, ".rtf"));
        }

        if (Html is not null && HtmlReader.TryRead(Html) is { IsBlank: false } html)
        {
            return (html, new TextOrigin(HtmlDocument(Html), ".html"));
        }

        return Plain is not null && StyledText.Plain(Plain) is { IsBlank: false } plain ? (plain, new TextOrigin(Plain, ".txt")) : null;
    }

    /// <summary>The fragment the clipboard's header points to, as a document a browser opens in UTF-8.</summary>
    private static string HtmlDocument(string clipboardHtml) =>
        $"<!DOCTYPE html>\n<html>\n<head><meta charset=\"utf-8\"></head>\n<body>\n{HtmlReader.Fragment(clipboardHtml)}\n</body>\n</html>\n";

    private static string? Get(IDataObject data, string format) =>
        data.GetDataPresent(format) && data.GetData(format) is string { Length: > 0 } text ? text : null;
}
