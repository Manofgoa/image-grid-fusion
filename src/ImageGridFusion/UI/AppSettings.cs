using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImageGridFusion.Composition;
using ImageGridFusion.Explorer;

namespace ImageGridFusion.UI;

/// <summary>
/// The app's settings remembered between sessions, in <see cref="FileName"/> next to the exe — never the
/// registry: the border color, whether the borders' Twitter corners are on by default, the video size, the file explorer's
/// base folder, whether its panel is open, its width, its tile size, its pages per load, its view and
/// open folder, its files' order, whether it searches the files' contents, the OCR result style, the window's
/// size and screen, the last folders of the file dialogs, and the maximum zoom (edited by hand only). Each save rewrites the whole file at once.
/// </summary>
internal static class AppSettings
{
    public const string FileName = "settings.json";

    private const string BorderColorName = "BorderColor";
    private const string TwitterCornersName = "TwitterCornersByDefault";
    private const string VideoMaxSideName = "VideoMaxSide";
    private const string ExplorerFolderName = "ExplorerFolder";
    private const string ExplorerPanelOpenName = "ExplorerPanelOpen";
    private const string ExplorerWidthName = "ExplorerWidth";
    private const string ExplorerTileSizeName = "ExplorerTileSize";
    private const string ExplorerPagesPerLoadName = "ExplorerPagesPerLoad";
    private const string ExplorerFolderViewName = "ExplorerFolderView";
    private const string ExplorerOpenFolderName = "ExplorerOpenFolder";
    private const string ExplorerContentSearchName = "ExplorerContentSearch";
    private const string ExplorerFileOrderName = "ExplorerFileOrder";
    private const string ExplorerViewName = "ExplorerView";
    private const string OcrArrowCurvedName = "OcrArrowCurved";
    private const string OcrArrowThicknessName = "OcrArrowThickness";
    private const string OcrArrowColorName = "OcrArrowColor";
    private const string MaxZoomName = "MaxZoom";
    private const string GuideApproachName = "GuideApproach";
    private const string WindowWidthName = "WindowWidth";
    private const string WindowHeightName = "WindowHeight";
    private const string WindowScreenName = "WindowScreen";
    private const string AddFolderName = "LastAddFolder";
    private const string SoundtrackFolderName = "LastSoundtrackFolder";
    private const string ExportFolderName = "LastExportFolder";

    /// <summary>
    /// The names the settings had as registry values, in the same types (DWORD → int, string → string):
    /// what <see cref="RegistryMigration"/> copies across.
    /// </summary>
    public static readonly string[] RegistryNames =
    [
        BorderColorName, TwitterCornersName, ExplorerFolderName, ExplorerPanelOpenName, ExplorerWidthName,
        ExplorerTileSizeName, ExplorerPagesPerLoadName, WindowWidthName, WindowHeightName,
    ];

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private static JsonObject? _values;

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>The settings read from the file once; empty when it is missing or cannot be read or parsed.</summary>
    private static JsonObject Values => _values ??= Load();

    private static JsonObject Load()
    {
        try
        {
            return File.Exists(FilePath) && JsonNode.Parse(File.ReadAllText(FilePath)) is JsonObject values ? values : [];
        }
        catch (Exception ex) when (IsSaveError(ex) || ex is JsonException)
        {
            return [];
        }
    }

    /// <summary>Whether a save failed on the file: the exceptions the Save methods throw.</summary>
    public static bool IsSaveError(Exception ex) => ex is IOException or UnauthorizedAccessException or SecurityException;

    private static int? Int(string name) => Values[name] is JsonValue value && value.TryGetValue(out int result) ? result : null;

    private static string? Text(string name) => Values[name] is JsonValue value && value.TryGetValue(out string? result) && result is { Length: > 0 } ? result : null;

    /// <summary>Whether a value is saved under <paramref name="name"/>.</summary>
    public static bool Has(string name) => Values.ContainsKey(name);

    /// <summary>
    /// Sets the values and writes the file; throws an <see cref="IsSaveError"/> exception on failure. The file
    /// is read again first, so what another instance saved in the meantime is kept.
    /// </summary>
    public static void Save(params (string Name, JsonNode? Value)[] values)
    {
        _values = Load();
        foreach (var (name, value) in values)
        {
            Values[name] = value;
        }

        // Written beside, then moved over: a failure never leaves a half-written file.
        string temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, Values.ToJsonString(WriteOptions));
        File.Move(temporary, FilePath, overwrite: true);
    }

    /// <summary>The border color before one is chosen, and the one the Borders' Resets and Clear all bring back.</summary>
    public static readonly Color DefaultBorderColor = Color.HotPink;

    /// <summary>The last color chosen for the borders, theirs at start-up; <see cref="DefaultBorderColor"/> when none was saved.</summary>
    public static Color BorderColor => Int(BorderColorName) is int argb ? Color.FromArgb(argb) : DefaultBorderColor;

    /// <summary>Saves the border color; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveBorderColor(Color color) => Save((BorderColorName, color.ToArgb()));

    /// <summary>Whether the borders start with their Twitter corners; true when nothing was saved.</summary>
    public static bool TwitterCornersByDefault => Int(TwitterCornersName) is not int value || value != 0;

    /// <summary>Saves whether the borders start with their Twitter corners; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveTwitterCornersByDefault(bool on) => Save((TwitterCornersName, on ? 1 : 0));

    /// <summary>The longer side a video export (MP4, GIF) is clamped to when nothing was saved: Full HD.</summary>
    public const int DefaultVideoMaxSide = 1920;

    /// <summary>The sizes the ⚙ menu offers for <see cref="VideoMaxSide"/>: Full HD, QHD, and 4K — the PNG's own maximum.</summary>
    public static readonly int[] VideoMaxSides = [1920, 2560, CanvasSizer.MaxLongSide];

    /// <summary>
    /// The longer side an MP4 or GIF export is clamped to — every canvas-sized buffer of the export
    /// shrinks with it — one of <see cref="VideoMaxSides"/>; <see cref="DefaultVideoMaxSide"/> when
    /// nothing was saved or the value is none of them. A PNG keeps <see cref="CanvasSizer.MaxLongSide"/>.
    /// See workfiles/20261010-optimisation-lack-of-memory.md § 2.3.
    /// </summary>
    public static int VideoMaxSide => Int(VideoMaxSideName) is int value && VideoMaxSides.Contains(value) ? value : DefaultVideoMaxSide;

    /// <summary>Saves the video size; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveVideoMaxSide(int side) => Save((VideoMaxSideName, side));

    /// <summary>The folder the file explorer indexes, with its subfolders; null when none was saved.</summary>
    public static string? ExplorerFolder => Text(ExplorerFolderName);

    /// <summary>Saves the file explorer's folder; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerFolder(string folder) => Save((ExplorerFolderName, folder));

    /// <summary>Whether the file explorer's panel is open; true when nothing was saved.</summary>
    public static bool ExplorerPanelOpen => Int(ExplorerPanelOpenName) is not int value || value != 0;

    /// <summary>Saves whether the file explorer's panel is open; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerPanelOpen(bool open) => Save((ExplorerPanelOpenName, open ? 1 : 0));

    /// <summary>
    /// The file explorer panel's open width in logical pixels, <see cref="FileExplorerPanel.MinOpenWidth"/>
    /// at least; the default width when nothing was saved.
    /// </summary>
    public static int ExplorerWidth => Int(ExplorerWidthName) is int value ? Math.Clamp(value, FileExplorerPanel.MinOpenWidth, 10000) : FileExplorerPanel.DefaultOpenWidth;

    /// <summary>Saves the file explorer panel's open width; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerWidth(int width) => Save((ExplorerWidthName, width));

    /// <summary>
    /// The file explorer's tile size in logical pixels, <see cref="ThumbnailGrid.MinTileSize"/> to
    /// <see cref="ThumbnailGrid.MaxTileSize"/>; the default size when nothing was saved.
    /// </summary>
    public static int ExplorerTileSize => Int(ExplorerTileSizeName) is int value ? Math.Clamp(value, ThumbnailGrid.MinTileSize, ThumbnailGrid.MaxTileSize) : ThumbnailGrid.DefaultTileSize;

    /// <summary>Saves the file explorer's tile size; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerTileSize(int size) => Save((ExplorerTileSizeName, size));

    /// <summary>
    /// How many pages of tiles the file explorer loads at a time, <see cref="FileExplorerPanel.MinPagesPerLoad"/>
    /// to <see cref="FileExplorerPanel.MaxPagesPerLoad"/>; the default when nothing was saved or the value
    /// is out of range.
    /// </summary>
    public static int ExplorerPagesPerLoad =>
        Int(ExplorerPagesPerLoadName) is int value && value is >= FileExplorerPanel.MinPagesPerLoad and <= FileExplorerPanel.MaxPagesPerLoad
            ? value
            : FileExplorerPanel.DefaultPagesPerLoad;

    /// <summary>Saves the file explorer's pages per load; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerPagesPerLoad(int pages) => Save((ExplorerPagesPerLoadName, pages));

    /// <summary>
    /// How far before its stop a guide shows, in percent of the cell — one of
    /// <see cref="GridPreview.GuideApproachChoices"/>; the default when nothing was saved or the value is not one of them.
    /// </summary>
    public static int GuideApproach =>
        Int(GuideApproachName) is int value && GridPreview.GuideApproachChoices.Contains(value) ? value : GridPreview.DefaultGuideApproach;

    /// <summary>Saves how far before its stop a guide shows; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveGuideApproach(int percent) => Save((GuideApproachName, percent));

    /// <summary>The maximum zoom, in percent, when the file holds no whole number for it.</summary>
    public const int DefaultMaxZoom = 2000;

    /// <summary>The lowest maximum zoom accepted, in percent: a lower one is raised to it.</summary>
    public const int MinMaxZoom = 200;

    /// <summary>The highest maximum zoom accepted, in percent: a higher one is lowered to it.</summary>
    public const int MaxMaxZoom = 10000;

    /// <summary>
    /// The maximum zoom in percent — no UI sets it, it is edited by hand in the file: a whole number
    /// clamped to <see cref="MinMaxZoom"/>–<see cref="MaxMaxZoom"/>, <see cref="DefaultMaxZoom"/> when
    /// missing or not a whole number. See workfiles/20261006-wheel-zoom-step.md.
    /// </summary>
    public static int MaxZoom => Int(MaxZoomName) is int value ? Math.Clamp(value, MinMaxZoom, MaxMaxZoom) : DefaultMaxZoom;

    /// <summary>Whether the file holds <see cref="MaxZoom"/> as it is applied: false when missing, unreadable or out of range.</summary>
    public static bool HoldsMaxZoom => Int(MaxZoomName) == MaxZoom;

    /// <summary>Saves the maximum zoom, in percent; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveMaxZoom(int percent) => Save((MaxZoomName, percent));

    /// <summary>Whether the file explorer shows its folder view; false — the search view — when nothing was saved.</summary>
    public static bool ExplorerFolderView => Int(ExplorerFolderViewName) is int value && value != 0;

    /// <summary>The folder the file explorer's folder view has open, relative to the base folder; "" — the base folder — when none was saved.</summary>
    public static string ExplorerOpenFolder => Text(ExplorerOpenFolderName) ?? "";

    /// <summary>Saves the file explorer's view and open folder together; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerFolderView(bool folderView, string openFolder) =>
        Save((ExplorerFolderViewName, folderView ? 1 : 0), (ExplorerOpenFolderName, openFolder));

    /// <summary>
    /// The file explorer's order of the files, its sort drop-down's — saved by name; <see cref="FileOrder.Newest"/>
    /// when nothing was saved or the name is unknown. See workfiles/20261009-search-results-sort.md.
    /// </summary>
    public static FileOrder ExplorerFileOrder =>
        Text(ExplorerFileOrderName) is { } name && Enum.TryParse(name, out FileOrder order) && order.ToString() == name
            ? order
            : FileOrder.Newest;

    /// <summary>Saves the file explorer's order of the files; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerFileOrder(FileOrder order) => Save((ExplorerFileOrderName, order.ToString()));

    /// <summary>
    /// The file explorer's view — Thumbnails or Details, its view buttons' — saved by name;
    /// <see cref="ExplorerView.Thumbnails"/> when nothing was saved or the name is unknown. See
    /// workfiles/20261009-explorer-details-view.md.
    /// </summary>
    public static ExplorerView ExplorerView =>
        Text(ExplorerViewName) is { } name && Enum.TryParse(name, out ExplorerView view) && view.ToString() == name
            ? view
            : ExplorerView.Thumbnails;

    /// <summary>Saves the file explorer's view; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerView(ExplorerView view) => Save((ExplorerViewName, view.ToString()));

    /// <summary>
    /// Whether the file explorer extracts the files' content texts — OCR included — for its search; false
    /// when nothing was saved. See workfiles/20260926-ocr-search.md § Extraction.
    /// </summary>
    public static bool ExplorerContentSearch => Int(ExplorerContentSearchName) is int value && value != 0;

    /// <summary>Saves whether the file explorer searches the files' contents; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerContentSearch(bool on) => Save((ExplorerContentSearchName, on ? 1 : 0));

    /// <summary>
    /// How the light bulb's arrow is drawn — the ⚙ menu's OCR result style: each part read on its own,
    /// its <see cref="OcrArrowStyle.Default"/> value when missing or, for the thickness, out of range.
    /// </summary>
    public static OcrArrowStyle OcrArrow => new(
        Int(OcrArrowCurvedName) is not int curved || curved != 0,
        Int(OcrArrowThicknessName) is int thickness && thickness is >= OcrArrowStyle.MinThickness and <= OcrArrowStyle.MaxThickness
            ? thickness
            : OcrArrowStyle.DefaultThickness,
        Int(OcrArrowColorName) is int argb ? Color.FromArgb(argb) : OcrArrowStyle.DefaultColor);

    /// <summary>Saves the OCR result style; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveOcrArrow(OcrArrowStyle style) =>
        Save((OcrArrowCurvedName, style.Curved ? 1 : 0), (OcrArrowThicknessName, style.Thickness), (OcrArrowColorName, style.Color.ToArgb()));

    /// <summary>
    /// The client size the window had at its last use, in logical (96 DPI) pixels; null when none was
    /// saved, or a value is missing or not positive.
    /// </summary>
    public static Size? WindowClientSize =>
        Int(WindowWidthName) is int width && width > 0 && Int(WindowHeightName) is int height && height > 0
            ? new Size(width, height)
            : null;

    /// <summary>The device name of the monitor the window was last used on (<c>\\.\DISPLAY2</c>); null when none was saved.</summary>
    public static string? WindowScreen => Text(WindowScreenName);

    /// <summary>
    /// Saves the window's client size, in logical pixels, and the device name of its monitor, at once;
    /// throws an <see cref="IsSaveError"/> exception on failure.
    /// </summary>
    public static void SaveWindowPlacement(Size size, string screen) =>
        Save((WindowWidthName, size.Width), (WindowHeightName, size.Height), (WindowScreenName, screen));

    /// <summary>The folder of the files last picked in the Add images dialog; null when none was saved.</summary>
    public static string? AddFolder => Text(AddFolderName);

    /// <summary>Saves the Add images dialog's folder; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveAddFolder(string folder) => Save((AddFolderName, folder));

    /// <summary>The folder of the file last picked in the soundtrack dialog; null when none was saved.</summary>
    public static string? SoundtrackFolder => Text(SoundtrackFolderName);

    /// <summary>Saves the soundtrack dialog's folder; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveSoundtrackFolder(string folder) => Save((SoundtrackFolderName, folder));

    /// <summary>
    /// The folder of the file last saved by an export or Save last…, shared by both; null until a first
    /// one was saved.
    /// </summary>
    public static string? ExportFolder => Text(ExportFolderName);

    /// <summary>Saves the exports' folder; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExportFolder(string folder) => Save((ExportFolderName, folder));
}
