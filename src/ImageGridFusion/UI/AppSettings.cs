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
/// size and screen, the last folders of the file dialogs, the color dialog's custom colors, and the maximum zoom (edited by hand only). Each save rewrites the whole file at once.
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
    private const string ExplorerModeName = "ExplorerMode";
    private const string FaceAnalysisName = "FaceAnalysis";
    private const string FaceGroupingName = "FaceGrouping";
    private const string SimilarImagesName = "SimilarImages";
    private const string VisualAnalysisName = "VisualAnalysis";
    private const string CaptionsName = "Captions";
    private const string EmojiFontsDeclinedName = "EmojiFontsDeclined";
    private const string ExplorerOpenFolderName = "ExplorerOpenFolder";
    private const string ExplorerContentSearchName = "ExplorerContentSearch";
    private const string ExplorerFileOrderName = "ExplorerFileOrder";
    private const string ExplorerViewName = "ExplorerView";
    private const string ExplorerFolderGroupsName = "ExplorerFolderGroups";
    private const string ExplorerSearchGroupsName = "ExplorerSearchGroups";
    private const string ExplorerPatternsOffName = "ExplorerPatternsOff";
    private const string OcrArrowCurvedName = "OcrArrowCurved";
    private const string OcrArrowThicknessName = "OcrArrowThickness";
    private const string OcrArrowColorName = "OcrArrowColor";
    private const string MaxZoomName = "MaxZoom";
    private const string GuideApproachName = "GuideApproach";
    private const string GapBarsFromEdgesName = "GapBarsFromEdges";
    private const string WindowWidthName = "WindowWidth";
    private const string WindowHeightName = "WindowHeight";
    private const string WindowScreenName = "WindowScreen";
    private const string AddFolderName = "LastAddFolder";
    private const string SoundtrackFolderName = "LastSoundtrackFolder";
    private const string ExportFolderName = "LastExportFolder";
    private const string CustomColorsName = "CustomColors";
    private const string CustomColorsNextName = "CustomColorsNext";
    private const string LanguageName = "Language";
    private const string GiphyApiKeyName = "GiphyApiKey";
    private const string GiphyRatingName = "GiphyRating";
    private const string GiphyCallsName = "GiphyCalls";
    private const string GiphyGifsShownName = "GiphyGifsShown";
    private const string GiphyStickersShownName = "GiphyStickersShown";
    private const string SecondarySourcesName = "SecondarySources";

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

    /// <summary>The sizes the ⚙ menu offers for <see cref="VideoMaxSide"/>: Full HD, QHD, and 4K — <see cref="CanvasSizer.MaxSharedLongSide"/>.</summary>
    public static readonly int[] VideoMaxSides = [1920, 2560, CanvasSizer.MaxSharedLongSide];

    /// <summary>
    /// The longer side an MP4 or GIF export is clamped to — every canvas-sized buffer of the export
    /// shrinks with it — one of <see cref="VideoMaxSides"/>; <see cref="DefaultVideoMaxSide"/> when
    /// nothing was saved or the value is none of them. A PNG has its own cap, <see cref="CanvasSizer.MaxLongSide"/>.
    /// See workfiles/20261010-optimisation-lack-of-memory.md § 2.3.
    /// </summary>
    public static int VideoMaxSide => Int(VideoMaxSideName) is int value && VideoMaxSides.Contains(value) ? value : DefaultVideoMaxSide;

    /// <summary>Saves the video size; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveVideoMaxSide(int side) => Save((VideoMaxSideName, side));

    /// <summary>The UI language when nothing was saved: English.</summary>
    public const string DefaultLanguage = "en";

    /// <summary>The UI languages the ⚙ menu offers, by their culture name.</summary>
    public static readonly string[] Languages = [DefaultLanguage, "fr"];

    /// <summary>
    /// The UI language, one of <see cref="Languages"/> — <see cref="DefaultLanguage"/> when nothing was saved or the
    /// value is none of them. Read once, at start-up (<c>Program.Main</c>): a change applies at the next start.
    /// See workfiles/20261105-french-translation.md § UI Language Setting.
    /// </summary>
    public static string Language => Text(LanguageName) is { } value && Languages.Contains(value) ? value : DefaultLanguage;

    /// <summary>Saves the UI language; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveLanguage(string language) => Save((LanguageName, language));

    /// <summary>The folder the file explorer indexes, with its subfolders; null when none was saved.</summary>
    public static string? ExplorerFolder => Text(ExplorerFolderName);

    /// <summary>Saves the file explorer's folder; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerFolder(string folder) => Save((ExplorerFolderName, folder));

    /// <summary>
    /// The secondary sources, in the order they were added, each <c>{ "folder", "color" }</c>; an older list of bare
    /// paths read with its colors given in order, a missing or foreign color given the first free one; empty when
    /// none was saved. See workfiles/20261105-secondary-file-sources.md § Managing the Sources, § Source Colors.
    /// </summary>
    public static IReadOnlyList<SecondarySource> SecondarySources
    {
        get
        {
            if (Values[SecondarySourcesName] is not JsonArray array)
            {
                return [];
            }

            var folders = new List<string>();
            var colored = new List<SecondarySource>();
            foreach (var node in array)
            {
                string? folder = node switch
                {
                    JsonValue value when value.TryGetValue(out string? path) => path,
                    JsonObject entry when entry["folder"] is JsonValue value && value.TryGetValue(out string? path) => path,
                    _ => null,
                };
                if (folder is not { Length: > 0 })
                {
                    continue;
                }

                folders.Add(folder);
                if (node is JsonObject withColor && withColor["color"] is JsonValue color && color.TryGetValue(out int index))
                {
                    colored.Add(new SecondarySource(folder, index));
                }
            }

            return Explorer.SecondarySources.Colored(folders, colored, SourceColors.Palette.Length);
        }
    }

    /// <summary>Saves the secondary sources, each its folder and its color; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveSecondarySources(IEnumerable<SecondarySource> sources) =>
        Save((SecondarySourcesName, new JsonArray([.. sources.Select(source => (JsonNode?)new JsonObject { ["folder"] = source.Folder, ["color"] = source.Color })])));

    /// <summary>
    /// The user's GIPHY API key, the GIPHY view's searches sent with it — kept here only, never in the repository;
    /// null when none was saved. See workfiles/20261105-online-library-giphy.md § API Key.
    /// </summary>
    public static string? GiphyApiKey => Text(GiphyApiKeyName)?.Trim() is { Length: > 0 } key ? key : null;

    /// <summary>Saves the GIPHY API key, "" to forget it; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveGiphyApiKey(string key) => Save((GiphyApiKeyName, key.Trim()));

    /// <summary>
    /// The content rating every GIPHY request carries, one of <see cref="Web.Giphy.Ratings"/> —
    /// <see cref="Web.Giphy.DefaultRating"/> when nothing was saved or the value is none of them.
    /// </summary>
    public static string GiphyRating => Text(GiphyRatingName) is { } value && Web.Giphy.Ratings.Contains(value) ? value : Web.Giphy.DefaultRating;

    /// <summary>Saves the GIPHY content rating; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveGiphyRating(string rating) => Save((GiphyRatingName, rating));

    /// <summary>
    /// Whether the file explorer offers its GIPHY GIFs and GIPHY Stickers views — the ⚙ menu's GIPHY submenu; each
    /// shown when nothing was saved. See workfiles/20261105-giphy-gif-sticker-views.md § The ⚙ Menu's GIPHY Submenu.
    /// </summary>
    public static (bool Gifs, bool Stickers) GiphyViewsShown => (Int(GiphyGifsShownName) is not 0, Int(GiphyStickersShownName) is not 0);

    /// <summary>Saves which GIPHY views the file explorer offers; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveGiphyViewsShown(bool gifs, bool stickers) => Save((GiphyGifsShownName, gifs ? 1 : 0), (GiphyStickersShownName, stickers ? 1 : 0));

    /// <summary>
    /// When the app called GIPHY in the last hour, in Unix seconds — the quota counted across sessions, GIPHY giving
    /// none; empty when nothing was saved or the value is no list of numbers. See workfiles/20261105-online-library-giphy.md
    /// § Quota.
    /// </summary>
    public static long[] GiphyCalls =>
        Values[GiphyCallsName] is JsonArray array && array.All(node => node is JsonValue value && value.TryGetValue(out long _))
            ? [.. array.Select(node => node!.GetValue<long>())]
            : [];

    /// <summary>Saves when the app called GIPHY in the last hour; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveGiphyCalls(IEnumerable<long> calls) =>
        Save((GiphyCallsName, new JsonArray([.. calls.Select(call => (JsonNode?)call)])));

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

    /// <summary>Whether the guides' gap bars run from the image's edges rather than its center; its center when nothing was saved.</summary>
    public static bool GapBarsFromEdges => Int(GapBarsFromEdgesName) is int value && value != 0;

    /// <summary>Saves where the guides' gap bars start; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveGapBarsFromEdges(bool edges) => Save((GapBarsFromEdgesName, edges ? 1 : 0));

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

    /// <summary>The folder the file explorer's folder view has open, relative to the base folder; "" — the base folder — when none was saved.</summary>
    public static string ExplorerOpenFolder => Text(ExplorerOpenFolderName) ?? "";

    /// <summary>
    /// Saves what the file explorer lists and its open folder together — the older folder-view flag
    /// too, for an older version; throws an <see cref="IsSaveError"/> exception on failure. The view is
    /// written for an older version only: every start opens on the favorites, the open folder alone read back.
    /// </summary>
    public static void SaveExplorerMode(ExplorerMode mode, string openFolder) =>
        Save((ExplorerModeName, mode.ToString()), (ExplorerFolderViewName, mode == ExplorerMode.Folders ? 1 : 0), (ExplorerOpenFolderName, openFolder));

    /// <summary>
    /// Whether the file explorer finds the faces in the indexed images and groups them into people — the
    /// ⚙ menu's Face analysis; false, opt-in, when nothing was saved. See workfiles/20261027-face-detection.md § Privacy.
    /// </summary>
    public static bool FaceAnalysis => Int(FaceAnalysisName) is int value && value != 0;

    /// <summary>Saves whether the file explorer analyses the faces; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveFaceAnalysis(bool on) => Save((FaceAnalysisName, on ? 1 : 0));

    /// <summary>
    /// How strictly the faces are grouped into people — the ⚙ menu's Face grouping, a level of
    /// <see cref="Explorer.FaceGrouping.Thresholds"/>; Normal when nothing was saved or the value is not a
    /// level. See workfiles/20261028-face-grouping-accuracy.md § Strictness Setting.
    /// </summary>
    public static int FaceGrouping =>
        Int(FaceGroupingName) is int value && value >= 0 && value < Explorer.FaceGrouping.Thresholds.Length ? value : Explorer.FaceGrouping.DefaultLevel;

    /// <summary>Saves how strictly the faces are grouped; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveFaceGrouping(int level) => Save((FaceGroupingName, level));

    /// <summary>
    /// How far an image found by its similarity must stand out — the ⚙ menu's Similar images, a level of
    /// <see cref="VisionSimilarity.Cutoffs"/>; Normal when nothing was saved or the value is not a level. See
    /// workfiles/20261029-similar-images-accuracy.md § Strictness Setting.
    /// </summary>
    public static int SimilarImages =>
        Int(SimilarImagesName) is int value && value >= 0 && value < VisionSimilarity.Cutoffs.Length ? value : VisionSimilarity.DefaultLevel;

    /// <summary>Saves how far an image found by its similarity must stand out; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveSimilarImages(int level) => Save((SimilarImagesName, level));

    /// <summary>
    /// Whether the file explorer computes the images' embeddings and tags, for the searches by what the
    /// images show — the ⚙ menu's Visual analysis; false, opt-in, when nothing was saved. See
    /// workfiles/20261027-image-content-analysis.md § Engine Offer.
    /// </summary>
    public static bool VisualAnalysis => Int(VisualAnalysisName) is int value && value != 0;

    /// <summary>Saves whether the file explorer analyses what the images show; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveVisualAnalysis(bool on) => Save((VisualAnalysisName, on ? 1 : 0));

    /// <summary>Whether the file explorer writes a caption for every image — the ⚙ menu's Captions; false, opt-in, when nothing was saved.</summary>
    public static bool Captions => Int(CaptionsName) is int value && value != 0;

    /// <summary>Saves whether the file explorer writes the images' captions; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveCaptions(bool on) => Save((CaptionsName, on ? 1 : 0));

    /// <summary>
    /// Whether the emoji fonts' download was declined at a launch: it is not offered at launch again, the ⚙ menu still
    /// offers it (workfiles/20261102-emoji-fonts.md § Shipped Fonts).
    /// </summary>
    public static bool EmojiFontsDeclined => Int(EmojiFontsDeclinedName) is int value && value != 0;

    /// <summary>Saves that the emoji fonts' download was declined at launch; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveEmojiFontsDeclined() => Save((EmojiFontsDeclinedName, 1));

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
    /// Whether the file explorer's folder view and its search results show their groups — their buttons';
    /// both on when nothing was saved. See workfiles/20261103-file-explorer-groups.md § Toggles.
    /// </summary>
    public static (bool Folders, bool Search) ExplorerGroups =>
        (Int(ExplorerFolderGroupsName) is not int folders || folders != 0, Int(ExplorerSearchGroupsName) is not int search || search != 0);

    /// <summary>Saves whether the folder view and the search results show their groups; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerGroups(bool folders, bool search) =>
        Save((ExplorerFolderGroupsName, folders ? 1 : 0), (ExplorerSearchGroupsName, search ? 1 : 0));

    /// <summary>
    /// The name groups of the folder view turned off — the ⚙ menu's File explorer groups — saved by id,
    /// comma-separated; none when nothing was saved. An id no group has any more turns nothing off.
    /// </summary>
    public static IReadOnlySet<string> ExplorerPatternsOff =>
        (Text(ExplorerPatternsOffName) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Saves the name groups turned off; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveExplorerPatternsOff(IEnumerable<string> off) =>
        Save((ExplorerPatternsOffName, string.Join(',', off.Order(StringComparer.Ordinal))));

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

    /// <summary>
    /// The color dialog's custom colors, as the dialog holds them (COLORREF, <c>0x00BBGGRR</c>); null when none
    /// were saved, or the value is not <see cref="SharedColorDialog.SlotCount"/> numbers.
    /// </summary>
    public static int[]? CustomColors =>
        Values[CustomColorsName] is JsonArray { Count: SharedColorDialog.SlotCount } array
        && array.All(node => node is JsonValue value && value.TryGetValue(out int _))
            ? [.. array.Select(node => node!.GetValue<int>())]
            : null;

    /// <summary>The custom slot the eyedropper writes into once none is free; 0 when none was saved or out of range.</summary>
    public static int CustomColorsNext =>
        Int(CustomColorsNextName) is int next && next is >= 0 and < SharedColorDialog.SlotCount ? next : 0;

    /// <summary>Saves the color dialog's custom colors and the next slot, at once; throws an <see cref="IsSaveError"/> exception on failure.</summary>
    public static void SaveCustomColors(int[] colors, int next) =>
        Save((CustomColorsName, new JsonArray([.. colors.Select(color => (JsonNode?)color)])), (CustomColorsNextName, next));
}
