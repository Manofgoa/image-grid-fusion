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
    /// only an image gives way to its plain text, the image's address. Null when all are blank.
    /// </summary>
    public StyledText? Parse()
    {
        if (Rtf is not null && RtfReader.TryRead(Rtf) is { IsBlank: false } rtf)
        {
            return rtf;
        }

        if (Html is not null && HtmlReader.TryRead(Html) is { IsBlank: false } html)
        {
            return html;
        }

        return Plain is not null && StyledText.Plain(Plain) is { IsBlank: false } plain ? plain : null;
    }

    private static string? Get(IDataObject data, string format) =>
        data.GetDataPresent(format) && data.GetData(format) is string { Length: > 0 } text ? text : null;
}
