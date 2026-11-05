using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Explorer;
using ImageGridFusion.Imaging;
using ImageGridFusion.Resources;

namespace ImageGridFusion.UI;

/// <summary>
/// The file explorer, at the right of the preview (see workfiles/20260926-file-explorer.md and
/// workfiles/20260928-tile-size-slider.md): a search box over the index of the base folder, its
/// matches as the user types — every file, the most recently created first, for <c>*</c> — and the
/// favorites — all of them, the newest first — while the box is empty, as a grid of thumbnail tiles
/// filling their rows at the size of the slider below them, loaded a few pages at a time as the list
/// scrolls (workfiles/20260927-file-explorer-show-all.md);
/// the panel's width is the user's, dragged from its edge. A tile is dragged onto a cell like a file
/// from the Explorer, double-clicked to be added like Add images, hearted to become a favorite.
/// Collapses to a strip. The cached index is loaded, then rescanned in the background, at every start.
/// The 📁 toggle switches to the folder view (workfiles/20260930-file-explorer-folder-view.md): the
/// open folder's subfolders and files as the disk holds them, a folder tile opening its folder, the
/// breadcrumb in the caption's line going back up, the search limited to the open folder.
/// </summary>
internal sealed partial class FileExplorerPanel : Panel
{
    // In logical pixels: the strip, and what the panel adds around its grid (paddings, the list's
    // border, the grid's insets, a vertical scrollbar).
    private const int StripWidth = 20;
    private const int Surround = 39;

    /// <summary>The narrowest open panel, in logical pixels: the smallest tile and the surround.</summary>
    public const int MinOpenWidth = ThumbnailGrid.MinTileSize + Surround;

    /// <summary>The open width before the user drags it, in logical pixels: one 200 px tile.</summary>
    public const int DefaultOpenWidth = 240;

    /// <summary>How many pages of tiles a load holds: the ⚙ menu's choices, and its default.</summary>
    public const int MinPagesPerLoad = 1;
    public const int MaxPagesPerLoad = 10;
    public const int DefaultPagesPerLoad = 2;

    private const int TransientDuration = 5000;

    // The content extraction (workfiles/20260926-ocr-search.md § Extraction), in ms: how long it waits
    // after a keystroke, how often it looks again while paused, how often it saves its texts and
    // refreshes the search shown.
    private const int KeystrokePause = 1000;
    private const int PausePoll = 200;

    // The image content analysis (workfiles/20261027-image-content-analysis.md § Search Under 💡): the
    // closest images a similarity lists, how long the typing pauses before the query is embedded, how long
    // the text model stays loaded without a search.
    private const int SimilarCount = 100;
    private const int QueryDelayMs = 300;
    private const int TextEncoderIdleMs = 5 * 60 * 1000;

    // The most characters of the text read a tile's tooltip shows (workfiles/20261027-image-content-analysis.md § Tile Tooltip).
    private const int TipTextLength = 200;
    private const int ContentSaveInterval = 30000;
    private const int ContentRefreshInterval = 5000;

    // The texts are also saved every ContentSaveEvery files extracted, no sooner than ContentSaveFloor
    // ms after the previous save (workfiles/20261007-index-write-every-50-files.md § Design).
    private const int ContentSaveEvery = 50;
    private const int ContentSaveFloor = 5000;

    // How long a folder may take to list before the status line says it is being read, in ms.
    private const int ReadingNoticeDelay = 150;

    private static string SearchPlaceholder => Strings.ExplorerSearchPlaceholder;
    private static string FolderSearchPlaceholder => Strings.ExplorerFolderSearchPlaceholder;
    private static string PeopleSearchPlaceholder => Strings.ExplorerPeopleSearchPlaceholder;
    private static string OpenInExplorerText => Strings.ExplorerMenuOpenInExplorer;
    private static string FolderInviteText => Strings.ExplorerInviteFolder;

    // A file tile's menu labels, the URL zone's preview menu showing the same ones.
    internal static string OpenFileLocationText => Strings.ExplorerMenuOpenFileLocation;
    internal static string CopyText => Strings.ExplorerMenuCopy;
    internal static string CopyPngText => Strings.ExplorerMenuCopyPng;
    internal static string CopyFullPathText => Strings.ExplorerMenuCopyFullPath;
    private static string SortNewestText => Strings.ExplorerSortNewest;

    // The sections' titles beside the folder groups' and the name patterns' (workfiles/20261103-file-explorer-groups.md).
    private static string OtherFoldersTitle => Strings.ExplorerSectionOtherFolders;
    private static string NamedFilesTitle => Strings.ExplorerSectionNamedFiles;
    private static string PathTitle => Strings.ExplorerSectionPath;
    private static string ContentTitle => Strings.ExplorerSectionContent;
    private static string SortNameText => Strings.ExplorerSortName;
    private static string SortSmallestText => Strings.ExplorerSortSmallest;

    // The sort drop-down's width in logical pixels: its longest entry and the arrow.
    private const int SortWidth = 92;

    // The views drop-down's width in logical pixels: its icon, its longest entry and the arrow.
    private const int ModesWidth = 96;

    // The views drop-down's icons, in ExplorerMode's order (workfiles/20261029-ctrl-f-view-cycle.md § Drop-Down
    // Icons), in full color (workfiles/20261029-menu-icons.md § Other Controls).
    private static readonly UiIcon[] ModeIcons = [UiIcons.Folder, UiIcons.Group, UiIcons.Favorites, UiIcons.OnlineLibrary];

    // The sort drop-down's icons, in FileOrder's order; the direction stays in the text (Iteration 11).
    private static readonly UiIcon[] SortIcons = [UiIcons.Date, UiIcons.ByName, UiIcons.FileSize];

    // The width of a face in the People submenu of a file's tile, in logical pixels, at the tiles' 4:3.
    private const int PhotoFaceWidth = 40;

    private readonly TableLayoutPanel _content = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(6, 4, 6, 6) };
    private readonly TableLayoutPanel _header = new() { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label _title = new() { Text = Strings.ExplorerTitleFiles, AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button _collapse = new() { AccessibleName = Strings.ExplorerAccCollapse, Size = new Size(26, 23), AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly Button _expand = new() { AccessibleName = Strings.ExplorerAccExpand, Dock = DockStyle.Fill, Visible = false, Margin = Padding.Empty };
    private readonly TableLayoutPanel _searchRow = new() { ColumnCount = 7, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    // The views drop-down (workfiles/20261027-face-detection.md § People View): its entries in
    // ExplorerMode's order, each after its icon.
    private readonly ComboBox _modes = new() { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 3, 0) };

    // The search criteria (workfiles/20261007-search-options.md): pressed, the search looks in the
    // relative paths, in the content texts; both at start-up, never none, not remembered. Toggled by
    // hand (AutoCheck off), so the last one pressed stays pressed.
    private readonly CheckBox _byName = new() { AccessibleName = Strings.ExplorerAccName, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 3, 0), Checked = true, AutoCheck = false };
    private readonly CheckBox _byContent = new() { AccessibleName = Strings.ExplorerSectionContent, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 3, 0), Checked = true, AutoCheck = false };
    private readonly TextBox _search = new() { PlaceholderText = SearchPlaceholder, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _rescan = new() { AccessibleName = Strings.ExplorerAccRescan, Size = new Size(26, 23), AutoSize = true, Anchor = AnchorStyles.Right, Enabled = false };
    // One bar per pass (ContentPass, its index) — the OCR passes' (workfiles/20261013-complementary-ocr-passes.md
    // § Progress Bars), the face pass's (workfiles/20261027-face-detection.md § Face Pass), the Vision and
    // Caption passes' (workfiles/20261027-image-content-analysis.md § Passes) — each in its colour, stacked
    // in the progress row in the order the passes run; the first — the Windows pass's, blue — is the
    // scan's too. The OCR passes' show while the content search is on, the others while their own setting is.
    private static readonly Color[] PassColors =
    [
        IndexingBar.DefaultColor, Color.FromArgb(214, 0, 120), Color.FromArgb(0, 153, 153), Color.FromArgb(136, 23, 152), Color.FromArgb(202, 80, 16), Color.FromArgb(16, 124, 16), Color.FromArgb(193, 156, 0),
    ];

    private static string[] PassColorNames =>
        [Strings.ExplorerColorBlue, Strings.ExplorerColorPink, Strings.ExplorerColorTeal, Strings.ExplorerColorPurple, Strings.ExplorerColorOrange, Strings.ExplorerColorGreen, Strings.ExplorerColorGold];
    private readonly TableLayoutPanel _bars = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = PassColors.Length, Margin = Padding.Empty, Padding = Padding.Empty };
    private readonly IndexingBar[] _passBars = PassColors.Select(color => new IndexingBar { Dock = DockStyle.Fill, BarColor = color }).ToArray();

    // Each pass's files done and files it reads, as the extraction last reported them; -1: not known yet.
    private readonly (int Done, int Needed)[] _passCounts = PassColors.Select(_ => (-1, -1)).ToArray();

    /// <summary>The scan's bar: the first one, the Windows pass's.</summary>
    private IndexingBar _progress => this._passBars[0];
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private readonly Panel _captionRow = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label _caption = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private readonly Breadcrumb _breadcrumb = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _listHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, BorderStyle = BorderStyle.FixedSingle };
    private readonly ThumbnailGrid _grid = new() { Dock = DockStyle.Fill };
    private readonly TableLayoutPanel _sizeRow = new() { ColumnCount = 9, RowCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Label _smallerGlyph = new() { Text = "▭", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 0, 4, 0) };
    // No ticks: the thumb then sits on the row's centre line, level with the glyphs at any DPI.
    private readonly TrackBar _size = new() { AutoSize = false, Dock = DockStyle.Fill, Margin = Padding.Empty, Minimum = ThumbnailGrid.MinTileSize, Maximum = ThumbnailGrid.MaxTileSize, TickStyle = TickStyle.None, SmallChange = 20, LargeChange = 100 };
    private readonly Label _largerGlyph = new() { Text = "▭", AutoSize = true, Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(4, 0, 0, 0) };

    // The sort drop-down (workfiles/20261009-search-results-sort.md), at the end of the slider's row:
    // its entries in FileOrder's order.
    private readonly ComboBox _sort = new() { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, Anchor = AnchorStyles.Right, Margin = new Padding(8, 0, 0, 0) };

    // The view buttons (workfiles/20261009-explorer-details-view.md), after the sort drop-down: the
    // current view's pressed, toggled by hand (AutoCheck off) so one is always pressed.
    private readonly CheckBox _thumbnailsView = new() { AccessibleName = Strings.ExplorerAccThumbnails, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Right, Margin = new Padding(6, 0, 0, 0), AutoCheck = false, Checked = true };
    private readonly CheckBox _detailsView = new() { AccessibleName = Strings.ExplorerAccDetails, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Right, Margin = new Padding(2, 0, 0, 0), AutoCheck = false };
    private readonly CheckBox _folderGroupsButton = new() { AccessibleName = Strings.ExplorerAccFolderGroups, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Right, Margin = new Padding(6, 0, 0, 0), AutoCheck = false, Checked = true };
    private readonly CheckBox _seriesButton = new() { AccessibleName = Strings.ExplorerAccSeries, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Right, Margin = new Padding(2, 0, 0, 0), AutoCheck = false, Checked = true };
    private readonly CheckBox _searchGroupsButton = new() { AccessibleName = Strings.ExplorerAccSearchGroups, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(26, 23), Anchor = AnchorStyles.Right, Margin = new Padding(2, 0, 0, 0), AutoCheck = false, Checked = true };
    private readonly Panel _invite = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _inviteText = new()
    {
        Dock = DockStyle.Top,
        TextAlign = ContentAlignment.MiddleLeft,
        Text = FolderInviteText,
    };
    private readonly Button _chooseFolder = new() { Text = Strings.ExplorerChooseFolder, Dock = DockStyle.Top, AutoSize = true };
    private readonly ContextMenuStrip _menu = new();

    // The URL zone (workfiles/20261009-url-content.md), the last row: its media dragged like a tile.
    private readonly UrlZone _urlZone = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly ToolStripMenuItem _openLocation = new(OpenFileLocationText);

    // A file tile's copies (see workfiles/20261008-explorer-tile-copy-menu.md), hidden on a folder tile.
    private readonly ToolStripMenuItem _copy = new(CopyText) { ShortcutKeyDisplayString = "Ctrl+C" };
    private readonly ToolStripMenuItem _copyPng = new(CopyPngText);

    // Any tile's full path, as text (see workfiles/20261014-copy-full-path.md), a folder's too.
    private readonly ToolStripMenuItem _copyFullPath = new(CopyFullPathText);
    private readonly ToolStripSeparator _copySeparator = new();
    private readonly ToolStripMenuItem _editDescription = new(Strings.ExplorerMenuEditDescription);

    // A face group's entries, and a file's inside a group (workfiles/20261027-face-detection.md § Decisions).
    private readonly ToolStripMenuItem _openGroupItem = new(Strings.ExplorerMenuOpen);
    private readonly ToolStripMenuItem _renameGroup = new(Strings.ExplorerMenuRename) { ShortcutKeyDisplayString = "F2" };
    private readonly ToolStripMenuItem _hideGroup = new(Strings.ExplorerMenuHide);
    private readonly ToolStripMenuItem _unhideGroup = new(Strings.ExplorerMenuUnhide);
    private readonly ToolStripMenuItem _unmergeGroup = new(Strings.ExplorerMenuUnmerge);
    private readonly ToolStripMenuItem _notThisPerson = new(Strings.ExplorerMenuNotThisPerson);

    // The decisions taking something away confirmed by their submenu's single item, never done in one
    // click (workfiles/20261028-open-people-from-image.md § Safer People Decisions).
    private readonly ToolStripMenuItem _hideConfirm = new(Strings.ExplorerMenuHide);
    private readonly ToolStripMenuItem _unmergeConfirm = new(Strings.ExplorerMenuUnmerge);
    private readonly ToolStripMenuItem _notConfirm = new(Strings.ExplorerMenuRemove);
    private readonly ToolStripSeparator _peopleSeparator = new();

    // A file's people, each opening its group, and the title of the group open, clicked to rename it
    // (workfiles/20261028-open-people-from-image.md).
    private readonly ToolStripMenuItem _photoPeople = new(Strings.ExplorerPeople);
    private readonly ToolStripSeparator _photoPeopleSeparator = new();
    private readonly Label _groupTitle = new() { Dock = DockStyle.Top, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand, Visible = false, Padding = new Padding(6, 0, 6, 0) };
    private readonly ToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _transient = new() { Interval = TransientDuration };
    private readonly Favorites _favorites = new(Favorites.DefaultPath);
    private readonly People _peopleStore = new(People.DefaultPath);

    private bool _open = true;
    private int _openWidth = DefaultOpenWidth;
    private int _tileSize = ThumbnailGrid.DefaultTileSize;
    private bool _syncingSize;
    private string? _baseFolder;
    private FileIndex? _index;
    private CancellationTokenSource? _scan;
    private string _summary = Strings.ExplorerNoBaseFolder;
    private bool _summaryError;

    // The content texts of the base folder's files, once loaded; whether the content search is on (the
    // ⚙ setting); the extraction running, cancelled with the scan; whether the index came from a scan,
    // with its stamps; whether a scan runs; the last keystroke in the search box (Environment.TickCount64,
    // read by the extraction's worker); what the extraction does, after the summary.
    private ContentIndex? _contents;
    private bool _contentSearch;
    private CancellationTokenSource? _extraction;
    private bool _indexScanned;
    private bool _scanning;
    private long _lastKeystroke;
    private string? _activity;
    private int _pagesPerLoad = DefaultPagesPerLoad;

    // What the status line says after the activity (workfiles/20261013-complementary-ocr-passes.md § RapidOCR Download).
    private string? _engineActivity;
    private FileOrder _fileOrder = FileOrder.Newest;
    private ExplorerView _view = ExplorerView.Thumbnails;

    // The sizes of the files the index does not give — a favorite elsewhere, say — read from the disk
    // once per list, for the Details view.
    private readonly Dictionary<string, long?> _diskSizes = new(StringComparer.OrdinalIgnoreCase);

    // A tile of this panel being dragged: not a source of favorites, so the panel refuses it.
    private bool _draggingOwnTile;
    private bool _dropFrame;

    // The whole list the search box asks for, computed once, and how many of its tiles are loaded.
    private IReadOnlyList<ExplorerRow> _list = [];
    private int _loaded;

    // The folder view (workfiles/20260930-file-explorer-folder-view.md): whether it shows, the open
    // folder relative to the base folder ("" for the base folder itself), the index's folders once
    // derived, and the list being built — a later refresh making an earlier folder read stale.
    private bool _folderView = true;
    private string _openFolder = "";

    // The groups (workfiles/20261103-file-explorer-groups.md): the folder view's, the search results', and
    // the name groups turned off in the ⚙ menu, by id.
    private bool _folderGroups = true;
    private bool _searchGroups = true;
    private IReadOnlySet<string> _patternsOff = new HashSet<string>(StringComparer.Ordinal);
    private FolderTree? _tree;
    private int _listVersion;
    private bool _clearingSearch;
    private bool _syncingToggle;

    // The People view (workfiles/20261027-face-detection.md): whether it shows, the group open in it —
    // HiddenGroups for the hidden ones, LowQualityFaces for the low-quality faces — the faces found, whether
    // they are looked for (the ⚙ setting) and how strictly they are grouped, the groups as last computed and
    // whether a computation runs or waits, the index's entries by full path, the face engine's download, and
    // what the invitation's button does.
    private const string HiddenGroups = "hidden";
    private const string LowQualityFaces = "low-quality";
    private const string GroupDataFormat = "ImageGridFusion.FaceGroup";
    private const string FaceDataFormat = "ImageGridFusion.Face";
    private bool _peopleView;
    private string? _openGroup;
    private string? _openGroupFace;
    private FaceIndex? _faces;
    private bool _faceAnalysis;
    private int _faceGrouping = FaceGrouping.DefaultLevel;
    private PeopleSnapshot _snapshot = PeopleSnapshot.Empty;
    private bool _grouping;
    private bool _groupAgain;
    private bool _facePassRunning;
    private int _groupedAt = -1;
    private Dictionary<string, IndexEntry>? _entriesByPath;

    // The person just created for a face alone, opened once the groups computed again hold it.
    private string? _openAfterRegroup;
    private InviteAction _inviteAction = InviteAction.ChooseFolder;

    // The image content analysis (workfiles/20261027-image-content-analysis.md): the embeddings, the ⚙
    // settings, the engines' downloads; the text model loaded for the searches and released when unused;
    // the query whose embedding is known, and the one being computed; the image a similar images search
    // started from. How far a result must stand out (the ⚙ level), the vocabulary's concepts the texts' results
    // are read against, and each image's spread over them, by its embedding
    // (workfiles/20261029-similar-images-accuracy.md).
    private VisionIndex? _vision;
    private bool _visualAnalysis;
    private int _similarImages = VisionSimilarity.DefaultLevel;
    private IReadOnlyList<float[]>? _concepts;
    private readonly ConditionalWeakTable<float[], StrongBox<VisionSimilarity.Spread>> _spreads = new();
    private bool _captions;
    private readonly object _textEncoderLock = new();
    private TextEncoder? _textEncoder;
    private readonly System.Windows.Forms.Timer _textEncoderIdle = new() { Interval = 60 * 1000, Enabled = true };
    private long _textEncoderUsedAt;
    private (string Query, float[] Embedding)? _queryEmbedding;
    private string? _queryPending;
    private SimilarSource? _similarTo;
    private readonly Button _similarClear = new() { AccessibleName = Strings.ExplorerAccClear, Dock = DockStyle.Right, Width = 22, FlatStyle = FlatStyle.Flat, Visible = false, Margin = Padding.Empty };
    private readonly ToolStripMenuItem _findSimilar = new(Strings.ExplorerMenuFindSimilar);

    public FileExplorerPanel()
    {
        _title.Font = new Font(Font, FontStyle.Bold);
        this._groupTitle.Font = new Font(this.Font.FontFamily, this.Font.SizeInPoints + 4f, FontStyle.Bold);
        _smallerGlyph.Font = new Font("Segoe UI Symbol", Font.SizeInPoints);
        _largerGlyph.Font = new Font("Segoe UI Symbol", Font.SizeInPoints + 6f);
        ApplyWidth();

        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, IndexingBar.Thickness));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        this._content.RowStyles.Add(new RowStyle(SizeType.Absolute, UrlZone.LogicalHeight));
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _header.Controls.Add(_title, 0, 0);
        _header.Controls.Add(_collapse, 1, 0);
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (ExplorerMode mode in Enum.GetValues<ExplorerMode>())
        {
            this._modes.Items.Add(mode);
        }

        // The entries' text as drawn, also what the accessibility tree reads — never the enum's name.
        this._modes.FormattingEnabled = true;
        this._modes.Format += (_, e) => e.Value = ModeText((ExplorerMode)e.ListItem!);
        this._modes.SelectedIndex = (int)this.Mode;
        this.ApplyView();
        this._searchRow.Controls.Add(this._modes, 0, 0);
        this._searchRow.Controls.Add(this._byName, 1, 0);
        this._searchRow.Controls.Add(this._byContent, 2, 0);
        this._searchRow.Controls.Add(this._giphyGifs, 3, 0);
        this._searchRow.Controls.Add(this._giphyStickers, 4, 0);
        this._searchRow.Controls.Add(this._search, 5, 0);
        this._searchRow.Controls.Add(this._rescan, 6, 0);
        _captionRow.Controls.Add(_caption);
        _captionRow.Controls.Add(_breadcrumb);
        this._captionRow.Controls.Add(this._similarClear);
        this._captionRow.Controls.Add(this._giphyAttribution);
        this._similarClear.FlatAppearance.BorderSize = 0;
        _sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        this._sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _sizeRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sizeRow.Controls.Add(_smallerGlyph, 0, 0);
        _sizeRow.Controls.Add(_size, 1, 0);
        _sizeRow.Controls.Add(_largerGlyph, 2, 0);
        this._sizeRow.Controls.Add(this._sort, 3, 0);
        this._sizeRow.Controls.Add(this._thumbnailsView, 4, 0);
        this._sizeRow.Controls.Add(this._detailsView, 5, 0);
        this._sizeRow.Controls.Add(this._folderGroupsButton, 6, 0);
        this._sizeRow.Controls.Add(this._seriesButton, 7, 0);
        this._sizeRow.Controls.Add(this._searchGroupsButton, 8, 0);
        this._thumbnailsView.Font = new Font("Segoe UI Symbol", this.Font.SizeInPoints);
        this._detailsView.Font = new Font("Segoe UI Symbol", this.Font.SizeInPoints);
        this._sort.Items.AddRange([SortNewestText, SortNameText, SortSmallestText]);
        this._sort.SelectedIndex = (int)this._fileOrder;
        this._sort.DrawItem += (_, e) =>
        {
            if (e.Index >= 0)
            {
                UiIcons.DrawItem(this._sort, e, SortIcons[e.Index], (string)this._sort.Items[e.Index]!);
            }
        };

        // Docked to the top in reverse order of addition: the text above the link — the GIPHY invitation's — above the button.
        _invite.Controls.Add(_chooseFolder);
        this._invite.Controls.Add(this._inviteLink);
        _invite.Controls.Add(_inviteText);
        _listHost.Controls.Add(_grid);
        _listHost.Controls.Add(_invite);
        this._listHost.Controls.Add(this._groupTitle);
        _content.Controls.Add(_header, 0, 0);
        _content.Controls.Add(_searchRow, 0, 1);
        this._bars.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < this._passBars.Length; i++)
        {
            this._bars.RowStyles.Add(new RowStyle(SizeType.Absolute, IndexingBar.Thickness));
        }

        this.LayoutBars();
        _content.Controls.Add(this._bars, 0, 2);
        _content.Controls.Add(_status, 0, 3);
        _content.Controls.Add(_captionRow, 0, 4);
        _content.Controls.Add(_listHost, 0, 5);
        _content.Controls.Add(_sizeRow, 0, 6);
        this._content.Controls.Add(this._urlZone, 0, 7);
        Controls.Add(_content);
        Controls.Add(_expand);
        this._menu.Items.AddRange([this._photoPeople, this._photoPeopleSeparator, this._openGroupItem, this._renameGroup, this._hideGroup, this._unhideGroup, this._unmergeGroup, this._notThisPerson, this._peopleSeparator]);
        this._menu.Items.AddRange([this._editDescription, this._findSimilar, this._copy, this._copyPng, this._copyFullPath, this._copySeparator]);
        _menu.Items.Add(_openLocation);

        // A confirming item takes its parent's icon: it is the action itself (RULES.md § Icons).
        foreach (var (item, icon) in new (ToolStripItem, UiIcon)[]
        {
            (this._photoPeople, UiIcons.People), (this._openGroupItem, UiIcons.Group), (this._renameGroup, UiIcons.Rename),
            (this._hideGroup, UiIcons.Hide), (this._hideConfirm, UiIcons.Hide), (this._unhideGroup, UiIcons.Unhide),
            (this._unmergeGroup, UiIcons.Unmerge), (this._unmergeConfirm, UiIcons.Unmerge),
            (this._notThisPerson, UiIcons.NotThisPerson), (this._notConfirm, UiIcons.NotThisPerson),
            (this._editDescription, UiIcons.Rename), (this._findSimilar, UiIcons.FindSimilar), (this._copy, UiIcons.Copy),
            (this._copyPng, UiIcons.Png), (this._copyFullPath, UiIcons.CopyPath), (this._openLocation, UiIcons.OpenFolder),
        })
        {
            UiIcons.Set(item, icon);
        }

        UiIcons.Attach(this._menu);
        _grid.ContextMenuStrip = _menu;
        this._grid.IsFavorite = this.IsFavoritePath;
        this.InitGiphy();
        this._grid.ContentExcerpt = this.ExcerptOf;
        this._grid.ContentSpot = this.SpotOf;
        this._grid.ContentWords = this.WordsOf;
        this._grid.SizeOf = this.SizeOf;
        this._grid.FolderFiles = this.FolderFiles;

        _toolTip.SetToolTip(_collapse, Strings.ExplorerTipCollapse);
        _toolTip.SetToolTip(_expand, Strings.ExplorerTipExpand);
        _toolTip.SetToolTip(_rescan, Strings.ExplorerTipRescan);
        this._toolTip.SetToolTip(this._modes, Strings.ExplorerTipModes);
        this.ApplyCriteriaTips();
        _toolTip.SetToolTip(_smallerGlyph, Strings.ExplorerTipSmallerTiles);
        _toolTip.SetToolTip(_largerGlyph, Strings.ExplorerTipLargerTiles);
        this.ApplySortTip();
        this._toolTip.SetToolTip(this._thumbnailsView, Strings.ExplorerTipThumbnails);
        this._toolTip.SetToolTip(this._detailsView, Strings.ExplorerTipDetails);
        this._toolTip.SetToolTip(this._folderGroupsButton, Strings.ExplorerTipFolderGroups);
        this._toolTip.SetToolTip(this._searchGroupsButton, Strings.ExplorerTipSearchGroups);
        this._folderGroupsButton.Click += (_, _) => this.ChangeGroups(!this._folderGroups, this._searchGroups);
        this._searchGroupsButton.Click += (_, _) => this.ChangeGroups(this._folderGroups, !this._searchGroups);
        this._toolTip.SetToolTip(this._seriesButton, Strings.ExplorerTipSeries);
        this._seriesButton.Click += (_, _) => this.ToggleSeries();
        _collapse.Click += (_, _) => SetOpen(false);
        _expand.Click += (_, _) => SetOpen(true);
        _size.ValueChanged += (_, _) => OnSliderChanged();
        this._sort.SelectionChangeCommitted += (_, _) => this.ChangeOrder((FileOrder)this._sort.SelectedIndex);
        this._thumbnailsView.Click += (_, _) => this.ChangeView(ExplorerView.Thumbnails);
        this._detailsView.Click += (_, _) => this.ChangeView(ExplorerView.Details);
        _grid.SizeStepRequested += (_, direction) => StepSize(direction);
        _rescan.Click += (_, _) => Rescan();
        this._chooseFolder.Click += (_, _) => this.RunInvite();
        _search.TextChanged += (_, _) =>
        {
            Interlocked.Exchange(ref this._lastKeystroke, Environment.TickCount64);
            if (!_clearingSearch)
            {
                this._similarTo = null;
                RefreshRows();
            }
        };
        this._search.AllowDrop = true;
        this._search.DragEnter += (_, e) => e.Effect = this.CanFindSimilar && SimilarDropOf(e.Data) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        this._search.DragOver += (_, e) => e.Effect = this.CanFindSimilar && SimilarDropOf(e.Data) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        this._search.DragDrop += (_, e) => this.DropOnSearch(e.Data);
        this._similarClear.Click += (_, _) => this.ClearSimilar();
        this._findSimilar.Click += (_, _) =>
        {
            if (this._grid.SelectedRow is { IsFile: true } row)
            {
                _ = this.FindSimilarAsync(row.FullPath);
            }
        };
        this._textEncoderIdle.Tick += (_, _) => this.ReleaseTextEncoder();
        this._grid.IndexedContent = this.IndexedContentOf;
        this._toolTip.SetToolTip(this._similarClear, Strings.ExplorerTipBackToSearch);
        _search.KeyDown += OnSearchKeyDown;
        this._modes.SelectionChangeCommitted += (_, _) => this.ChangeMode((ExplorerMode)this._modes.SelectedIndex);
        this._modes.DrawItem += this.DrawModeItem;
        this._byName.Click += (_, _) => this.ToggleCriterion(this._byName, this._byContent);
        this._byContent.Click += (_, _) => this.ToggleCriterion(this._byContent, this._byName);
        _breadcrumb.UpRequested += (_, _) => GoUp();
        _breadcrumb.SegmentClicked += (_, depth) => OpenFolderAtDepth(depth);
        _grid.HeartClicked += (_, row) => ToggleFavorite(row);
        _grid.RowActivated += (_, row) => Activate(row);
        _grid.DragRequested += (_, row) => StartDrag(row);
        this._urlZone.DragRequested += (_, path) => this.StartUrlDrag(path);
        this._urlZone.FileActionRequested += async (_, request) => await this.RunUrlFileActionAsync(request.Path, request.Action);
        this._urlZone.ComponentsRequested += (_, need) => this.ComponentsRequested?.Invoke(this, need);
        _grid.MoreRequested += (_, _) => LoadMore();
        _grid.UpRequested += (_, _) => GoUp();
        this._grid.RenameRequested += (_, row) => this.RenameGroup(row);
        _menu.Opening += (_, e) =>
        {
            var row = _grid.RowAt(_grid.PointToClient(MousePosition));

            // A GIPHY result has no file to copy or show yet: no menu.
            e.Cancel = row is null || row.Online is not null;
            _openLocation.Text = row?.IsFolder == true ? OpenInExplorerText : OpenFileLocationText;
            bool file = row is { IsFile: true };
            bool group = row is { IsGroup: true } && !IsPeopleTile(row.GroupId);
            var shown = group ? this._snapshot.ById.GetValueOrDefault(row!.GroupId!) : null;
            this._copy.Visible = file;
            this._copyPng.Visible = file;
            this._copyFullPath.Visible = !(row?.IsGroup ?? false);
            this._copySeparator.Visible = !(row?.IsGroup ?? false);
            this._openLocation.Visible = !(row?.IsGroup ?? false);
            this._editDescription.Visible = row?.Preset is not null;
            this._findSimilar.Visible = file && this.CanFindSimilar && ContentExtractor.KindOf(row!.FullPath) is ContentKind.Image or ContentKind.Pdf;
            this._openGroupItem.Visible = row?.IsGroup == true && shown?.Parent is null;
            this._renameGroup.Visible = group;
            this._hideGroup.Visible = group && shown is { Hidden: false, Parent: null };
            this._unhideGroup.Visible = group && shown is { Hidden: true, Parent: null };
            this._unmergeGroup.Visible = group && shown?.Parent is not null;
            this._notThisPerson.Visible = file && row!.FaceKey is not null && this._openGroup is { } open && !IsPeopleTile(open);
            var opened = this._notThisPerson.Visible ? this._snapshot.ById.GetValueOrDefault(this._openGroup!) : null;
            this._notThisPerson.Text = opened is not null ? Texts.Format(Strings.ExplorerMenuNotPerson, GroupName(opened)) : Strings.ExplorerMenuNotThisPerson;
            this._notConfirm.Text = opened is not null ? Texts.Format(Strings.ExplorerMenuRemoveFrom, GroupName(opened)) : Strings.ExplorerMenuRemove;
            this._hideConfirm.Text = shown is not null ? Texts.Format(Strings.ExplorerMenuHideName, GroupName(shown)) : Strings.ExplorerMenuHide;
            this._unmergeConfirm.Text = shown is not null ? Texts.Format(Strings.ExplorerMenuUnmergeName, GroupName(shown)) : Strings.ExplorerMenuUnmerge;
            this._peopleSeparator.Visible = row?.IsGroup == true || this._notThisPerson.Visible;
            this.FillPhotoPeople(file ? row : null);
            this._photoPeopleSeparator.Visible = this._photoPeople.Visible;
        };
        this._openGroupItem.Click += (_, _) => { if (this._grid.SelectedRow is { } row) { this.Activate(row); } };
        this._renameGroup.Click += (_, _) => { if (this._grid.SelectedRow is { } row) { this.RenameGroup(row); } };
        this._hideGroup.DropDownItems.Add(this._hideConfirm);
        this._unmergeGroup.DropDownItems.Add(this._unmergeConfirm);
        this._notThisPerson.DropDownItems.Add(this._notConfirm);
        this._hideConfirm.Click += (_, _) => this.SetGroupHidden(this._grid.SelectedRow, hidden: true);
        this._unhideGroup.Click += (_, _) => this.SetGroupHidden(this._grid.SelectedRow, hidden: false);
        this._unmergeConfirm.Click += (_, _) => this.UnmergeGroup(this._grid.SelectedRow);
        this._notConfirm.Click += (_, _) => this.RemoveFace(this._grid.SelectedRow);
        this._groupTitle.Click += (_, _) => this.RenameOpenGroup();
        this._toolTip.SetToolTip(this._groupTitle, Strings.ExplorerTipClickToRename);
        this._editDescription.Click += (_, _) => this.EditDescription();
        _openLocation.Click += (_, _) => OpenLocation();
        this._copy.Click += (_, _) => this.CopySelected();
        this._copyPng.Click += async (_, _) => await this.CopySelectedPngAsync();
        this._copyFullPath.Click += (_, _) => this.CopySelectedPath();
        _transient.Tick += (_, _) => ShowSummary();

        // Files dropped anywhere on the panel, open or collapsed, become favorites (see
        // workfiles/20260930-favorites-drag-drop.md); none of its children is a drop target of its own.
        AllowDrop = true;
        _content.Paint += (_, e) => PaintDropFrame(e.Graphics);
        ApplyMetrics();
        SyncSize();
        RefreshRows();
    }

    /// <summary>The user opened or collapsed the panel.</summary>
    public event EventHandler? OpenChanged;

    /// <summary>The user changed the tile size — the slider, or the wheel over the tiles.</summary>
    public event EventHandler? TileSizeChanged;

    /// <summary>The view was switched, or another folder opened: <see cref="FolderView"/> and <see cref="OpenFolder"/> to remember.</summary>
    public event EventHandler? FolderViewChanged;

    /// <summary>The people's state took a new generation — loaded, or forgotten — no decision of the user's.</summary>
    public event EventHandler? PeopleReset;

    /// <summary>
    /// The people as the undo history reads them: replaced by every decision, put back by
    /// <see cref="RestorePeople"/> (workfiles/20261028-open-people-from-image.md § Undo).
    /// </summary>
    public PeopleState PeopleState { get; private set; } = PeopleState.Empty;

    /// <summary>The user picked another order in the sort drop-down: <see cref="FileOrder"/> to remember.</summary>
    public event EventHandler? FileOrderChanged;

    /// <summary>The user switched the view with its buttons: <see cref="View"/> to remember.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>The user turned the folder groups or the search groups on or off with their buttons: to remember.</summary>
    public event EventHandler? GroupsChanged;

    /// <summary>The user turned the series on or off with their button: <see cref="PatternsOff"/> to remember.</summary>
    public event EventHandler? PatternsOffChanged;

    /// <summary>
    /// Whether the folder view shows its sections — the folder prefix groups, the other folders, the named
    /// files, the name patterns' groups; the list shown is made again at once. Setting it raises nothing.
    /// See workfiles/20261103-file-explorer-groups.md.
    /// </summary>
    [DefaultValue(true)]
    public bool FolderGroups
    {
        get => this._folderGroups;
        set
        {
            if (value == this._folderGroups)
            {
                return;
            }

            this._folderGroups = value;
            this._folderGroupsButton.Checked = value;
            this.RefreshRows(keepPlace: true);
        }
    }

    /// <summary>
    /// Whether the search results show their sections, Path then Content; the list shown is made again
    /// at once. Setting it raises nothing.
    /// </summary>
    [DefaultValue(true)]
    public bool SearchGroups
    {
        get => this._searchGroups;
        set
        {
            if (value == this._searchGroups)
            {
                return;
            }

            this._searchGroups = value;
            this._searchGroupsButton.Checked = value;
            this.RefreshRows(keepPlace: true);
        }
    }

    /// <summary>
    /// The name groups turned off, by id (<see cref="NameGroup.Id"/>, <see cref="NamePatterns.SeriesId"/>,
    /// <see cref="NamePatterns.DetectedSourcesId"/>) — the ⚙ menu's File explorer groups: their files go to the
    /// next group they fit, else among the named files. The list shown is made again at once.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlySet<string> PatternsOff
    {
        get => this._patternsOff;
        set
        {
            if (value.SetEquals(this._patternsOff))
            {
                return;
            }

            this._patternsOff = new HashSet<string>(value, StringComparer.Ordinal);
            this._seriesButton.Checked = !this._patternsOff.Contains(NamePatterns.SeriesId);
            this.RefreshRows(keepPlace: true);
        }
    }

    /// <summary>A tile was double-clicked or entered: its file, to be added like Add images.</summary>
    public event EventHandler<string>? FileActivated;

    /// <summary>An effect preset's tile double-clicked or entered: its file and its effects (<see cref="PresetDataFormat"/>'s text), added like Add images.</summary>
    public event EventHandler<(string Path, string Effects)>? PresetActivated;

    /// <summary>
    /// The drag format carrying an effect preset's effects beside its file, as the JSON text
    /// <c>LookPreset.ToJson</c> wrote: a drop into a cell reads it (<see cref="PresetLookOf"/>).
    /// </summary>
    public const string PresetDataFormat = "ImageGridFusion.EffectPreset";

    /// <summary>The effects a dropped preset tile carries; null for any other drop, or effects that cannot be read.</summary>
    public static ImageLook? PresetLookOf(IDataObject? data) =>
        data?.GetDataPresent(PresetDataFormat) == true && data.GetData(PresetDataFormat) is string effects ? PresetLookOf(effects) : null;

    /// <summary>The look effects saved as JSON text give; null when they cannot be read.</summary>
    public static ImageLook? PresetLookOf(string effects)
    {
        try
        {
            return System.Text.Json.Nodes.JsonNode.Parse(effects) is System.Text.Json.Nodes.JsonObject json ? LookPreset.FromJson(json) : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the effects <paramref name="look"/> holds as a preset of <paramref name="fullPath"/>, the
    /// newest favorite, described by <paramref name="description"/>; the status line says so.
    /// </summary>
    public void AddPreset(string fullPath, ImageLook look, string description)
    {
        string? error = null;
        try
        {
            this._favorites.AddPreset(fullPath, EffectPreset.New(description, LookPreset.ToJson(look)));
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            error = ex.Message;
        }

        if (this.ShowingFavorites)
        {
            this.RefreshRows(keepPlace: true);
        }

        string name = Path.GetFileName(fullPath);
        this.Report(error is null ? Texts.Format(Strings.ExplorerPresetSaved, name) : Texts.Format(Strings.ExplorerPresetSavedNotWritten, error), error is not null);
    }

    /// <summary>The invitation's button: the base folder is to be chosen, as from the ⚙ menu.</summary>
    public event EventHandler? ChooseFolderRequested;

    /// <summary>A message the collapsed panel cannot show, its status line hidden: for the window's status line.</summary>
    public event EventHandler<(string Text, bool Error)>? MessageWhileCollapsed;

    /// <summary>Escape in the search box: the focus is to go back to the grid, the search kept.</summary>
    public event EventHandler? SearchEscaped;

    /// <summary>Whether the panel is open, or collapsed to its strip. Setting it raises nothing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Open
    {
        get => _open;
        set
        {
            if (value == _open)
            {
                return;
            }

            _open = value;
            SuspendLayout();
            _content.Visible = value;
            _expand.Visible = !value;
            ApplyWidth();
            ResumeLayout();
        }
    }

    /// <summary>
    /// The panel's width while open, in logical pixels — the user's, dragged from the panel's edge:
    /// the window sets it when the splitter stops, and it is applied again when the panel reopens or
    /// the DPI changes. Setting it raises nothing; <see cref="MinOpenWidth"/> at least.
    /// </summary>
    [DefaultValue(DefaultOpenWidth)]
    public int OpenWidth
    {
        get => _openWidth;
        set
        {
            value = Math.Max(MinOpenWidth, value);
            if (value == _openWidth)
            {
                return;
            }

            _openWidth = value;
            ApplyWidth();
        }
    }

    /// <summary>
    /// The tile size in logical pixels, <see cref="ThumbnailGrid.MinTileSize"/> to
    /// <see cref="ThumbnailGrid.MaxTileSize"/>: the width at which one more tile fits on a row, the
    /// tiles stretched to fill it. Setting it raises nothing.
    /// </summary>
    [DefaultValue(ThumbnailGrid.DefaultTileSize)]
    public int TileSize
    {
        get => _tileSize;
        set
        {
            value = Math.Clamp(value, ThumbnailGrid.MinTileSize, ThumbnailGrid.MaxTileSize);
            if (value == _tileSize)
            {
                return;
            }

            _tileSize = value;
            _grid.TileSize = value;
            SyncSize();
        }
    }

    /// <summary>
    /// How many pages of tiles a load holds, <see cref="MinPagesPerLoad"/> to
    /// <see cref="MaxPagesPerLoad"/> — a page being what the list shows at once. Applies from the next
    /// load. Setting it raises nothing.
    /// </summary>
    [DefaultValue(DefaultPagesPerLoad)]
    public int PagesPerLoad
    {
        get => _pagesPerLoad;
        set => _pagesPerLoad = Math.Clamp(value, MinPagesPerLoad, MaxPagesPerLoad);
    }

    /// <summary>
    /// The sort drop-down's order of the files: inside each relevance tier of a search, alone for
    /// <c>*</c> and a browsed folder; the list shown is ordered again at once. Setting it raises nothing.
    /// </summary>
    [DefaultValue(FileOrder.Newest)]
    public FileOrder FileOrder
    {
        get => this._fileOrder;
        set
        {
            if (value == this._fileOrder || !Enum.IsDefined(value))
            {
                return;
            }

            this._fileOrder = value;
            this._sort.SelectedIndex = (int)value;
            this.ApplySortTip();
            this.RefreshOrdered();
        }
    }

    /// <summary>
    /// How the lists show — tiles, or a row per file with its size and the words a search found in its
    /// content — in every view; the list shown is laid out again at once. Setting it raises nothing.
    /// </summary>
    [DefaultValue(ExplorerView.Thumbnails)]
    public ExplorerView View
    {
        get => this._view;
        set
        {
            if (value == this._view || !Enum.IsDefined(value))
            {
                return;
            }

            this._view = value;
            this._thumbnailsView.Checked = value == ExplorerView.Thumbnails;
            this._detailsView.Checked = value == ExplorerView.Details;
            this._grid.View = value;
        }
    }

    /// <summary>
    /// How the light bulb's arrow to the word found by the OCR is drawn — the ⚙ menu's OCR result
    /// style; the tiles shown are painted again at once. Setting it raises nothing.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public OcrArrowStyle ArrowStyle
    {
        get => this._grid.ArrowStyle;
        set => this._grid.ArrowStyle = value;
    }

    /// <summary>
    /// Whether the files' content texts are extracted — the ⚙ menu's Search file contents (OCR): on, the
    /// extraction runs after each scan; turned off, it stops, the texts already extracted kept and
    /// still searched. Setting it raises nothing.
    /// </summary>
    [DefaultValue(false)]
    public bool ContentSearch
    {
        get => this._contentSearch;
        set
        {
            if (value == this._contentSearch)
            {
                return;
            }

            // Started again either way: the face pass may run without the OCR passes.
            this._contentSearch = value;
            this.StopExtraction();
            this.StartExtraction();
            this.ApplyCriteriaTips();
            this.LayoutBars();
            this.ApplyMetrics();
        }
    }

    /// <summary>Whether the content texts can be rebuilt: the content search on, a base folder set.</summary>
    public bool CanRebuildContent => this._contentSearch && this._baseFolder is not null;

    /// <summary>
    /// Forgets every content text and extracts them all again — after an OCR language was installed,
    /// for instance: the folder rescanned first, the list of files keeping the priority.
    /// </summary>
    public void RebuildContentIndex()
    {
        if (!this.CanRebuildContent)
        {
            return;
        }

        this.StopExtraction();
        if (this._contents is { } contents)
        {
            contents.Clear();
        }
        else
        {
            this._contents = new ContentIndex(this._baseFolder!);
        }

        this.RescanBase();
    }

    /// <summary>A feature of the panel needs components it lacks — the URL zone's yt-dlp: the Component updates window to open on them.</summary>
    public event EventHandler<ComponentNeed>? ComponentsRequested;

    /// <summary>
    /// Before the engines' files are swapped: the content passes stopped — a pass running closes its sessions — and the
    /// text model released (workfiles/20261102-component-update-verification.md § Updating). <see cref="EnginesChanged"/>
    /// starts them again.
    /// </summary>
    public void SuspendEngines()
    {
        this.StopExtraction();
        this.ReleaseTextEncoder(force: true);
    }

    /// <summary>
    /// The engines' files changed — installed, updated, put back — or their swap is over: the bars' tooltips, the content
    /// passes started again with what is there, the People view refreshed.
    /// </summary>
    public void EnginesChanged()
    {
        if (this.IsDisposed)
        {
            return;
        }

        this.ApplyBarTips();
        this.StopExtraction();
        this.StartExtraction();
        this.RefreshPeopleIfShown();
    }

    /// <summary>
    /// What the list shows — the views drop-down's: the search view — the favorites, <c>*</c> or the
    /// matches — the folder view — the open folder's subfolders and files — or the People view — the
    /// face groups, then the faces close to no other. Setting it raises nothing.
    /// </summary>
    [DefaultValue(ExplorerMode.Folders)]
    public ExplorerMode Mode
    {
        get => this._giphyView ? ExplorerMode.Giphy : this._peopleView ? ExplorerMode.People : this._folderView ? ExplorerMode.Folders : ExplorerMode.Favorites;
        set => this.SetMode(value);
    }

    /// <summary>The view set as <see cref="Mode"/> does, <paramref name="select"/> selected once listed.</summary>
    private void SetMode(ExplorerMode value, string? select = null)
    {
        if (value == this.Mode || !Enum.IsDefined(value))
        {
            return;
        }

        // Leaving the folder view leaves the source it shows: its run paused (§ 🔄 Inside a Source).
        if (value != ExplorerMode.Folders)
        {
            this.PauseSourceRun();
        }

        this._folderView = value == ExplorerMode.Folders;
        this._peopleView = value == ExplorerMode.People;
        this._giphyView = value == ExplorerMode.Giphy;
        this._giphyListShown = false;
        this._openGroup = null;
        this._similarTo = null;
        this._syncingToggle = true;
        try
        {
            this._modes.SelectedIndex = (int)value;
        }
        finally
        {
            this._syncingToggle = false;
        }

        this.ApplyView();
        this.RefreshRows(select: select);
    }

    /// <summary>Whether the folder view shows.</summary>
    public bool FolderView => this._folderView;

    /// <summary>
    /// Whether the faces are looked for in the indexed images and PDFs and grouped into people — the ⚙
    /// menu's Face analysis, off by default: on, the face pass runs with the extraction, once the face
    /// engine is there; turned off, it stops, the faces already found kept. Setting it raises nothing.
    /// See workfiles/20261027-face-detection.md § Privacy.
    /// </summary>
    [DefaultValue(false)]
    public bool FaceAnalysis
    {
        get => this._faceAnalysis;
        set
        {
            if (value == this._faceAnalysis)
            {
                return;
            }

            this._faceAnalysis = value;
            this.StopExtraction();
            this.StartExtraction();
            this.LayoutBars();
            this.ApplyMetrics();
            if (this._peopleView)
            {
                this.RefreshRows(keepPlace: true);
            }
        }
    }

    /// <summary>
    /// How strictly the faces are grouped — a level of <see cref="FaceGrouping.Thresholds"/>, the ⚙ menu's
    /// Face grouping: set, the groups are computed again at once. Setting it raises nothing. See
    /// workfiles/20261028-face-grouping-accuracy.md § Strictness Setting.
    /// </summary>
    [DefaultValue(FaceGrouping.DefaultLevel)]
    public int FaceGroupingLevel
    {
        get => this._faceGrouping;
        set
        {
            if (value == this._faceGrouping)
            {
                return;
            }

            this._faceGrouping = value;
            this.Regroup(force: true);
        }
    }

    /// <summary>The People view's invitation was answered: the face analysis to turn on, as from the ⚙ menu.</summary>
    public event EventHandler? FaceAnalysisRequested;

    /// <summary>The People view's invitation was answered: the face engine to install, from the Component updates window.</summary>
    public event EventHandler? FaceEngineRequested;

    /// <summary>
    /// Forgets every face found and every decision on the people — <c>files.faces</c> and
    /// <c>people.json</c> deleted — the extraction going on without the face pass, which the caller turns
    /// off. The status line says what could not be deleted. See workfiles/20261027-face-detection.md § Privacy.
    /// </summary>
    public void ForgetFaces()
    {
        this.StopExtraction();
        this._faces?.Clear();
        string? error = null;
        try
        {
            FaceIndex.Delete(FaceIndex.DefaultPath);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            error = ex.Message;
        }

        // The secondary sources' faces too: they are in the People view.
        error ??= this.ForgetSourceFaces();

        try
        {
            this._peopleStore.Clear();
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            error ??= ex.Message;
        }

        this.ResetPeopleState();
        this._snapshot = PeopleSnapshot.Empty;
        this._openGroup = null;
        this.Regroup();
        this.StartExtraction();
        this.RefreshPeopleIfShown();
        this.Report(error is null ? Strings.ExplorerFacesForgotten : Texts.Format(Strings.ExplorerFacesForgottenPartly, error), error is not null);
    }

    /// <summary>
    /// The folder the folder view shows, relative to the base folder — "" for the base folder itself, and
    /// while a secondary source is open: a source is never remembered, its tile being at the base folder's root.
    /// Set before <see cref="Start"/> to the remembered one; one gone from the disk gives way to its
    /// nearest parent still there when listed. Setting it raises nothing.
    /// </summary>
    [DefaultValue("")]
    public string OpenFolder
    {
        get => this._openSource is null ? _openFolder : "";
        set
        {
            string folder = Path.TrimEndingDirectorySeparator(value ?? "");
            if (folder == _openFolder && this._openSource is null)
            {
                return;
            }

            this.SetOpenSource(null);
            _openFolder = folder;
            if (_folderView)
            {
                RefreshRows();
            }
        }
    }

    /// <summary>Whether <paramref name="screenPoint"/> is over the panel, open or collapsed.</summary>
    public bool ContainsScreenPoint(Point screenPoint) => Visible && RectangleToScreen(ClientRectangle).Contains(screenPoint);

    /// <summary>
    /// Frames the panel while a drop of favorites hovers it — a drag over the panel, or a cell's ✥
    /// handle driven by the window. The preview's drop-target highlight, not a helper indicator.
    /// </summary>
    public void ShowDropFrame(bool shown)
    {
        if (shown == _dropFrame)
        {
            return;
        }

        _dropFrame = shown;
        _content.Invalidate();
        _expand.UseVisualStyleBackColor = !shown;
        _expand.BackColor = shown ? SystemColors.Highlight : SystemColors.Control;
        _expand.ForeColor = shown ? SystemColors.HighlightText : SystemColors.ControlText;
    }

    /// <summary>
    /// Adds dropped files to the favorites, in their order, the last one the newest; a folder is
    /// skipped. The favorites list keeps its place, a search stays; the status line says what was added.
    /// </summary>
    public void AddFavorites(IReadOnlyList<string> paths)
    {
        var files = new List<string>();
        int folders = 0;
        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                folders++;
            }
            else if (File.Exists(path))
            {
                files.Add(Path.GetFullPath(path));
            }
        }

        if (files.Count == 0)
        {
            Report(folders > 0 ? Strings.ExplorerNothingAddedFolders : Strings.ExplorerNothingAddedNoFile, error: false);
            return;
        }

        string? error = null;
        try
        {
            _favorites.Add(files);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            error = ex.Message;
        }

        if (ShowingFavorites)
        {
            RefreshRows(keepPlace: true);
        }
        else
        {
            _grid.Invalidate();
        }

        string added = files.Count == 1 ? Path.GetFileName(files[0]) : ThumbnailGrid.FileCount(files.Count);
        string skipped = folders switch { 0 => "", 1 => Strings.ExplorerSkippedFolderOne, _ => Texts.Format(Strings.ExplorerSkippedFolders, folders) };
        Report(error is null ? Texts.Format(Strings.ExplorerAddedToFavorites, added, skipped) : Texts.Format(Strings.ExplorerAddedToFavoritesNotSaved, added, error), error is not null);
    }

    /// <summary>The search box has the focus: its keys are its own, not the window's shortcuts.</summary>
    public bool IsEditingText => _search.Focused || this._urlZone.IsEditingText;

    /// <summary>The tiles have the focus: Ctrl+C is theirs, <see cref="CopySelected"/>, not the grid's copy.</summary>
    public bool TilesFocused => this._grid.Focused;

    /// <summary>
    /// Copy: the selected file tile's file on the clipboard, as Ctrl+C in the Explorer puts it. A folder
    /// tile, or no tile, selected: nothing.
    /// </summary>
    public void CopySelected()
    {
        if (this._grid.SelectedRow is not { IsFile: true } row || !this.Exists(row))
        {
            return;
        }

        this.CopyFile(row.FullPath);
    }

    /// <summary>Copy: the file at <paramref name="path"/> on the clipboard, as Ctrl+C in the Explorer puts it.</summary>
    private void CopyFile(string path)
    {
        try
        {
            var data = new DataObject();
            data.SetFileDropList(new StringCollection { path });
            Clipboard.SetDataObject(data, copy: true);
            this.ShowTransient(Texts.Format(Strings.ExplorerCopied, Path.GetFileName(path)));
        }
        catch (ExternalException ex)
        {
            this.ShowTransient(Texts.Format(Strings.ExplorerCopyFailed, ex.Message), error: true);
        }
    }

    /// <summary>
    /// Copy PNG: the selected file tile's image on the clipboard, as a cell shows it when the file
    /// arrives — a video's frame at 10 %, a PDF's or a text's page — at its full size, no effect
    /// applied; in the grid Copy's formats.
    /// </summary>
    private async Task CopySelectedPngAsync()
    {
        if (this._grid.SelectedRow is not { IsFile: true } row || !this.Exists(row))
        {
            return;
        }

        await this.CopyPngAsync(row.FullPath);
    }

    /// <summary>Copy PNG: the image of the file at <paramref name="path"/>, as <see cref="CopySelectedPngAsync"/> copies a tile's.</summary>
    private async Task CopyPngAsync(string path)
    {
        string name = Path.GetFileName(path);
        var image = await Task.Run(() => ImageLoader.TryLoadFile(path));
        if (this.IsDisposed)
        {
            image?.Dispose();
            return;
        }

        if (image is null)
        {
            this.ShowTransient(Texts.Format(Strings.ExplorerNoImageToCopy, name), error: true);
            return;
        }

        using (image)
        {
            try
            {
                // Standard bitmap for most apps, flattened on white as it carries no transparency, plus the
                // PNG format that browsers paste more reliably, which keeps it.
                using var png = new MemoryStream();
                image.Bitmap.Save(png, ImageFormat.Png);
                using var flat = Compositor.Flattened(image.Bitmap);
                var data = new DataObject();
                data.SetImage(flat);
                data.SetData("PNG", png);
                Clipboard.SetDataObject(data, copy: true);
                this.ShowTransient(Texts.Format(Strings.ExplorerCopiedAsPng, name, image.Size.Width, image.Size.Height));
            }
            catch (ExternalException ex)
            {
                this.ShowTransient(Texts.Format(Strings.ExplorerCopyFailed, ex.Message), error: true);
            }
        }
    }

    /// <summary>
    /// Copy full path: the selected tile's full path — a file's or a folder's — on the clipboard as
    /// text, raw, without quotes. A missing one is dropped by <see cref="Exists"/>, nothing copied.
    /// </summary>
    private void CopySelectedPath()
    {
        if (this._grid.SelectedRow is not { } row || !this.Exists(row))
        {
            return;
        }

        this.CopyPath(row.FullPath);
    }

    /// <summary>Copy full path: <paramref name="path"/> on the clipboard as text, raw, without quotes.</summary>
    private void CopyPath(string path)
    {
        try
        {
            Clipboard.SetText(path);
            this.ShowTransient(Texts.Format(Strings.ExplorerCopiedPath, path));
        }
        catch (ExternalException ex)
        {
            this.ShowTransient(Texts.Format(Strings.ExplorerCopyFailed, ex.Message), error: true);
        }
    }

    /// <summary>
    /// The URL zone's preview menu (see workfiles/20261014-url-zone-context-menu.md): a file tile's
    /// entry, on the zone's downloaded file — which the zone checked is still there.
    /// </summary>
    private async Task RunUrlFileActionAsync(string path, UrlZone.FileAction action)
    {
        switch (action)
        {
            case UrlZone.FileAction.Copy:
                this.CopyFile(path);
                break;
            case UrlZone.FileAction.CopyPng:
                await this.CopyPngAsync(path);
                break;
            case UrlZone.FileAction.CopyFullPath:
                this.CopyPath(path);
                break;
            case UrlZone.FileAction.OpenFileLocation:
                this.OpenInExplorer(path, folder: false);
                break;
        }
    }

    /// <summary>
    /// Ctrl+F: the panel opened if collapsed, as its « button does, then the search box focused, its
    /// text selected (see workfiles/20261007-ctrl-f-search-focus.md). Pressed again while the search box
    /// has the focus, it moves to the view <paramref name="step"/> away in the drop-down's order — 1 for
    /// Ctrl+F, -1 for Ctrl+Shift+F — round the end, People skipped while the face analysis is off (see
    /// workfiles/20261029-ctrl-f-view-cycle.md). The folder view reached that way opens at the base folder,
    /// the folder left on its way selected, so pressing it until Folders comes back goes up to the top (see
    /// workfiles/20261102-ctrl-f-cycle-folder-root.md); the drop-down keeps the folder left.
    /// </summary>
    public void FocusSearch(int step)
    {
        if (!this._open)
        {
            this.SetOpen(true);
        }
        else if (this._search.Focused)
        {
            var mode = this.NextMode(step);
            string? select = null;
            if (mode == ExplorerMode.Folders && (this._openFolder.Length > 0 || this._openSource is not null))
            {
                // Out of a secondary source, back at the base folder's root, the source's tile selected.
                select = this._openSource is { } source ? source.Folder
                    : this._baseFolder is null ? null : Path.Combine(this._baseFolder, this.SegmentsOf(this._openFolder)[1]);
                this.SetOpenSource(null);
                this._openFolder = "";
            }

            this.ChangeMode(mode, select);
        }

        this._search.Focus();
        this._search.SelectAll();
    }

    /// <summary>
    /// Loads the favorites and the cached index of <paramref name="baseFolder"/> — usable at once —
    /// then rescans the folder in the background; null when no folder was chosen yet.
    /// </summary>
    public void Start(string? baseFolder)
    {
        _favorites.Load();
        this._peopleStore.Load();
        this.ResetPeopleState();
        SetBaseFolder(baseFolder);
    }

    /// <summary>Points the explorer at another folder, or none: its cached index is loaded, then the folder rescanned.</summary>
    public void SetBaseFolder(string? folder)
    {
        _scan?.Cancel();
        SetIndex(null, null);
        this.StopExtraction();
        this._contents = null;
        this._faces = null;
        this._vision = null;
        this._similarTo = null;
        this._snapshot = PeopleSnapshot.Empty;
        this._groupedAt = -1;
        this._openGroup = null;
        this._indexScanned = false;
        this._scanning = false;
        this._activity = null;
        this._progress.HideProgress();
        this.SetBaseRescan(false);
        string? previous = _baseFolder;
        try
        {
            _baseFolder = folder is null ? null : FileIndex.NormalizeFolder(folder);

            // Another base folder: the folder view opens at its root. The first one, at start-up, keeps the remembered folder.
            if (previous is not null && !string.Equals(previous, _baseFolder, StringComparison.OrdinalIgnoreCase) && (_openFolder.Length > 0 || this._openSource is not null))
            {
                this.SetOpenSource(null);
                _openFolder = "";
                FolderViewChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (ArgumentException)
        {
            _baseFolder = null;
            SetSummary(Texts.Format(Strings.ExplorerInvalidFolder, folder), error: true);
            RefreshRows();
            return;
        }

        if (_baseFolder is null)
        {
            SetSummary(Strings.ExplorerNoBaseFolder);
            RefreshRows();
            return;
        }

        _ = LoadAndScanAsync(_baseFolder, Restart());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scan?.Cancel();
            this.StopSources();
            this._textEncoderIdle.Dispose();
            this.ReleaseTextEncoder(force: true);
            _transient.Dispose();
            _toolTip.Dispose();
            _menu.Dispose();
            _title.Font.Dispose();
            _smallerGlyph.Font.Dispose();
            _largerGlyph.Font.Dispose();
            this._giphyCall?.Cancel();
            this._giphyFrames.Dispose();
            this._giphyThumbnails?.Dispose();
            this._giphyAttribution.Font.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        OnDragOver(e);

        // As the window does: the shell helper keeps Explorer's thumbnail over the panel.
        if (e.Effect == DragDropEffects.Copy)
        {
            e.DropImageType = DropImageType.Copy;
            e.Message = Strings.ExplorerDropAddTo;
            e.MessageReplacementToken = Strings.ExplorerModeFavorites;
        }
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);

        // The People view's own tiles: a face or a group over a group it may join.
        if (this._draggingOwnTile && this.PeopleDropTarget(e) is { } target)
        {
            e.Effect = DragDropEffects.Move;
            this._grid.DropTargetKey = target.Key;
            return;
        }

        this._grid.DropTargetKey = null;
        bool files = !_draggingOwnTile && e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
        e.Effect = files ? DragDropEffects.Copy : DragDropEffects.None;
        ShowDropFrame(files);
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        this._grid.DropTargetKey = null;
        ShowDropFrame(false);
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        ShowDropFrame(false);
        this._grid.DropTargetKey = null;
        if (this._draggingOwnTile && this.PeopleDropTarget(e) is { } target)
        {
            // Once the drag is over: a merge asks first, which a drop in progress cannot show.
            string? group = e.Data?.GetData(GroupDataFormat) as string;
            string? face = e.Data?.GetData(FaceDataFormat) as string;
            this.BeginInvoke(() => this.DropOnGroup(group, face, target));
            return;
        }

        if (!_draggingOwnTile && e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddFavorites(paths);
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyMetrics();
    }

    // The width is left to the window's own scaling: inside this handler the panel's DeviceDpi still
    // reads the old value, so setting it again here would scale it twice.
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyMetrics();
    }

    /// <summary>The panel's width, from its state: logical before the window scales its controls, device units after.</summary>
    private void ApplyWidth()
    {
        int logical = _open ? _openWidth : StripWidth;
        Width = IsHandleCreated ? LogicalToDeviceUnits(logical) : logical;
    }

    /// <summary>The buttons' icons at the DPI, emoji first (RULES.md § Icons).</summary>
    private void UpdateIcons()
    {
        int size = this.LogicalToDeviceUnits(UiIcons.LogicalSize);
        foreach (var (button, icon) in new (ButtonBase, UiIcon)[]
        {
            (this._byName, UiIcons.ByName), (this._byContent, UiIcons.Caption), (this._rescan, UiIcons.Rescan),
            (this._collapse, UiIcons.Collapse), (this._expand, UiIcons.Expand), (this._similarClear, UiIcons.Clear),
            (this._thumbnailsView, UiIcons.Thumbnails), (this._detailsView, UiIcons.Details), (this._chooseFolder, this.InviteIcon),
            (this._folderGroupsButton, UiIcons.FolderGroups), (this._seriesButton, UiIcons.Series), (this._searchGroupsButton, UiIcons.SearchGroups),
            (this._giphyGifs, UiIcons.Gif), (this._giphyStickers, UiIcons.Sticker),
        })
        {
            UiIcons.SetImage(button, icon, size);
        }
    }

    /// <summary>The rows sized to the font and the DPI: the progress bar's, the status and caption lines, the slider's row, the URL zone, the invitation.</summary>
    private void ApplyMetrics()
    {
        this.UpdateIcons();
        int line = Font.Height + LogicalToDeviceUnits(4);
        int bar = this.LogicalToDeviceUnits(IndexingBar.Thickness);
        foreach (RowStyle row in this._bars.RowStyles)
        {
            row.Height = bar;
        }

        _content.RowStyles[2].Height = bar * this.BarsShown;
        _content.RowStyles[3].Height = line;
        _content.RowStyles[4].Height = line;
        _content.RowStyles[6].Height = Font.Height * 2 + LogicalToDeviceUnits(4);
        this._content.RowStyles[7].Height = this.LogicalToDeviceUnits(UrlZone.LogicalHeight);
        this._sort.Width = this.LogicalToDeviceUnits(SortWidth);
        this._modes.Width = this.LogicalToDeviceUnits(ModesWidth);
        _inviteText.Height = Font.Height * 4 + LogicalToDeviceUnits(8);
        this._groupTitle.Height = this._groupTitle.Font.Height + this.LogicalToDeviceUnits(10);
    }

    /// <summary>The frame in the content's padding, around everything the open panel shows.</summary>
    private void PaintDropFrame(Graphics g)
    {
        if (!_dropFrame)
        {
            return;
        }

        using var pen = new Pen(SystemColors.Highlight, LogicalToDeviceUnits(3)) { Alignment = PenAlignment.Inset };
        g.DrawRectangle(pen, new Rectangle(Point.Empty, _content.ClientSize));
    }

    /// <summary>A message on the status line, else on the window's while the panel is collapsed.</summary>
    public void Report(string message, bool error)
    {
        if (_open)
        {
            ShowTransient(message, error);
        }
        else
        {
            MessageWhileCollapsed?.Invoke(this, (message, error));
        }
    }

    private void SetOpen(bool open)
    {
        Open = open;
        OpenChanged?.Invoke(this, EventArgs.Empty);
        if (open)
        {
            _search.Focus();
        }
    }

    /// <summary>The slider on the tile size, its tooltip saying it.</summary>
    private void SyncSize()
    {
        _syncingSize = true;
        try
        {
            _size.Value = _tileSize;
        }
        finally
        {
            _syncingSize = false;
        }

        _toolTip.SetToolTip(_size, Texts.Format(Strings.ExplorerTipTileSize, _tileSize));
    }

    /// <summary>The user picked an order in the sort drop-down: applied, then reported to be remembered.</summary>
    private void ChangeOrder(FileOrder order)
    {
        if (order == this._fileOrder)
        {
            return;
        }

        this.FileOrder = order;
        this.FileOrderChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A view button pressed by the user: applied, then reported to be remembered.</summary>
    private void ChangeView(ExplorerView view)
    {
        if (view == this._view)
        {
            return;
        }

        this.View = view;
        this.ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The Series button clicked: the series turned on or off, the list made again, the choice to remember.</summary>
    private void ToggleSeries()
    {
        var off = new HashSet<string>(this._patternsOff, StringComparer.Ordinal);
        if (!off.Remove(NamePatterns.SeriesId))
        {
            off.Add(NamePatterns.SeriesId);
        }

        this.PatternsOff = off;
        this.PatternsOffChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A groups button clicked: its groups turned on or off, the list made again, the choice to remember.</summary>
    private void ChangeGroups(bool folders, bool search)
    {
        this.FolderGroups = folders;
        this.SearchGroups = search;
        this.GroupsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The list shown ordered again in <see cref="FileOrder"/>, from its first load: a search, <c>*</c> or
    /// a browsed folder — the favorites keep their own order, so they are left as they are.
    /// </summary>
    private void RefreshOrdered()
    {
        if (this._folderView || (this._peopleView && this._openGroup is not null) || FileSearch.Words(this._search.Text).Length > 0)
        {
            this.RefreshRows();
        }
    }

    /// <summary>The sort drop-down's tooltip: the order chosen, and the relevance a search puts first.</summary>
    private void ApplySortTip()
    {
        string order = this._fileOrder switch
        {
            FileOrder.Name => Strings.ExplorerSortOrderName,
            FileOrder.Smallest => Strings.ExplorerSortOrderSmallest,
            _ => Strings.ExplorerSortOrderNewest,
        };
        this._toolTip.SetToolTip(this._sort, Texts.Format(Strings.ExplorerTipSort, order));
    }

    /// <summary>The slider moved: its size applied and reported.</summary>
    private void OnSliderChanged()
    {
        if (!_syncingSize)
        {
            ChangeTileSize(_size.Value);
        }
    }

    /// <summary>The wheel over the tiles: 15 % larger or smaller per notch, the same feel at every size.</summary>
    private void StepSize(int direction)
    {
        double factor = direction > 0 ? 1.15 : 1 / 1.15;
        ChangeTileSize((int)Math.Round(_tileSize * factor));
    }

    /// <summary>A size chosen by the user: applied, and reported when it changed.</summary>
    private void ChangeTileSize(int size)
    {
        int before = _tileSize;
        TileSize = size;
        if (_tileSize != before)
        {
            TileSizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private CancellationToken Restart()
    {
        _scan?.Cancel();
        _scan = new CancellationTokenSource();
        return _scan.Token;
    }

    private async Task LoadAndScanAsync(string folder, CancellationToken cancellation)
    {
        ShowStatus(Strings.ExplorerLoadingIndex);

        // The content texts load alongside: the file list shows without waiting for them
        // (workfiles/20261013-complementary-ocr-passes.md § Index Format).
        var contentsLoad = Task.Run(() => ContentIndex.Load(ContentIndex.DefaultPath, folder));
        var facesLoad = Task.Run(() => FaceIndex.Load(FaceIndex.DefaultPath, folder));
        var visionLoad = Task.Run(() => VisionIndex.Load(VisionIndex.DefaultPath, folder));
        var (loaded, tree) = await Task.Run(() =>
        {
            FileIndex.MoveLegacy();
            var index = FileIndex.Load(FileIndex.DefaultPath, folder);
            return (index, index is null ? null : FolderTree.Of(index.Entries));
        });
        if (cancellation.IsCancellationRequested || IsDisposed)
        {
            return;
        }

        SetIndex(loaded, tree);
        SetSummary(loaded is null ? Strings.ExplorerNoIndexYet : Summary(loaded));
        RefreshRows(keepPlace: true);
        var contents = await contentsLoad;
        if (cancellation.IsCancellationRequested || IsDisposed)
        {
            return;
        }

        if (this._contents is null)
        {
            this._contents = contents;
            this.RefreshContentResults();
        }

        var faces = await facesLoad;
        if (cancellation.IsCancellationRequested || IsDisposed)
        {
            return;
        }

        if (this._faces is null)
        {
            this._faces = faces;
            this.Regroup();
        }

        var vision = await visionLoad;
        if (cancellation.IsCancellationRequested || IsDisposed)
        {
            return;
        }

        if (this._vision is null)
        {
            this._vision = vision;
            this.RefreshContentResults();
        }

        await ScanAsync(folder, cancellation);
    }

    /// <summary>
    /// 🔄: inside a secondary source, that source's folder read again and its content analysed
    /// (workfiles/20261105-secondary-file-sources.md § 🔄 Inside a Source); anywhere else, the base folder rescanned.
    /// </summary>
    private void Rescan()
    {
        if (this.ViewSource is { } source)
        {
            _ = this.LoadAndScanSourceAsync(source, analyse: true);
            return;
        }

        this.RescanBase();
    }

    private void RescanBase()
    {
        if (_baseFolder is { } folder)
        {
            _ = ScanAsync(folder, Restart());
        }
    }

    /// <summary>
    /// Scans the folder off the UI thread, the progress on the status line, and takes the result over
    /// unless another scan or folder replaced this one meanwhile. The index file is written from the
    /// worker too.
    /// </summary>
    private async Task ScanAsync(string folder, CancellationToken cancellation)
    {
        this.SetBaseRescan(false);
        this._scanning = true;
        this._activity = null;
        this._progress.ShowUnknown();
        var progress = new Progress<ScanProgress>(p =>
        {
            if (!cancellation.IsCancellationRequested && !IsDisposed)
            {
                ShowStatus(p.Phase == ScanPhase.Counting ? Texts.Format(Strings.ExplorerCounting, p.Done) : Texts.Format(Strings.ExplorerIndexing, p.Done, p.Total));
                if (p.Phase == ScanPhase.Counting || p.Total == 0)
                {
                    this._progress.ShowUnknown();
                }
                else
                {
                    this._progress.ShowProgress((double)p.Done / p.Total);
                }
            }
        });
        var known = this._contents;
        var knownFaces = this._faces;
        var knownVision = this._vision;
        bool scanned = false;
        try
        {
            if (!Directory.Exists(folder))
            {
                SetSummary(Texts.Format(Strings.ExplorerFolderNotFoundNamed, folder), error: true);
                return;
            }

            var (index, tree, saveError, contents, faces, vision) = await Task.Run(
                () =>
                {
                    var scanned = FileIndex.Scan(folder, progress, cancellation);
                    string? error = null;
                    try
                    {
                        scanned.Save(FileIndex.DefaultPath);
                    }
                    catch (Exception ex) when (FileIndex.IsFileError(ex))
                    {
                        error = ex.Message;
                    }

                    // A rescan that cut the cached index's loading short loads the texts itself.
                    return (scanned, FolderTree.Of(scanned.Entries), error, known ?? ContentIndex.Load(ContentIndex.DefaultPath, folder), knownFaces ?? FaceIndex.Load(FaceIndex.DefaultPath, folder),
                        knownVision ?? VisionIndex.Load(VisionIndex.DefaultPath, folder));
                },
                cancellation);
            if (cancellation.IsCancellationRequested || IsDisposed)
            {
                return;
            }

            SetIndex(index, tree);
            this._contents ??= contents;
            this._faces ??= faces;
            this._vision ??= vision;
            this.Regroup();
            this._indexScanned = true;
            scanned = true;
            SetSummary(saveError is null ? Summary(index) : Texts.Format(Strings.ExplorerSummaryNotSaved, Summary(index), saveError), error: saveError is not null);
            RefreshRows(keepPlace: true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            if (!cancellation.IsCancellationRequested && !IsDisposed)
            {
                SetSummary(Texts.Format(Strings.ExplorerScanFailed, ex.Message), error: true);
            }
        }
        finally
        {
            if (!cancellation.IsCancellationRequested && !IsDisposed)
            {
                this.SetBaseRescan(true);
                this._scanning = false;
                this._progress.HideProgress();
            }
        }

        // The list of files first, the search needing it; then their content texts.
        if (scanned)
        {
            this.StartExtraction();
        }
    }

    /// <summary>
    /// Starts extracting the content texts the scanned index lacks, when the content search is on —
    /// cancelled with the scan, so a rescan, another folder or closing stops it — any extraction
    /// running stopped first.
    /// </summary>
    private void StartExtraction()
    {
        // A secondary source's run goes first: the base folder's waits for it (workfiles/20261105-secondary-file-sources.md).
        if (!(this._contentSearch || this._faceAnalysis || this._visualAnalysis || this._captions) || this._sourceRun is not null || !this._indexScanned || this._scanning || this._index is not { } index
            || this._contents is not { } contents || this._faces is not { } faces || this._vision is not { } vision || this._scan is not { IsCancellationRequested: false } scan)
        {
            return;
        }

        this.StopExtraction();
        this._extraction = CancellationTokenSource.CreateLinkedTokenSource(scan.Token);
        var passes = new ExtractionSettings(this._contentSearch, this._faceAnalysis, this._visualAnalysis, this._captions);
        _ = this.ExtractAsync(index, contents, faces, vision, IndexFiles.Main, passes, this._extraction.Token);
    }

    /// <summary>The base folder's passes stopped, and a secondary source's run paused.</summary>
    private void StopExtraction()
    {
        this._extraction?.Cancel();
        this._extraction = null;
        this.PauseSourceRun();
    }

    /// <summary>
    /// Extracts the content texts off the UI thread, pass after pass, one file at a time: each pass's
    /// bar and the status line following it, the search shown refreshed every few seconds and at the end.
    /// </summary>
    private async Task ExtractAsync(FileIndex index, ContentIndex contents, FaceIndex faces, VisionIndex vision, IndexFiles files, ExtractionSettings passes, CancellationToken cancellation)
    {
        bool finished = false;
        var progress = new Progress<ExtractionProgress>(p =>
        {
            if (finished || cancellation.IsCancellationRequested || this.IsDisposed)
            {
                return;
            }

            bool facePass = p.Pass == ContentPass.Faces;
            if (p.Refresh)
            {
                if (facePass)
                {
                    this.Regroup();
                }
                else
                {
                    this.RefreshContentResults();
                }

                return;
            }

            int bar = (int)p.Pass;
            this._passCounts[bar] = (p.PassDone, p.PassNeeded);
            if (p.Finished)
            {
                this._passBars[bar].HideProgress();
            }
            else
            {
                this._passBars[bar].ShowProgress(p.PassNeeded == 0 ? 1 : (double)p.PassDone / p.PassNeeded);
            }

            this.ApplyBarTips();
            if (p.Note is { } note)
            {
                this.ShowTransient(note, error: true);
            }

            // The pass, the file's rank in it, then its name — none for a file that has no text to find.
            string doing = ContentPasses.IsOcr(p.Pass)
                ? Texts.Format(Strings.ExplorerOcrProgress, Array.IndexOf(ContentPasses.Ocr, p.Pass) + 1, ContentPasses.Ocr.Length, ContentPasses.NameOf(p.Pass), p.Done + 1, p.Total)
                : Texts.Format("{0}: {1:N0}/{2:N0}", ContentPasses.NameOf(p.Pass), p.Done + 1, p.Total);
            this.SetActivity(p.File is null ? null
                : p.Kind == ContentKind.None ? doing
                : $"{doing} ({Path.GetFileName(p.File)})");
            if (facePass && p.Finished != !this._facePassRunning)
            {
                this._facePassRunning = !p.Finished;
                this.RefreshPeopleIfShown();
            }

            if (facePass && p.Finished)
            {
                this.Regroup();
            }
        });
        try
        {
            await Task.Run(() => this.Extract(index, contents, faces, vision, files, passes, progress, cancellation), cancellation);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            finished = true;
            this._facePassRunning = false;
            if (!this.IsDisposed)
            {
                this.SetActivity(null);
                for (int i = 0; i < this._passBars.Length; i++)
                {
                    // The first bar is the scan's too.
                    if (i > 0 || !this._scanning)
                    {
                        this._passBars[i].HideProgress();
                    }
                }

                if (!cancellation.IsCancellationRequested)
                {
                    this.RefreshContentResults();
                    this.Regroup();
                }
            }
        }
    }

    /// <summary>
    /// The extraction's worker: the texts and faces of the files gone from the index dropped, then each OCR
    /// pass in turn (<see cref="ContentPasses.All"/>) over every file it has not read yet — the Windows pass over
    /// every file whose text is missing or older than the file, the later ones over the images and PDFs
    /// the Windows pass read — the face pass right after the Windows one, over every image and PDF it has
    /// not analysed (workfiles/20261027-face-detection.md § Face Pass), the Vision pass right after it, over
    /// every image and PDF it has not embedded, the tags of every image computed again first when the
    /// vocabulary changed (workfiles/20261027-image-content-analysis.md § Passes); the OCR passes while the
    /// content search is on, each other pass while its own setting is (<paramref name="passes"/>) and its
    /// engine is there; waiting between two files while the search is busy; the texts, the faces and the
    /// embeddings each saved every 50 files — no sooner than 5 s after the previous save — every half minute
    /// whatever the count, and at the end, so a pass cut short resumes where it stopped. Every bar is set
    /// first, the passes complete hidden; RapidOCR's pass is skipped without its engine. See
    /// workfiles/20261013-complementary-ocr-passes.md § Indexing Passes.
    /// </summary>
    private void Extract(FileIndex index, ContentIndex contents, FaceIndex faces, VisionIndex vision, IndexFiles files, ExtractionSettings passes, IProgress<ExtractionProgress> progress, CancellationToken cancellation)
    {
        bool changed = contents.RetainOnly(index.Entries);
        bool facesChanged = faces.RetainOnly(index.Entries);
        bool visionChanged = vision.RetainOnly(index.Entries);
        using var extractor = new ContentExtractor();
        int unsaved = 0;
        long saved = Environment.TickCount64;
        long refreshed = saved;
        var imageRead = ReadImages(index);
        bool facesRun = passes.Faces && FaceEngineFiles.IsAvailable;
        bool visionRun = passes.Vision && VisionEngineFiles.IsAvailable;
        bool captionsRun = passes.Captions && CaptionEngineFiles.IsAvailable;
        try
        {
            foreach (var pass in ContentPasses.All)
            {
                switch (pass)
                {
                    case ContentPass.Faces when passes.Faces:
                    {
                        int done = imageRead.Count(faces.IsCurrent);
                        progress.Report(new ExtractionProgress(pass, 0, 0, done, imageRead.Length, null, ContentKind.None, false, Finished: !facesRun || done == imageRead.Length));
                        break;
                    }

                    case ContentPass.Vision when passes.Vision:
                    {
                        int done = imageRead.Count(vision.IsCurrent);
                        progress.Report(new ExtractionProgress(pass, 0, 0, done, imageRead.Length, null, ContentKind.None, false, Finished: !visionRun || done == imageRead.Length));
                        break;
                    }

                    case ContentPass.Caption when passes.Captions:
                    {
                        int done = imageRead.Count(e => contents.IsCurrent(e, pass));
                        progress.Report(new ExtractionProgress(pass, 0, 0, done, imageRead.Length, null, ContentKind.None, false, Finished: !captionsRun || done == imageRead.Length));
                        break;
                    }

                    case ContentPass.Windows or ContentPass.Rapid or ContentPass.Inverted or ContentPass.Ring when passes.Ocr:
                    {
                        var read = Read(index, pass);
                        int done = read.Count(e => contents.IsCurrent(e, pass));
                        bool runs = done < read.Length && (pass != ContentPass.Rapid || OcrEngineFiles.IsAvailable);
                        progress.Report(new ExtractionProgress(pass, 0, 0, done, read.Length, null, ContentKind.None, false, Finished: !runs));
                        break;
                    }
                }
            }

            foreach (var pass in ContentPasses.All)
            {
                cancellation.ThrowIfCancellationRequested();
                switch (pass)
                {
                    case ContentPass.Faces when facesRun:
                        facesChanged |= this.ExtractFaces(index, faces, files, imageRead, progress, cancellation);
                        break;
                    case ContentPass.Vision when visionRun:
                        RunVision();
                        break;
                    case ContentPass.Caption when captionsRun:
                        RunCaptions();
                        break;
                    case ContentPass.Windows or ContentPass.Rapid or ContentPass.Inverted or ContentPass.Ring when passes.Ocr:
                        RunPass(pass);
                        break;
                }
            }
        }
        finally
        {
            if (changed)
            {
                TrySave(contents, files);
            }

            if (facesChanged)
            {
                TrySave(faces, files);
            }

            if (visionChanged)
            {
                TrySave(vision, files);
            }
        }

        // The Vision pass: the vocabulary's concepts embedded — from their cache, else by the text model —
        // the tags of the images already embedded computed again when the vocabulary changed, then every
        // image not embedded yet embedded and tagged. A failure of the engine to load is said, the pass
        // waiting for a working one.
        void RunVision()
        {
            var queue = imageRead.Where(e => !vision.IsCurrent(e)).ToArray();
            int before = imageRead.Length - queue.Length;
            VisionTags tags;
            try
            {
                tags = VisionTags.Load();
                if (!tags.Embedded)
                {
                    progress.Report(new ExtractionProgress(ContentPass.Vision, 0, queue.Length, before, imageRead.Length, null, ContentKind.None, false, false));
                    tags.Embed(this.EmbedText, cancellation);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                progress.Report(new ExtractionProgress(ContentPass.Vision, 0, queue.Length, before, imageRead.Length, null, ContentKind.None, false, Finished: true, Texts.Format(Strings.ExplorerVisionEngineFailed, ex.Message)));
                return;
            }

            if (vision.TagsKey != tags.Key)
            {
                foreach (var (relative, stamp, embedding) in vision.WithEmbeddings())
                {
                    cancellation.ThrowIfCancellationRequested();
                    contents.Set(relative, stamp, ContentPass.Vision, [new ContentResult(tags.TagsOf(embedding), null)]);
                }

                vision.TagsKey = tags.Key;
                changed = true;
                visionChanged = true;
                progress.Report(new ExtractionProgress(ContentPass.Vision, 0, queue.Length, before, imageRead.Length, null, ContentKind.None, true, false));
            }
            else
            {
                // Tags lost while the embeddings stayed — the content texts rebuilt, an older files.content:
                // computed again from the embeddings, no image read.
                foreach (var entry in imageRead.Where(e => vision.IsCurrent(e) && !contents.IsCurrent(e, ContentPass.Vision)))
                {
                    contents.Set(entry.RelativePath, entry.Stamp!.Value, ContentPass.Vision, [new ContentResult(vision.EmbeddingOf(entry.RelativePath) is { } embedding ? tags.TagsOf(embedding) : "", null)]);
                    changed = true;
                }
            }

            if (queue.Length == 0)
            {
                progress.Report(new ExtractionProgress(ContentPass.Vision, 0, 0, before, imageRead.Length, null, ContentKind.None, false, Finished: true));
                return;
            }

            VisionEncoder encoder;
            try
            {
                encoder = new VisionEncoder();
            }
            catch (Exception ex)
            {
                progress.Report(new ExtractionProgress(ContentPass.Vision, 0, queue.Length, before, imageRead.Length, null, ContentKind.None, false, Finished: true, Texts.Format(Strings.ExplorerVisionEngineFailed, ex.Message)));
                return;
            }

            using (encoder)
            {
                for (int done = 0; done < queue.Length; done++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (this.ExtractionPaused())
                    {
                        progress.Report(new ExtractionProgress(ContentPass.Vision, done, queue.Length, before + done, imageRead.Length, null, ContentKind.None, false, false));
                        this.WaitWhilePaused(cancellation);
                    }

                    var entry = queue[done];
                    string path = index.FullPath(entry);
                    progress.Report(new ExtractionProgress(ContentPass.Vision, done, queue.Length, before + done, imageRead.Length, path, ContentKind.Image, false, false));
                    float[] embedding;
                    try
                    {
                        embedding = ContentExtractor.DecodePixels(path) is var (pixels, width, height) ? encoder.Embed(pixels, width, height) : [];
                    }
                    catch (Exception ex) when (ex is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException or ArgumentException or System.Runtime.InteropServices.ExternalException)
                    {
                        // As an unreadable file: no embedding, not analysed again until it changes.
                        embedding = [];
                    }

                    vision.Set(entry.RelativePath, entry.Stamp!.Value, embedding);
                    contents.Set(entry.RelativePath, entry.Stamp!.Value, ContentPass.Vision, [new ContentResult(embedding.Length > 0 ? tags.TagsOf(embedding) : "", null)]);
                    visionChanged = true;
                    changed = true;
                    unsaved++;

                    long now = Environment.TickCount64;
                    if (now - saved >= ContentSaveInterval || (unsaved >= ContentSaveEvery && now - saved >= ContentSaveFloor))
                    {
                        TrySave(vision, files);
                        TrySave(contents, files);
                        saved = now;
                        unsaved = 0;
                    }

                    if (now - refreshed >= ContentRefreshInterval)
                    {
                        refreshed = now;
                        progress.Report(new ExtractionProgress(ContentPass.Vision, done, queue.Length, before + done, imageRead.Length, null, ContentKind.None, true, false));
                    }
                }
            }

            progress.Report(new ExtractionProgress(ContentPass.Vision, queue.Length, queue.Length, imageRead.Length, imageRead.Length, null, ContentKind.None, false, Finished: true));
        }

        // The Caption pass, the last one: every image not described yet described by Florence-2, a few
        // seconds each. A failure of the engine to load is said, the pass waiting for a working one.
        void RunCaptions()
        {
            var queue = imageRead.Where(e => !contents.IsCurrent(e, ContentPass.Caption)).ToArray();
            int before = imageRead.Length - queue.Length;
            if (queue.Length == 0)
            {
                progress.Report(new ExtractionProgress(ContentPass.Caption, 0, 0, before, imageRead.Length, null, ContentKind.None, false, Finished: true));
                return;
            }

            Captioner captioner;
            try
            {
                captioner = new Captioner();
            }
            catch (Exception ex)
            {
                progress.Report(new ExtractionProgress(ContentPass.Caption, 0, queue.Length, before, imageRead.Length, null, ContentKind.None, false, Finished: true, Texts.Format(Strings.ExplorerCaptionEngineFailed, ex.Message)));
                return;
            }

            using (captioner)
            {
                for (int done = 0; done < queue.Length; done++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (this.ExtractionPaused())
                    {
                        progress.Report(new ExtractionProgress(ContentPass.Caption, done, queue.Length, before + done, imageRead.Length, null, ContentKind.None, false, false));
                        this.WaitWhilePaused(cancellation);
                    }

                    var entry = queue[done];
                    string path = index.FullPath(entry);
                    progress.Report(new ExtractionProgress(ContentPass.Caption, done, queue.Length, before + done, imageRead.Length, path, ContentKind.Image, false, false));
                    string caption;
                    try
                    {
                        caption = ContentExtractor.DecodePixels(path) is var (pixels, width, height) ? captioner.Describe(pixels, width, height) : "";
                    }
                    catch (Exception ex) when (ex is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException or ArgumentException or System.Runtime.InteropServices.ExternalException)
                    {
                        // As an unreadable file: no caption, not described again until it changes.
                        caption = "";
                    }

                    contents.Set(entry.RelativePath, entry.Stamp!.Value, ContentPass.Caption, [new ContentResult(caption, null)]);
                    changed = true;
                    unsaved++;

                    long now = Environment.TickCount64;
                    if (now - saved >= ContentSaveInterval || (unsaved >= ContentSaveEvery && now - saved >= ContentSaveFloor))
                    {
                        TrySave(contents, files);
                        saved = now;
                        unsaved = 0;
                    }

                    if (now - refreshed >= ContentRefreshInterval)
                    {
                        refreshed = now;
                        progress.Report(new ExtractionProgress(ContentPass.Caption, done, queue.Length, before + done, imageRead.Length, null, ContentKind.None, true, false));
                    }
                }
            }

            progress.Report(new ExtractionProgress(ContentPass.Caption, queue.Length, queue.Length, imageRead.Length, imageRead.Length, null, ContentKind.None, false, Finished: true));
        }

        void RunPass(ContentPass pass)
        {
            var read = Read(index, pass);
            var queue = read.Where(e => (pass == ContentPass.Windows || contents.IsCurrent(e, ContentPass.Windows)) && !contents.IsCurrent(e, pass)).ToArray();
            int before = read.Length - queue.Length;
            if (queue.Length == 0)
            {
                progress.Report(new ExtractionProgress(pass, 0, 0, before, read.Length, null, ContentKind.None, false, Finished: true));
                return;
            }

            if (pass == ContentPass.Rapid && extractor.StartRapid() is { } why)
            {
                // Not downloaded: offered from the ⚙ menu; failing to load: said, the pass waiting.
                string? note = OcrEngineFiles.IsAvailable ? Texts.Format(Strings.ExplorerRapidFailed, why) : null;
                progress.Report(new ExtractionProgress(pass, 0, queue.Length, before, read.Length, null, ContentKind.None, false, Finished: true, note));
                return;
            }

            for (int done = 0; done < queue.Length; done++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (this.ExtractionPaused())
                {
                    progress.Report(new ExtractionProgress(pass, done, queue.Length, before + done, read.Length, null, ContentKind.None, false, false));
                    this.WaitWhilePaused(cancellation);
                }

                var entry = queue[done];
                string path = index.FullPath(entry);
                var kind = ContentExtractor.KindOf(path);
                progress.Report(new ExtractionProgress(pass, done, queue.Length, before + done, read.Length, path, kind, false, false));
                contents.Set(entry.RelativePath, entry.Stamp!.Value, pass, extractor.Extract(path, pass));
                changed = true;
                unsaved++;

                long now = Environment.TickCount64;
                if (now - saved >= ContentSaveInterval || (unsaved >= ContentSaveEvery && now - saved >= ContentSaveFloor))
                {
                    TrySave(contents, files);
                    saved = now;
                    unsaved = 0;
                }

                if (now - refreshed >= ContentRefreshInterval)
                {
                    refreshed = now;
                    progress.Report(new ExtractionProgress(pass, done, queue.Length, before + done, read.Length, null, ContentKind.None, true, false));
                }
            }

            extractor.StopRapid();
            progress.Report(new ExtractionProgress(pass, queue.Length, queue.Length, read.Length, read.Length, null, ContentKind.None, false, Finished: true));
        }
    }

    /// <summary>
    /// The face pass: every image and PDF the face index lacks, or holds from an older version of the file,
    /// analysed in turn — its faces recorded, none included — with the OCR passes' pause rule and save
    /// cadence, the groups computed again every few seconds; true when any face was recorded. A failure of
    /// the engine to load is said, the pass waiting for a working one. See
    /// workfiles/20261027-face-detection.md § Face Pass.
    /// </summary>
    private bool ExtractFaces(FileIndex index, FaceIndex faces, IndexFiles files, IndexEntry[] read, IProgress<ExtractionProgress> progress, CancellationToken cancellation)
    {
        var queue = read.Where(e => !faces.IsCurrent(e)).ToArray();
        int before = read.Length - queue.Length;
        if (queue.Length == 0)
        {
            progress.Report(new ExtractionProgress(ContentPass.Faces, 0, 0, before, read.Length, null, ContentKind.None, false, Finished: true));
            return false;
        }

        FaceAnalyzer analyzer;
        try
        {
            analyzer = new FaceAnalyzer();
        }
        catch (Exception ex)
        {
            // A DLL that does not load, a model that does not parse: the pass waits for a working engine.
            progress.Report(new ExtractionProgress(ContentPass.Faces, 0, queue.Length, before, read.Length, null, ContentKind.None, false, Finished: true, Texts.Format(Strings.ExplorerFaceEngineFailed, ex.Message)));
            return false;
        }

        using (analyzer)
        {
            int unsaved = 0;
            long saved = Environment.TickCount64;
            long refreshed = saved;
            for (int done = 0; done < queue.Length; done++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (this.ExtractionPaused())
                {
                    progress.Report(new ExtractionProgress(ContentPass.Faces, done, queue.Length, before + done, read.Length, null, ContentKind.None, false, false));
                    this.WaitWhilePaused(cancellation);
                }

                var entry = queue[done];
                string path = index.FullPath(entry);
                progress.Report(new ExtractionProgress(ContentPass.Faces, done, queue.Length, before + done, read.Length, path, ContentKind.Image, false, false));
                FaceBox[] found;
                try
                {
                    found = ContentExtractor.DecodePixels(path) is var (pixels, width, height) ? analyzer.Analyze(pixels, width, height) : [];
                }
                catch (Exception ex) when (ex is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or OutOfMemoryException or ArgumentException)
                {
                    // As an unreadable file: no face, not analysed again until it changes.
                    found = [];
                }

                faces.Set(entry.RelativePath, entry.Stamp!.Value, found);
                unsaved++;

                long now = Environment.TickCount64;
                if (now - saved >= ContentSaveInterval || (unsaved >= ContentSaveEvery && now - saved >= ContentSaveFloor))
                {
                    TrySave(faces, files);
                    saved = now;
                    unsaved = 0;
                }

                if (now - refreshed >= ContentRefreshInterval)
                {
                    refreshed = now;
                    progress.Report(new ExtractionProgress(ContentPass.Faces, done, queue.Length, before + done, read.Length, null, ContentKind.None, true, false));
                }
            }
        }

        progress.Report(new ExtractionProgress(ContentPass.Faces, queue.Length, queue.Length, read.Length, read.Length, null, ContentKind.None, false, Finished: true));
        return true;
    }

    /// <summary>Waits while the extraction is paused (<see cref="ExtractionPaused"/>), looking again every 200 ms.</summary>
    private void WaitWhilePaused(CancellationToken cancellation)
    {
        while (this.ExtractionPaused())
        {
            cancellation.WaitHandle.WaitOne(PausePoll);
            cancellation.ThrowIfCancellationRequested();
        }
    }

    /// <summary>The scanned files the face and Vision passes read: the images and PDFs.</summary>
    private static IndexEntry[] ReadImages(FileIndex index) =>
        index.Entries.Where(e => e.Stamp is not null && ContentExtractor.KindOf(e.RelativePath) is ContentKind.Image or ContentKind.Pdf).ToArray();

    /// <summary>The scanned files a pass reads: every one for the Windows pass, the images and PDFs for the later ones.</summary>
    private static IndexEntry[] Read(FileIndex index, ContentPass pass) =>
        index.Entries.Where(e => e.Stamp is not null
            && (pass == ContentPass.Windows || ContentExtractor.KindOf(e.RelativePath) is ContentKind.Image or ContentKind.Pdf)).ToArray();

    /// <summary>
    /// The bars as the settings stand, in the order the passes run: the Windows pass's — the scan's too —
    /// always, the face pass's while the face analysis is on, the later OCR passes' while the content search is.
    /// </summary>
    private void LayoutBars()
    {
        var shown = this.ShownPasses();
        this._bars.SuspendLayout();
        this._bars.Controls.Clear();
        for (int row = 0; row < shown.Length; row++)
        {
            this._bars.Controls.Add(this._passBars[(int)shown[row]], 0, row);
        }

        this._bars.ResumeLayout();
    }

    /// <summary>The bars shown: <see cref="LayoutBars"/>'s count.</summary>
    private int BarsShown => this.ShownPasses().Length;

    /// <summary>The passes whose bar shows, in the order they run: the Windows pass — the scan — always, each other while its setting is on.</summary>
    private ContentPass[] ShownPasses() =>
        ContentPasses.All.Where(pass => pass == ContentPass.Windows || this.PassOn(pass)).ToArray();

    /// <summary>Whether a pass runs as the settings stand: an OCR pass while the content search is on, any other while its own setting is.</summary>
    private bool PassOn(ContentPass pass) => pass switch
    {
        ContentPass.Faces => this._faceAnalysis,
        ContentPass.Vision => this._visualAnalysis,
        ContentPass.Caption => this._captions,
        _ => this._contentSearch,
    };

    /// <summary>The bars' tooltip: each pass, its colour, and how far it has gone.</summary>
    private void ApplyBarTips()
    {
        var lines = ContentPasses.All.Where(pass => ContentPasses.IsOcr(pass) || this.PassOn(pass)).Select(pass =>
        {
            int i = (int)pass;
            var (done, needed) = this._passCounts[i];
            bool missing = pass switch
            {
                ContentPass.Rapid => !OcrEngineFiles.IsAvailable,
                ContentPass.Faces => !FaceEngineFiles.IsAvailable,
                ContentPass.Vision => !VisionEngineFiles.IsAvailable,
                ContentPass.Caption => !CaptionEngineFiles.IsAvailable,
                _ => false,
            };
            string state = missing ? Strings.ExplorerNotDownloaded
                : needed < 0 ? Strings.ExplorerNotStarted
                : Texts.Format("{0:N0}/{1:N0}", done, needed);
            return Texts.Format("{0} ({1}): {2}", ContentPasses.NameOf(pass), PassColorNames[i], state);
        });

        string tip = Strings.ExplorerIndexingPasses + "\n" + string.Join('\n', lines);
        this._toolTip.SetToolTip(this._bars, tip);
        foreach (var bar in this._passBars)
        {
            this._toolTip.SetToolTip(bar, tip);
        }
    }

    /// <summary>Whether the extraction waits: thumbnails are loading, or the search box was typed in a moment ago. Read from the worker.</summary>
    private bool ExtractionPaused() =>
        this._grid.LoadingThumbnails || Environment.TickCount64 - Interlocked.Read(ref this._lastKeystroke) < KeystrokePause;

    /// <summary>Saves the content texts into <paramref name="files"/>, a failure left for the next save.</summary>
    private static void TrySave(ContentIndex contents, IndexFiles files)
    {
        try
        {
            contents.Save(files.Content);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
        }
    }

    /// <summary>Saves the embeddings into <paramref name="files"/>, a failure left for the next save.</summary>
    private static void TrySave(VisionIndex vision, IndexFiles files)
    {
        try
        {
            vision.Save(files.Vision);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
        }
    }

    /// <summary>Saves the faces found into <paramref name="files"/>, a failure left for the next save.</summary>
    private static void TrySave(FaceIndex faces, IndexFiles files)
    {
        try
        {
            faces.Save(files.Faces);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
        }
    }

    /// <summary>The search shown — not the favorites, nor <c>*</c>, nor a folder as the disk holds it — run again, its place kept, as texts arrived.</summary>
    private void RefreshContentResults()
    {
        if (FileSearch.Words(_search.Text).Length > 0 && !FileSearch.IsEverything(_search.Text))
        {
            this.RefreshRows(keepPlace: true);
        }
    }

    /// <summary>What the extraction does — the file it reads — after the summary on the status line; null for nothing.</summary>
    private void SetActivity(string? activity)
    {
        if (activity == this._activity)
        {
            return;
        }

        this._activity = activity;
        if (!this._transient.Enabled)
        {
            this.ShowSummary();
        }
    }

    private static string Summary(FileIndex index)
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        string when = index.ScannedAt.Date == DateTime.Today ? index.ScannedAt.ToString("t", culture) : index.ScannedAt.ToString("g", culture);
        return Texts.Format(Strings.ExplorerSummaryIndexed, Files(index.Count), when);
    }

    private static string Files(int count) => ThumbnailGrid.FileCount(count);

    /// <summary>The line shown while nothing else is: the index summary, or what stands in its way.</summary>
    private void SetSummary(string summary, bool error = false)
    {
        _summary = summary;
        _summaryError = error;
        ShowSummary();
    }

    private void ShowSummary()
    {
        _transient.Stop();
        string[] parts = [this._summary, .. new[] { this._activity, this._engineActivity }.OfType<string>()];
        Show(string.Join(" · ", parts), this._summaryError);
    }

    /// <summary>Where the OCR engine's download stands, after the activity on the status line; null for nothing.</summary>
    private void SetEngineActivity(string? activity)
    {
        if (activity == this._engineActivity)
        {
            return;
        }

        this._engineActivity = activity;
        if (!this._transient.Enabled)
        {
            this.ShowSummary();
        }
    }

    /// <summary>A progress text, kept until the next status.</summary>
    private void ShowStatus(string message)
    {
        _transient.Stop();
        Show(message, error: false);
    }

    /// <summary>A message that gives way to the summary after a few seconds.</summary>
    private void ShowTransient(string message, bool error = false)
    {
        Show(message, error);
        _transient.Stop();
        _transient.Start();
    }

    private void Show(string message, bool error)
    {
        _status.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
        _status.Text = message;
        _toolTip.SetToolTip(_status, message);
    }

    /// <summary>
    /// The list as the view and the search box stand. The search view: every favorite while the box
    /// is blank, every file for <c>*</c>, else the matches of its words. The folder view: the open
    /// folder's content, read from the disk, while the box is blank; else the folders then the files
    /// below the open folder, from the index. Without a base folder, a typed search or the folder view
    /// shows the invitation instead. A new list shows its first load from the top; a refreshed one
    /// (<paramref name="keepPlace"/>: a rescan, a favorite or a missing file gone) keeps as many tiles
    /// loaded as before, the scroll and the selection. <paramref name="select"/>: a path to select
    /// once listed — the folder just left when going up.
    /// </summary>
    private void RefreshRows(bool keepPlace = false, string? select = null)
    {
        int version = ++_listVersion;
        bool everything = FileSearch.IsEverything(_search.Text);
        string[] words = FileSearch.Words(_search.Text);
        this._grid.HighlightWords = words;
        this._diskSizes.Clear();

        // 🔄 acts on the open secondary source or on the base folder, as the view stands.
        this.UpdateRescan();
        if (this._peopleView)
        {
            this.RefreshPeopleRows(keepPlace, select, words);
            return;
        }

        if (this._giphyView)
        {
            this._grid.HighlightWords = [];
            this.RefreshGiphyRows(keepPlace);
            return;
        }

        this.ShowGroupTitle(null);
        bool inviting = _baseFolder is null && (words.Length > 0 || _folderView);
        this.SetInvite(InviteAction.ChooseFolder);
        _invite.Visible = inviting;
        _grid.Visible = !inviting;
        if (_folderView)
        {
            if (inviting)
            {
                ShowRows([], keepPlace, null);
                ShowBreadcrumb("");
            }
            else if (words.Length == 0)
            {
                _ = ListFolderAsync(version, keepPlace, select);
            }
            else if (this.ViewIndex is not { } index)
            {
                ShowRows([], keepPlace, null);
                ShowBreadcrumb(Strings.ExplorerWaitingForIndex);
            }
            else
            {
                var (found, trailing) = SearchFolder(index, words, everything);
                ShowRows(found, keepPlace, select);
                ShowBreadcrumb(trailing);
            }

            return;
        }

        var rows = new List<ExplorerRow>();
        this._similarClear.Visible = words.Length == 0 && this._similarTo is not null && !inviting;
        if (this._similarClear.Visible)
        {
            rows.AddRange(this.SimilarRows(this._similarTo!));
            this._caption.Text = rows.Count == 0 ? Texts.Format(Strings.ExplorerSimilarNone, this._similarTo!.Name) : Texts.Format(Strings.ExplorerSimilarCount, this._similarTo!.Name, rows.Count);
        }
        else if (words.Length == 0)
        {
            foreach (var favorite in _favorites.Newest)
            {
                rows.Add(new ExplorerRow(favorite.FullPath, Path.GetFileName(favorite.FullPath), Preset: favorite.Preset));
            }

            _caption.Text = Texts.Format(Strings.ExplorerFavoritesCount, _favorites.Count);
        }
        else if (_index is null)
        {
            _caption.Text = inviting ? "" : Strings.ExplorerWaitingForIndex;
        }
        else
        {
            var found = this.Find(_index.Entries, words, everything);

            var inPath = new List<bool>();
            rows.AddRange(this.SearchSections(this.WithFavorites(found, _index, words, everything, inPath), inPath, everything));

            _caption.Text = everything
                ? Texts.Format(Strings.ExplorerAllFiles, found.Count)
                : rows.Count switch
                {
                    0 => Strings.ExplorerNoResult,
                    1 => Strings.ExplorerResultOne,
                    _ => Texts.Format(Strings.ExplorerResults, rows.Count),
                };
        }

        ShowRows(rows, keepPlace, null);
    }

    /// <summary>
    /// A new list: its first load — or, keeping the place, as many tiles as were loaded — and enough
    /// of it for <paramref name="select"/>, then selected.
    /// </summary>
    private void ShowRows(IReadOnlyList<ExplorerRow> rows, bool keepPlace, string? select)
    {
        _list = rows;
        int count = keepPlace ? Math.Max(_loaded, FirstLoad()) : FirstLoad();
        if (select is not null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].FullPath, select, StringComparison.OrdinalIgnoreCase))
                {
                    count = Math.Max(count, i + 1);
                    break;
                }
            }
        }

        ShowLoaded(count, keepPlace);
        if (select is not null)
        {
            _grid.SelectPath(select);
        }
    }

    /// <summary>
    /// The open folder's content, read from the disk off the UI thread — the status line saying so when
    /// it takes a while — unless another list was asked for meanwhile. The open folder gone from the
    /// disk gives way to its nearest parent still there, and the status line says so.
    /// </summary>
    private async Task ListFolderAsync(int version, bool keepPlace, string? select)
    {
        var source = this.ViewSource;
        string root = source?.Folder ?? _baseFolder!;
        string asked = _openFolder;
        var order = this._fileOrder;

        // At the base folder's root, the secondary sources first, each looked for on the disk too.
        string[] sources = source is null && asked.Length == 0 ? [.. this._sources.Select(s => s.Folder)] : [];
        var reading = Task.Run(() => (ReadFolder(root, asked, order), sources.Select(Directory.Exists).ToArray()));
        bool noticed = false;
        if (await Task.WhenAny(reading, Task.Delay(ReadingNoticeDelay)) != reading && version == _listVersion && !IsDisposed)
        {
            ShowStatus(Strings.ExplorerReadingFolder);
            noticed = true;
        }

        var (listing, found) = await reading;
        if (IsDisposed)
        {
            return;
        }

        if (noticed)
        {
            ShowSummary();
        }

        if (version != _listVersion)
        {
            return;
        }

        if (!string.Equals(listing.Folder, asked, StringComparison.OrdinalIgnoreCase))
        {
            _openFolder = listing.Folder;
            keepPlace = false;
            select = null;
            FolderViewChanged?.Invoke(this, EventArgs.Empty);
            Report(Texts.Format(Strings.ExplorerFolderNotFoundBack, asked, SegmentsOf(listing.Folder)[^1]), error: false);
        }

        var folders = listing.Folders.Select(this.FolderRow).ToList();
        var files = listing.Files.Select(file => new ExplorerRow(Path.Combine(root, file.RelativePath), file.Name, Size: file.Size)).ToList();
        var sourceRows = source is null && listing.Folder.Length == 0 ? this.SourceRows(found) : [];
        if (this._folderGroups && sourceRows.Count > 0)
        {
            string header = Texts.Format("{0} ({1:N0})", Strings.SourcesSection, sourceRows.Count);
            sourceRows = [.. sourceRows.Select(row => row with { Section = header })];
        }

        ShowRows([.. sourceRows, .. this._folderGroups ? this.FolderSections(folders, files) : [.. folders, .. files]], keepPlace, select);
        ShowBreadcrumb(listing.Error ?? Texts.Format("{0}, {1}", Folders(listing.Folders.Count), Files(listing.Files.Count)));
    }

    /// <summary>
    /// The folder as the disk holds it, on a worker: <paramref name="relative"/>, or its nearest parent
    /// still there, the base folder at worst; nothing listed, and why, when that cannot be read. Its
    /// files in <paramref name="order"/>, relative to <paramref name="root"/>.
    /// </summary>
    private static (string Folder, IReadOnlyList<string> Folders, IReadOnlyList<IndexEntry> Files, string? Error) ReadFolder(string root, string relative, FileOrder order)
    {
        while (relative.Length > 0 && !Directory.Exists(Path.Combine(root, relative)))
        {
            relative = FolderTree.Parent(relative);
        }

        try
        {
            var (folders, files) = FolderListing.Read(root, relative, order);
            return (relative, folders, files, null);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            return (relative, [], [], ex is DirectoryNotFoundException ? Strings.ExplorerFolderNotFound : Texts.Format(Strings.ExplorerNotReadable, ex.Message));
        }
    }

    /// <summary>
    /// A search in the folder view, from the index: the folders below the open one, then its files —
    /// every one of each for <c>*</c>, the folders A→Z by their path so each is followed by its
    /// subfolders, the files in <see cref="FileOrder"/>; else those matching the words, best first,
    /// the files of a relevance tier in <see cref="FileOrder"/>.
    /// </summary>
    private (IReadOnlyList<ExplorerRow> Rows, string Trailing) SearchFolder(FileIndex index, string[] words, bool everything)
    {
        var tree = this.ViewTree() ?? FolderTree.Of(index.Entries);
        var folders = tree.Folders.Where(f => FolderTree.IsUnder(f.RelativePath, _openFolder)).ToArray();
        var files = _openFolder.Length == 0 ? index.Entries : index.Entries.Where(e => FolderTree.IsUnder(e.RelativePath, _openFolder)).ToArray();
        IReadOnlyList<IndexEntry> foundFolders;
        if (everything)
        {
            Array.Sort(folders, (a, b) => FolderTree.ComparePaths(a.RelativePath, b.RelativePath));
            foundFolders = folders;
        }
        else if (this._byName.Checked)
        {
            foundFolders = FileSearch.Search(folders, words).Select(m => m.Entry).ToArray();
        }
        else
        {
            // Content only: a folder has no content text, so none is found.
            foundFolders = [];
        }

        var foundFiles = this.Find(files, words, everything);

        var rows = new List<ExplorerRow>(foundFolders.Count + foundFiles.Count);
        var inPath = new List<bool>();
        foreach (var folder in foundFolders)
        {
            rows.Add(FolderRow(index.FullPath(folder)));
            inPath.Add(true);
        }

        rows.AddRange(this.WithFavorites(foundFiles, index, words, everything, inPath));
        rows = this.SearchSections(rows, inPath, everything);

        int total = rows.Count;
        string trailing = everything
            ? Texts.Format(Strings.ExplorerAllCounts, Folders(foundFolders.Count), Files(foundFiles.Count))
            : total switch
            {
                0 => Strings.ExplorerNoResult,
                1 => Strings.ExplorerResultOne,
                _ => Texts.Format(Strings.ExplorerResults, total),
            };
        return (rows, trailing);
    }

    /// <summary>
    /// The files a search found, with the favorites matching the words merged in by tier, ahead of the
    /// files of theirs (<see cref="FileSearch.SearchFavorites"/>): every effect preset — by its file's name
    /// or its description — and the file favorites not found already, wherever they are, in the folder
    /// view too. Under the Aa criterion only; never in <c>*</c>. See
    /// workfiles/20261026-effect-preset-favorites.md, Iteration 6. <paramref name="inPath"/>, when given,
    /// gets for each row whether it was found by a name (<see cref="FileSearch.IsPathTier"/>).
    /// </summary>
    private List<ExplorerRow> WithFavorites(IReadOnlyList<SearchMatch> found, FileIndex index, string[] words, bool everything, List<bool>? inPath = null)
    {
        var rows = new List<ExplorerRow>(found.Count);
        var shown = new HashSet<string>(found.Select(match => index.FullPath(match.Entry)), StringComparer.OrdinalIgnoreCase);
        var extras = new List<(int Tier, ExplorerRow Row)>();
        if (!everything && this._byName.Checked)
        {
            foreach (var match in FileSearch.SearchFavorites(this._favorites.Newest, words))
            {
                if (match.Favorite.Preset is not null || shown.Add(match.Favorite.FullPath))
                {
                    extras.Add((match.Tier, FavoriteRow(match)));
                }
            }

            // The files of the people named so, in the folder view those below the open folder
            // (workfiles/20261027-face-detection.md § Search by Name).
            foreach (var (path, tier) in FileSearch.SearchPeople(this.NamedPeople(), words))
            {
                if (this.ViewEntryOf(path) is { } entry && (!this._folderView || FolderTree.IsUnder(entry.RelativePath, this._openFolder)) && shown.Add(path))
                {
                    extras.Add((tier, new ExplorerRow(path, entry.Name, Size: entry.Size)));
                }
            }
        }

        // Stable: the favorites' order, then the people's, within a tier.
        var ordered = extras.OrderBy(extra => extra.Tier).ToList();
        int next = 0;
        foreach (var match in found)
        {
            for (; next < ordered.Count && ordered[next].Tier <= match.Tier; next++)
            {
                rows.Add(ordered[next].Row);
                inPath?.Add(FileSearch.IsPathTier(ordered[next].Tier, words.Length));
            }

            rows.Add(new ExplorerRow(index.FullPath(match.Entry), match.Entry.Name, ContentWord: match.ContentWord, Size: match.Entry.Size, Closeness: Closeness(match)));
            inPath?.Add(FileSearch.IsPathTier(match.Tier, words.Length));
        }

        for (; next < ordered.Count; next++)
        {
            rows.Add(ordered[next].Row);
            inPath?.Add(FileSearch.IsPathTier(ordered[next].Tier, words.Length));
        }

        return rows;

        static ExplorerRow FavoriteRow(FavoriteMatch match) =>
            new(match.Favorite.FullPath, Path.GetFileName(match.Favorite.FullPath), Preset: match.Favorite.Preset, DescriptionWord: match.DescriptionWord);
    }

    /// <summary>
    /// The folder view's sections, in order (workfiles/20261103-file-explorer-groups.md § Sections): one per
    /// group of folders sharing their first words, A→Z by the words they share; the other folders; one per
    /// series of named files, A→Z; the files named by a person — and those whose every group is turned off —
    /// then one per name group, in the groups' order, the known sources among them; then one per source found
    /// by itself, A→Z. Each keeps the listing's order; a section left empty shows nothing.
    /// </summary>
    private List<ExplorerRow> FolderSections(IReadOnlyList<ExplorerRow> folders, IReadOnlyList<ExplorerRow> files)
    {
        var rows = new List<ExplorerRow>(folders.Count + files.Count);
        var prefixes = SharedPrefix.Groups(folders.Select(folder => folder.Name));
        foreach (var group in folders.Where(folder => prefixes.ContainsKey(folder.Name))
            .GroupBy(folder => prefixes[folder.Name], StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            Add(group.ToList(), group.Key);
        }

        Add(folders.Where(folder => !prefixes.ContainsKey(folder.Name)).ToList(), OtherFoldersTitle);

        // The files: a name group first, else a source found by itself, else a series.
        var groups = files.Select(file => NamePatterns.Of(file.Name, this._patternsOff)).ToArray();
        var left = files.Where((_, i) => groups[i] is null).Select(file => file.Name).ToList();
        var sources = this._patternsOff.Contains(NamePatterns.DetectedSourcesId) ? new Dictionary<string, string>() : NamePatterns.DetectSources(left);
        var series = this._patternsOff.Contains(NamePatterns.SeriesId) ? new Dictionary<string, string>() : NamePatterns.Series(left.Where(name => !sources.ContainsKey(name)));
        AddRuns(series);
        Add(files.Where((file, i) => groups[i] is null && !sources.ContainsKey(file.Name) && !series.ContainsKey(file.Name)).ToList(), NamedFilesTitle);
        foreach (var group in NamePatterns.Groups)
        {
            Add(files.Where((_, i) => groups[i] == group).ToList(), group.Title);
        }

        AddRuns(sources);
        return rows;

        // A section per title of a file's, A→Z.
        void AddRuns(Dictionary<string, string> titles)
        {
            foreach (var run in files.Where(file => titles.ContainsKey(file.Name))
                .GroupBy(file => titles[file.Name], StringComparer.OrdinalIgnoreCase)
                .OrderBy(run => run.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                Add(run.ToList(), run.Key);
            }
        }

        void Add(List<ExplorerRow> section, string title)
        {
            string header = Texts.Format("{0} ({1:N0})", title, section.Count);
            rows.AddRange(section.Select(row => row with { Section = header }));
        }
    }

    /// <summary>
    /// A search's rows in their two sections while the search groups are on — Path, found by a name, then
    /// Content (<paramref name="inPath"/>, one per row) — as they come, the tiers keeping each section in
    /// one run; <c>*</c> unsectioned. See workfiles/20261103-file-explorer-groups.md § Search Result Groups.
    /// </summary>
    private List<ExplorerRow> SearchSections(List<ExplorerRow> rows, List<bool> inPath, bool everything)
    {
        if (!this._searchGroups || everything)
        {
            return rows;
        }

        int path = inPath.Count(found => found);
        string pathHeader = Texts.Format("{0} ({1:N0})", PathTitle, path);
        string contentHeader = Texts.Format("{0} ({1:N0})", ContentTitle, rows.Count - path);
        return [.. rows.Select((row, i) => row with { Section = inPath[i] ? pathHeader : contentHeader })];
    }

    /// <summary>
    /// The named groups that show — the hidden ones left out — with their files: a group's and its
    /// sub-groups', a sub-group's of its own, so a name merged into another person still finds its files.
    /// </summary>
    private IEnumerable<(string Name, IEnumerable<string> Files)> NamedPeople() =>
        this._snapshot.Groups.SelectMany(group => group.SubGroups.Prepend(group))
            .Where(group => group.Name.Length > 0)
            .Select(group => (group.Name, (group.Parent is null ? group.AllFaces : group.Faces).Select(face => face.FullPath)));

    /// <summary>
    /// The files of a search: every one for <c>*</c>, in <see cref="FileOrder"/>; else those matching
    /// the words — by their path or by their content text, as the search criteria ask — best first, the
    /// files of a relevance tier in <see cref="FileOrder"/>.
    /// </summary>
    private IReadOnlyList<SearchMatch> Find(IReadOnlyList<IndexEntry> entries, string[] words, bool everything) =>
        this.Find(entries, words, everything, this.ViewContents, this.ViewVision);

    /// <summary>The files of a search, as <see cref="Find(IReadOnlyList{IndexEntry}, string[], bool)"/> finds them, read in the texts and embeddings of the index they belong to.</summary>
    private IReadOnlyList<SearchMatch> Find(IReadOnlyList<IndexEntry> entries, string[] words, bool everything, ContentIndex? texts, VisionIndex? embeddings)
    {
        if (everything)
        {
            return FileSearch.All(entries, this._fileOrder).Select(e => new SearchMatch(e, null)).ToArray();
        }

        var contents = this._byContent.Checked ? texts : null;
        var found = FileSearch.Search(entries, words, contents is null ? null : (e, word) => contents.HitOf(e.RelativePath, word), this._byName.Checked, this._fileOrder);
        return this._byContent.Checked ? this.WithSimilar(found, entries, words, embeddings) : found;
    }

    /// <summary>The words around the one the search found in a tile's content text, 4 on each side.</summary>
    private string? ExcerptOf(ExplorerRow row) =>
        row.ContentWord is { } word && this.Locate(row.FullPath) is { } at && this.ContentsAt(at.Source) is { } contents
            ? contents.ExcerptOf(at.Relative, word, 4)
            : null;

    /// <summary>Where the OCR recognised the word the search found in a tile's content, for the bulb's arrow.</summary>
    private RectangleF? SpotOf(ExplorerRow row) =>
        row.ContentWord is { } word && this.Locate(row.FullPath) is { } at && this.ContentsAt(at.Source) is { } contents
            ? contents.SpotOf(at.Relative, word)
            : null;

    /// <summary>
    /// A file's text as words and where the word the search found in it stands, for the Details view's
    /// excerpt; null for a file not found by its content.
    /// </summary>
    private (string[] Words, int At)? WordsOf(ExplorerRow row)
    {
        if (row.DescriptionWord is { } found && row.Preset is { } preset)
        {
            // A preset found by its description: the words around the first one holding the word found.
            string[] words = preset.Description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            int at = Array.FindIndex(words, word => FileSearch.Fold(word).Contains(found, StringComparison.Ordinal));
            return at >= 0 ? (words, at) : null;
        }

        return row.ContentWord is { } word && this.Locate(row.FullPath) is { } located && this.ContentsAt(located.Source) is { } contents
            ? contents.WordsAround(located.Relative, word)
            : null;
    }

    /// <summary>
    /// A file's size for the Details view: the index's or the folder listing's, else read from the disk
    /// once per list; null when it cannot be read.
    /// </summary>
    private long? SizeOf(ExplorerRow row)
    {
        if (row.Size is { } size)
        {
            return size;
        }

        // A GIPHY result has no file to measure until it is fetched.
        if (row.Online is not null)
        {
            return null;
        }

        if (!this._diskSizes.TryGetValue(row.FullPath, out var read))
        {
            try
            {
                var info = new FileInfo(row.FullPath);
                read = info.Exists ? info.Length : null;
            }
            catch (Exception ex) when (FileIndex.IsFileError(ex))
            {
                read = null;
            }

            this._diskSizes[row.FullPath] = read;
        }

        return read;
    }

    /// <summary>
    /// The first files directly in a folder row's folder, as the index holds them, in <see cref="FileOrder"/>:
    /// its strip in the Details view, <paramref name="max"/> at most; none before the index is there.
    /// </summary>
    private IReadOnlyList<string> FolderFiles(ExplorerRow row, int max)
    {
        if (!row.IsFolder || max <= 0 || this.Locate(row.FullPath) is not { } at || this.IndexAt(at.Source) is not { } index || this.TreeAt(at.Source) is not { } tree)
        {
            return [];
        }

        var files = tree.FilesIn(at.Relative == "." ? "" : at.Relative, this._fileOrder);
        var paths = new string[Math.Min(max, files.Count)];
        for (int i = 0; i < paths.Length; i++)
        {
            paths[i] = index.FullPath(files[i]);
        }

        return paths;
    }

    /// <summary>A folder's tile: its name, and every file below it as the index counts them, once the index is there.</summary>
    private ExplorerRow FolderRow(string fullPath)
    {
        string name = Path.GetFileName(fullPath);

        // Inside a secondary source too, a folder keeps the usual color: only the source's own tile is blue.
        if (this.ViewIndex is not { } index || this.ViewRoot is not { } root || this.ViewTree() is not { } tree)
        {
            return new ExplorerRow(fullPath, name, IsFolder: true);
        }

        int count = tree.CountBelow(Path.GetRelativePath(root, fullPath), index.Count);
        return new ExplorerRow(fullPath, name, IsFolder: true, Count: count);
    }

    private static string Folders(int count) => count == 1 ? Strings.ExplorerFolderOne : Texts.Format(Strings.ExplorerFolderMany, count);

    /// <summary>The base folder's name, the open secondary source's when one is, then every folder down to <paramref name="relative"/>.</summary>
    private string[] SegmentsOf(string relative)
    {
        string root = _baseFolder is null ? "" : Path.GetFileName(_baseFolder) is { Length: > 0 } name ? name : _baseFolder;
        string[] head = this.ViewSource is { } source ? [root, source.Name] : [root];
        return relative.Length == 0 ? head : [.. head, .. relative.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>The breadcrumb on the open folder, <paramref name="trailing"/> after it — a secondary source's name in blue, after a clear separator.</summary>
    private void ShowBreadcrumb(string trailing) =>
        _breadcrumb.SetPath(_baseFolder is null ? [] : SegmentsOf(_openFolder), trailing, this.ViewSource is null ? int.MaxValue : 1);

    /// <summary>The index, and the folders derived from it, taken over together.</summary>
    private void SetIndex(FileIndex? index, FolderTree? tree)
    {
        _index = index;
        _tree = tree;
        this._entriesByPath = null;
        this._groupedAt = -1;
    }

    /// <summary>Whether the list is the favorites: the search view with a blank box.</summary>
    private bool ShowingFavorites => !_folderView && !this._peopleView && !this._giphyView && FileSearch.Words(_search.Text).Length == 0;

    /// <summary>Another view picked in the drop-down by the user: it shows, remembered, <paramref name="select"/> selected once listed.</summary>
    private void ChangeMode(ExplorerMode mode, string? select = null)
    {
        if (this._syncingToggle || mode == this.Mode)
        {
            return;
        }

        this.SetMode(mode, select);
        this.FolderViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The view <paramref name="step"/> away from the current one in <see cref="ExplorerMode"/>'s order, round
    /// the end, People skipped while the face analysis is off.
    /// </summary>
    private ExplorerMode NextMode(int step)
    {
        int count = Enum.GetValues<ExplorerMode>().Length;
        var mode = this.Mode;
        do
        {
            mode = (ExplorerMode)((((int)mode + step) % count + count) % count);
        }
        while ((mode == ExplorerMode.People && !this._faceAnalysis) || (mode == ExplorerMode.Giphy && this._giphyKey is null));

        return mode;
    }

    /// <summary>
    /// An entry of the views drop-down — in its list and in its closed box alike: its icon in its colour, then
    /// its name; both faded while the drop-down is disabled.
    /// </summary>
    private void DrawModeItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            e.DrawBackground();
            return;
        }

        var mode = (ExplorerMode)this._modes.Items[e.Index]!;
        UiIcons.DrawItem(this._modes, e, ModeIcons[(int)mode], ModeText(mode));
    }

    /// <summary>A view's name in the views drop-down, in the UI language.</summary>
    private static string ModeText(ExplorerMode mode) => mode switch
    {
        ExplorerMode.Folders => Strings.ExplorerModeFolders,
        ExplorerMode.People => Strings.ExplorerPeople,
        ExplorerMode.Giphy => Strings.ExplorerModeGiphy,
        _ => Strings.ExplorerModeFavorites,
    };

    /// <summary>What the view changes around the grid: the caption or the breadcrumb, the search box's hint, the GIPHY view's controls.</summary>
    private void ApplyView()
    {
        bool trail = _folderView || this._peopleView || this._giphyView;
        _caption.Visible = !trail;
        _breadcrumb.Visible = trail;
        _search.PlaceholderText = this._giphyView ? Strings.ExplorerGiphyPlaceholder : this._peopleView ? PeopleSearchPlaceholder : _folderView ? FolderSearchPlaceholder : SearchPlaceholder;
        this.ApplyGiphyView();
    }

    /// <summary>
    /// A search criterion button clicked: pressed or released, the search shown run again — unless it is
    /// the last one pressed, which stays pressed. Nothing else moves: the favorites and <c>*</c> stay as
    /// they are, the criteria counting once words are typed.
    /// </summary>
    private void ToggleCriterion(CheckBox criterion, CheckBox other)
    {
        if (criterion.Checked && !other.Checked)
        {
            return;
        }

        criterion.Checked = !criterion.Checked;
        this.ApplyCriteriaTips();
        if (FileSearch.Words(this._search.Text).Length > 0 && !FileSearch.IsEverything(this._search.Text))
        {
            this.RefreshRows();
        }
    }

    /// <summary>The criteria buttons' tooltips: what each searches, whether it is on, why the last one pressed stays pressed, the extraction stopped.</summary>
    private void ApplyCriteriaTips()
    {
        this._toolTip.SetToolTip(this._byName, CriterionTip(Strings.ExplorerCriterionName, this._byName.Checked, this._byContent.Checked, null));
        this._toolTip.SetToolTip(this._byContent, CriterionTip(
            Strings.ExplorerCriterionContent,
            this._byContent.Checked,
            this._byName.Checked,
            this._contentSearch ? null : Strings.ExplorerCriterionExtractionStopped));
    }

    private static string CriterionTip(string what, bool on, bool otherOn, string? note)
    {
        string state = !on ? Strings.ExplorerCriterionOff
            : otherOn ? Strings.ExplorerCriterionOn
            : Strings.ExplorerCriterionLastOn;
        return note is null ? Texts.Format(Strings.ExplorerCriterionTip, what, state) : Texts.Format(Strings.ExplorerCriterionTipNote, what, state, note);
    }

    /// <summary>Opens a folder of the folder view — its content from the top, the search box cleared — and remembers it.</summary>
    private void Navigate(string relative, string? select = null)
    {
        _openFolder = relative;
        if (_search.TextLength > 0)
        {
            _clearingSearch = true;
            try
            {
                _search.Text = "";
            }
            finally
            {
                _clearingSearch = false;
            }
        }

        RefreshRows(select: select);
        FolderViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>↑, Backspace or Alt+↑: the parent folder, the folder just left selected.</summary>
    private void GoUp()
    {
        if (this._giphyView)
        {
            this.ShowGiphyTrending();
        }
        else if (this._peopleView && this._openGroup is not null)
        {
            this.OpenGroup(null);
        }
        else if (_folderView && _baseFolder is not null && this._openSource is not null && _openFolder.Length == 0)
        {
            this.LeaveSource();
        }
        else if (_folderView && this.ViewRoot is { } root && _openFolder.Length > 0)
        {
            Navigate(FolderTree.Parent(_openFolder), Path.Combine(root, _openFolder));
        }
    }

    /// <summary>A folder of the breadcrumb clicked (0: the base folder), the one below it on the way to the open folder selected.</summary>
    private void OpenFolderAtDepth(int depth)
    {
        if (this._giphyView)
        {
            if (depth == 0)
            {
                this.ShowGiphyTrending();
            }

            return;
        }

        if (this._peopleView)
        {
            if (depth == 0)
            {
                this.OpenGroup(null);
            }

            return;
        }

        if (_baseFolder is null)
        {
            return;
        }

        // Inside a secondary source: the base folder's name leaves it, the source's name is its root.
        if (this.ViewSource is { } source)
        {
            if (depth == 0)
            {
                this.LeaveSource();
                return;
            }

            string[] inner = SegmentsOf(_openFolder)[2..];
            if (depth - 1 >= inner.Length)
            {
                return;
            }

            Navigate(string.Join('\\', inner[..(depth - 1)]), Path.Combine(source.Folder, string.Join('\\', inner[..depth])));
            return;
        }

        string[] path = SegmentsOf(_openFolder)[1..];
        if (depth >= path.Length)
        {
            return;
        }

        Navigate(string.Join('\\', path[..depth]), Path.Combine(_baseFolder, string.Join('\\', path[..(depth + 1)])));
    }

    /// <summary>A load's slots: the pages per load times what the list shows at once.</summary>
    private int LoadSize() => _pagesPerLoad * _grid.PageSize;

    /// <summary>The first load's tiles: its slots but the last, left to the Loading… slot.</summary>
    private int FirstLoad() => LoadSize() - 1;

    /// <summary>
    /// The Loading… slot came into view: the next load, the first new tile in its slot, a new slot
    /// ending the load a load further.
    /// </summary>
    private void LoadMore()
    {
        if (this._giphyView)
        {
            this.LoadMoreGiphy();
        }
        else if (_loaded < _list.Count)
        {
            ShowLoaded(_loaded + LoadSize(), keepPlace: true);
        }
    }

    /// <summary>
    /// Shows the first <paramref name="count"/> tiles of the list and the Loading… slot after them —
    /// or the whole list, without the slot, when the rest would fit in it and no online library holds more.
    /// </summary>
    private void ShowLoaded(int count, bool keepPlace)
    {
        _loaded = count + 1 >= _list.Count ? _list.Count : Math.Max(1, count);
        bool hasMore = _loaded < _list.Count || (this._giphyView && this._giphyHasMore);
        var rows = _loaded < _list.Count ? _list.Take(_loaded).ToArray() : _list;
        _grid.SetRows(rows, hasMore, keepPlace);
    }

    /// <summary>The tiles loaded, the Loading… slot aside.</summary>
    private IReadOnlyList<ExplorerRow> LoadedRows => _grid.Rows;

    /// <summary>Enter activates the first tile; ↓ moves to the grid.</summary>
    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter when this._giphyView:
                // The GIPHY view searches on Enter only: typing costs none of the key's calls.
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (this._search.Text.Trim() != (this._giphyQuery ?? "") || this._list.Count == 0)
                {
                    this.SearchGiphy(this._search.Text);
                }

                break;
            case Keys.Enter:
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (_grid.Visible && LoadedRows.Count > 0)
                {
                    Activate(LoadedRows[0]);
                }

                break;
            case Keys.Up when e.Alt:
                e.Handled = true;
                e.SuppressKeyPress = true;
                GoUp();
                break;
            case Keys.Down when _grid.Visible && LoadedRows.Count > 0:
                e.Handled = true;
                if (_grid.SelectedRow is null)
                {
                    _grid.SelectFirst();
                }

                _grid.Focus();
                break;
            case Keys.Escape:
                e.Handled = true;
                e.SuppressKeyPress = true;
                this.SearchEscaped?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>A file tile is added like Add images; a folder tile opens its folder.</summary>
    private void Activate(ExplorerRow row)
    {
        if (row.Online is not null)
        {
            _ = this.ActivateGiphyAsync(row);
            return;
        }

        if (!Exists(row))
        {
            return;
        }

        if (row.IsGroup)
        {
            // A sub-group is a part of the group open: its files are listed there already.
            if (IsPeopleTile(row.GroupId) || this._snapshot.ById.GetValueOrDefault(row.GroupId!) is { Parent: null })
            {
                this.OpenGroup(row.GroupId);
            }
        }
        else if (row.IsFolder)
        {
            // A secondary source's tile, at the base folder's root, opens the source.
            if (this.ViewSource is null && row.InSource && this.SourceAt(row.FullPath) is { } source)
            {
                this.OpenSource(source);
            }
            else if (this.ViewRoot is { } root)
            {
                Navigate(Path.GetRelativePath(root, row.FullPath));
            }
        }
        else if (row.Preset is { } preset)
        {
            this.PresetActivated?.Invoke(this, (row.FullPath, preset.Effects.ToJsonString()));
        }
        else
        {
            FileActivated?.Invoke(this, row.FullPath);
        }
    }

    /// <summary>
    /// A file tile dragged past the threshold becomes a file drag, like one from the Explorer; a folder tile
    /// does not drag. A preset's carries its effects beside the file (<see cref="PresetDataFormat"/>):
    /// another app gets the file alone.
    /// </summary>
    private void StartDrag(ExplorerRow row)
    {
        if (row.Online is not null)
        {
            this.StartGiphyDrag(row);
            return;
        }

        // A face group dragged onto another merges into it: inside the panel only.
        if (row.IsGroup)
        {
            if (!IsPeopleTile(row.GroupId))
            {
                this._draggingOwnTile = true;
                try
                {
                    var group = new DataObject();
                    group.SetData(GroupDataFormat, row.GroupId);
                    this._grid.DoDragDrop(group, DragDropEffects.Move);
                }
                finally
                {
                    this._draggingOwnTile = false;
                    this._grid.DropTargetKey = null;
                }
            }

            return;
        }

        if (!row.IsFolder && Exists(row))
        {
            _draggingOwnTile = true;
            try
            {
                var data = new DataObject(DataFormats.FileDrop, new[] { row.FullPath });
                if (row.Preset is { } preset)
                {
                    data.SetData(PresetDataFormat, preset.Effects.ToJsonString());
                }

                // A face's tile also drops onto a face group, joining it.
                if (row.FaceKey is { } face)
                {
                    data.SetData(FaceDataFormat, face);
                }

                _grid.DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
            }
            finally
            {
                _draggingOwnTile = false;
                this._grid.DropTargetKey = null;
            }
        }
    }

    /// <summary>The URL zone's media dragged into a cell as a tile is: a file drag the panel refuses as a favorite.</summary>
    private void StartUrlDrag(string path)
    {
        this._draggingOwnTile = true;
        try
        {
            this._urlZone.DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { path }), DragDropEffects.Copy);
        }
        finally
        {
            this._draggingOwnTile = false;
        }
    }

    /// <summary>
    /// The heart of a tile. A favorite of <see cref="PastedFavorites.Folder"/> un-hearted goes to the
    /// Recycle Bin: the app made that file for the favorite.
    /// </summary>
    private void ToggleFavorite(ExplorerRow row)
    {
        if (row.Online is { } item)
        {
            _ = this.ToggleGiphyFavoriteAsync(row, item);
            return;
        }

        if (row.Preset is { } preset)
        {
            this.DeletePreset(row, preset);
            return;
        }

        bool added = true;
        try
        {
            added = _favorites.Toggle(row.FullPath);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            ShowTransient(Texts.Format(Strings.ExplorerFavoritesNotSaved, ex.Message), error: true);
        }

        this.AfterFavoriteRemoved(row, added);
    }

    /// <summary>
    /// A preset's heart: the preset deleted once the user confirms — its description would be lost —
    /// then its file recycled if it was pasted and nothing else holds it.
    /// </summary>
    private void DeletePreset(ExplorerRow row, EffectPreset preset)
    {
        string described = preset.Description.Trim().Length > 0 ? $"\n\n« {preset.Description.Trim()} »" : "";
        if (MessageBox.Show(
                this.FindForm(),
                Texts.Format(Strings.ExplorerDeletePresetPrompt, row.Name, described),
                Strings.ExplorerDeletePresetTitle,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.OK)
        {
            return;
        }

        bool removed = false;
        try
        {
            removed = this._favorites.RemovePreset(preset.Id);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            this.ShowTransient(Texts.Format(Strings.ExplorerFavoritesNotSaved, ex.Message), error: true);
            removed = true;
        }

        this.AfterFavoriteRemoved(row, added: !removed);
    }

    /// <summary>
    /// A favorite or a preset gone: a file of <see cref="PastedFavorites.Folder"/> or <see cref="GiphyFiles.FavoritesFolder"/>
    /// no favorite holds any more goes to the Recycle Bin, and the list follows.
    /// </summary>
    private void AfterFavoriteRemoved(ExplorerRow row, bool added)
    {
        this.RecycleUnheld(row.FullPath, added);

        if (ShowingFavorites || (!added && row.Preset is not null))
        {
            // The favorites list, or a search showing a deleted preset: the tile leaves it, the place kept —
            // once the grid is done with the click.
            BeginInvoke(() => RefreshRows(keepPlace: true));
        }
        else
        {
            _grid.InvalidateRow(row);
        }
    }

    /// <summary>The selected preset's description edited in the preset dialog — its effects stay as saved.</summary>
    private void EditDescription()
    {
        if (this._grid.SelectedRow is not { Preset: { } preset } row
            || PresetLookOf(preset.Effects.ToJsonString()) is not { } look)
        {
            return;
        }

        using var dialog = new EffectPresetDialog(row.Name, LookPreset.Describe(look), preset.Description, EffectPresetDialog.Purpose.Edit);
        if (dialog.ShowDialog(this.FindForm()) != DialogResult.OK)
        {
            return;
        }

        try
        {
            this._favorites.Describe(preset.Id, dialog.Description);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            this.ShowTransient(Texts.Format(Strings.ExplorerFavoritesNotSaved, ex.Message), error: true);
        }

        this.RefreshRows(keepPlace: true);
    }

    private void OpenLocation()
    {
        if (_grid.SelectedRow is not { } row || !Exists(row))
        {
            return;
        }

        this.OpenInExplorer(row.FullPath, row.IsFolder);
    }

    /// <summary>The Explorer opened on the folder at <paramref name="path"/>, or on a file's folder, the file selected.</summary>
    private void OpenInExplorer(string path, bool folder)
    {
        try
        {
            Process.Start("explorer.exe", folder ? $"\"{path}\"" : $"/select,\"{path}\"")?.Dispose();
        }
        catch (Win32Exception ex)
        {
            ShowTransient(Texts.Format(Strings.ExplorerOpenFailed, ex.Message), error: true);
        }
    }

    /// <summary>
    /// Whether the tile's file is still there. A missing one leaves the index — the file rewritten —
    /// and the favorites, the grid refreshed, and the status line says so for a few seconds.
    /// </summary>
    private bool Exists(ExplorerRow row)
    {
        // A GIPHY result is no file: its own routes fetch it (FileExplorerPanel.Giphy.cs).
        if (row.Online is not null)
        {
            return false;
        }

        // A group stands for faces, not a file: its files are checked when they are used.
        if (row.IsGroup)
        {
            return true;
        }

        if (row.IsFolder)
        {
            if (Directory.Exists(row.FullPath))
            {
                return true;
            }

            // A folder read from the disk, or derived from the index: the list is read again.
            RefreshRows(keepPlace: true);
            ShowTransient(Strings.ExplorerFolderNotFound);
            return false;
        }

        if (File.Exists(row.FullPath))
        {
            return true;
        }

        // A preset is kept, its description never dropped silently: only its heart removes it.
        if (row.Preset is not null)
        {
            this.ShowTransient(Texts.Format(Strings.ExplorerFileNotFoundPreset, row.FullPath), error: true);
            return false;
        }

        string? error = null;
        var at = this.Locate(row.FullPath);
        if (at is { } located && this.IndexAt(located.Source) is { } index && index.Remove(row.FullPath))
        {
            // The base folder's index or a secondary source's, written back into its own files.
            if (located.Source is { } source)
            {
                source.SetIndex(index);
            }
            else
            {
                _tree = null;
                this._entriesByPath = null;
            }

            this.ContentsAt(located.Source)?.Remove(located.Relative);
            try
            {
                index.Save((located.Source?.Files ?? IndexFiles.Main).Index);
            }
            catch (Exception ex) when (FileIndex.IsFileError(ex))
            {
                error = ex.Message;
            }
        }

        try
        {
            _favorites.Remove(row.FullPath);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            error ??= ex.Message;
        }

        RefreshRows(keepPlace: true);
        ShowTransient(error is null ? Strings.ExplorerFileNotFoundRemoved : Texts.Format(Strings.ExplorerFileNotFoundRemovedNotSaved, error), error: error is not null);
        return false;
    }

    /// <summary>What the invitation's button does: the base folder chosen, the face analysis turned on, the face engine downloaded.</summary>
    private enum InviteAction
    {
        ChooseFolder,
        TurnOnFaces,
        DownloadFaceEngine,
        GiphyKey,
    }

    /// <summary>The invitation's button's icon: the key for GIPHY's, the folder otherwise.</summary>
    private UiIcon InviteIcon => this._inviteAction == InviteAction.GiphyKey ? UiIcons.ApiKey : UiIcons.Folder;

    /// <summary>The invitation's text and button for <paramref name="action"/>.</summary>
    private void SetInvite(InviteAction action)
    {
        if (action == this._inviteAction)
        {
            return;
        }

        this._inviteAction = action;
        (this._inviteText.Text, this._chooseFolder.Text) = action switch
        {
            InviteAction.TurnOnFaces => (Strings.ExplorerInviteFacesOff, Strings.ExplorerInviteFacesOffButton),
            InviteAction.DownloadFaceEngine => (Strings.ExplorerInviteFaceEngine, Strings.ExplorerInviteFaceEngineButton),
            InviteAction.GiphyKey => (Strings.ExplorerInviteGiphy, Strings.ExplorerInviteGiphyButton),
            _ => (FolderInviteText, Strings.ExplorerChooseFolder),
        };
        this._inviteLink.Visible = action == InviteAction.GiphyKey;
        if (this.IsHandleCreated)
        {
            UiIcons.SetImage(this._chooseFolder, this.InviteIcon, this.LogicalToDeviceUnits(UiIcons.LogicalSize));
        }
    }

    /// <summary>The invitation's button: what it offers asked of the window, as from the ⚙ menu.</summary>
    private void RunInvite()
    {
        switch (this._inviteAction)
        {
            case InviteAction.TurnOnFaces:
                this.FaceAnalysisRequested?.Invoke(this, EventArgs.Empty);
                break;
            case InviteAction.DownloadFaceEngine:
                this.FaceEngineRequested?.Invoke(this, EventArgs.Empty);
                break;
            case InviteAction.GiphyKey:
                this.GiphyKeyRequested?.Invoke(this, EventArgs.Empty);
                break;
            default:
                this.ChooseFolderRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>
    /// The People view's list (workfiles/20261027-face-detection.md § People View): the face groups — named
    /// A→Z, then the others, the larger first — a tile for the hidden ones, a tile for the low-quality faces
    /// (workfiles/20261028-face-grouping-accuracy.md § Low Quality Tile), then the clear faces close to no
    /// other, each a file tile showing its face; a search keeping the groups whose name holds its words. A group
    /// open: the groups merged into it, then every file of it and of them, in the sort order — a search
    /// keeping the files it finds there. While nothing was found yet, the invitation says what is missing.
    /// </summary>
    private void RefreshPeopleRows(bool keepPlace, string? select, string[] words)
    {
        var snapshot = this._snapshot;
        bool nothing = snapshot.Groups.Count == 0 && snapshot.Hidden.Count == 0 && snapshot.Ungrouped.Count == 0 && snapshot.LowQuality.Count == 0;
        InviteAction? invite = this._baseFolder is null ? InviteAction.ChooseFolder
            : !nothing ? null
            : !this._faceAnalysis ? InviteAction.TurnOnFaces
            : !FaceEngineFiles.IsAvailable ? InviteAction.DownloadFaceEngine
            : null;
        if (invite is { } action)
        {
            this.SetInvite(action);
        }

        this._invite.Visible = invite is not null;
        this._grid.Visible = invite is null;
        if (invite is not null)
        {
            this.ShowGroupTitle(null);
            this.ShowRows([], keepPlace, null);
            this.ShowPeopleBreadcrumb(null, "");
            return;
        }

        if (this._openGroup is HiddenGroups)
        {
            this.ShowGroupTitle(null);
            var hidden = snapshot.Hidden.Where(group => NameHolds(group, words)).Select(GroupRow).ToList();
            this.ShowRows(hidden, keepPlace, select);
            this.ShowPeopleBreadcrumb(Strings.ExplorerHidden, Groups(hidden.Count));
            return;
        }

        if (this._openGroup is LowQualityFaces)
        {
            // A search in the People view looks for names: a face without a person has none.
            List<ExplorerRow> poor = words.Length > 0 ? [] : [.. snapshot.LowQuality.Select(this.FaceRow)];
            this.ShowRows(poor, keepPlace, select);
            this.ShowPeopleBreadcrumb(Strings.ExplorerLowQuality, Faces(poor.Count));
            return;
        }

        if (this._openGroup is { } id)
        {
            // An unnamed group's id is its first face's: followed by the face it was opened by.
            if (snapshot.ById.GetValueOrDefault(id) is not { Parent: null } && this._openGroupFace is { } face
                && snapshot.OwnerOf.GetValueOrDefault(face) is { } owner)
            {
                this._openGroup = id = (owner.Parent ?? owner).Id;
            }

            if (snapshot.ById.GetValueOrDefault(id) is not { Parent: null } open)
            {
                // Gone with a new grouping — its faces moved elsewhere: back to the people.
                this._openGroup = null;
            }
            else
            {
                this.ShowGroupTitle(open);
                var rows = open.SubGroups.Select(GroupRow).ToList();
                var files = this.GroupFiles(open, words);
                rows.AddRange(files);
                this.ShowRows(rows, keepPlace, select);
                this.ShowPeopleBreadcrumb(GroupName(open), open.SubGroups.Count > 0 ? Texts.Format("{0}, {1}", Groups(open.SubGroups.Count), Files(files.Count)) : Files(files.Count));
                return;
            }
        }

        this.ShowGroupTitle(null);
        var list = snapshot.Groups.Where(group => NameHolds(group, words)).Select(GroupRow).ToList();
        int groups = list.Count;
        if (words.Length == 0)
        {
            if (snapshot.Hidden.Count > 0)
            {
                var best = snapshot.Hidden[0].Best;
                list.Add(new ExplorerRow(best.FullPath, Texts.Format("{0} ({1:N0})", Strings.ExplorerHidden, snapshot.Hidden.Count), GroupId: HiddenGroups, Face: best.Face.Bounds));
            }

            if (snapshot.LowQuality.Count > 0)
            {
                var best = snapshot.LowQuality.MaxBy(face => face.Quality)!;
                list.Add(new ExplorerRow(best.FullPath, Texts.Format("{0} ({1:N0})", Strings.ExplorerLowQuality, snapshot.LowQuality.Count), GroupId: LowQualityFaces, Face: best.Face.Bounds));
            }

            list.AddRange(snapshot.Ungrouped.Select(this.FaceRow));
        }

        this.ShowRows(list, keepPlace, select);
        string looking = this._facePassRunning ? Strings.ExplorerLookingForFaces : "";
        this.ShowPeopleBreadcrumb(null, words.Length > 0 ? Groups(groups) : Texts.Format(Strings.ExplorerGroupsAndFacesAlone, Groups(groups), Faces(snapshot.Ungrouped.Count), looking));
    }

    private static string Groups(int count) => count == 1 ? Strings.ExplorerGroupOne : Texts.Format(Strings.ExplorerGroupMany, count);

    private static string Faces(int count) => count == 1 ? Strings.ExplorerFaceOne : Texts.Format(Strings.ExplorerFaceMany, count);

    /// <summary>A face close to no other: a file tile showing the face cut out of its file.</summary>
    private ExplorerRow FaceRow(FaceRef face) =>
        new(face.FullPath, Path.GetFileName(face.FullPath), Size: this.AnyEntryOf(face.FullPath)?.Entry.Size, Face: face.Face.Bounds, FaceKey: face.Key);

    /// <summary>A group's name, or <c>Unnamed</c>.</summary>
    private static string GroupName(FaceGroup group) => group.Name.Length > 0 ? group.Name : Strings.ExplorerUnnamed;

    /// <summary>Whether a group's name holds every word of a search, accents and case ignored; any group without one.</summary>
    private static bool NameHolds(FaceGroup group, string[] words)
    {
        string folded = FileSearch.Fold(group.Name);
        return words.All(word => folded.Contains(word, StringComparison.Ordinal));
    }

    /// <summary>A group's tile: its best face's picture, its name, its files counted.</summary>
    private static ExplorerRow GroupRow(FaceGroup group) =>
        new(group.Best.FullPath, GroupName(group), Count: group.FileCount, GroupId: group.Id, Face: group.Best.Face.Bounds);

    /// <summary>
    /// The files of a group and of its sub-groups, once each — the face it stands for, the first found in
    /// it — in the sort order; those a search finds there when <paramref name="words"/> are typed.
    /// </summary>
    private List<ExplorerRow> GroupFiles(FaceGroup group, string[] words)
    {
        var faceOf = new Dictionary<string, FaceRef>(StringComparer.OrdinalIgnoreCase);
        foreach (var face in group.AllFaces)
        {
            faceOf.TryAdd(face.FullPath, face);
        }

        // The files of the base folder and of the secondary sources alike, each searched in its own index's texts.
        var located = faceOf.Keys.Select(path => (Path: path, Found: this.AnyEntryOf(path))).Where(item => item.Found is not null).ToArray();
        var pathOf = new Dictionary<IndexEntry, string>(ReferenceEqualityComparer.Instance);
        foreach (var item in located)
        {
            pathOf[item.Found!.Value.Entry] = item.Path;
        }

        IEnumerable<IndexEntry> listed;
        if (words.Length > 0)
        {
            bool everything = FileSearch.IsEverything(this._search.Text);
            listed = located.GroupBy(item => item.Found!.Value.Source).SelectMany(root =>
                this.Find([.. root.Select(item => item.Found!.Value.Entry)], words, everything, this.ContentsAt(root.Key), this.VisionAt(root.Key)).Select(match => match.Entry));
        }
        else
        {
            listed = FileSearch.All([.. located.Select(item => item.Found!.Value.Entry)], this._fileOrder);
        }

        return [.. listed.Select(entry =>
        {
            string path = pathOf[entry];
            return new ExplorerRow(path, entry.Name, Size: entry.Size, FaceKey: faceOf[path].Key);
        })];
    }

    /// <summary>The index's entry of a file, by its full path; null for one the index does not hold.</summary>
    private IndexEntry? EntryOf(string fullPath)
    {
        if (this._index is not { } index)
        {
            return null;
        }

        this._entriesByPath ??= index.Entries.ToDictionary(index.FullPath, StringComparer.OrdinalIgnoreCase);
        return this._entriesByPath.GetValueOrDefault(fullPath);
    }

    /// <summary>The breadcrumb in the People view: <c>People</c>, then the group open, <paramref name="trailing"/> after it.</summary>
    private void ShowPeopleBreadcrumb(string? open, string trailing) =>
        this._breadcrumb.SetPath(open is null ? [Strings.ExplorerPeople] : [Strings.ExplorerPeople, open], trailing);

    /// <summary>The title at the top of the list: the group open's name, or <c>Unnamed</c>; none for <c>null</c>.</summary>
    private void ShowGroupTitle(FaceGroup? group)
    {
        if (group is not null)
        {
            this._groupTitle.Text = GroupName(group);
        }

        this._groupTitle.Visible = group is not null;
    }

    /// <summary>Opens a group of the People view — or the hidden ones, or none — from the top, the search box cleared.</summary>
    private void OpenGroup(string? id)
    {
        this._openAfterRegroup = null;
        string? left = this._openGroup;
        this._openGroup = id;
        this._openGroupFace = id is null ? null : this._snapshot.ById.GetValueOrDefault(id)?.Best.Key;
        if (this._search.TextLength > 0)
        {
            this._clearingSearch = true;
            try
            {
                this._search.Text = "";
            }
            finally
            {
                this._clearingSearch = false;
            }
        }

        this.RefreshRows();
        if (id is null && left is not null)
        {
            this._grid.SelectKey("group:" + left);
        }
    }

    /// <summary>The People view's list again, its place kept, when it shows.</summary>
    private void RefreshPeopleIfShown()
    {
        if (this._peopleView)
        {
            this.RefreshRows(keepPlace: true);
        }
    }

    /// <summary>
    /// The groups computed again off the UI thread — from the faces found and the people's decisions — then
    /// the People view refreshed; unless nothing changed since the last time (<paramref name="force"/>: a
    /// decision did). One computation at a time, a change meanwhile computing again once it ends.
    /// </summary>
    private void Regroup(bool force = false) => _ = this.RegroupAsync(force);

    private async Task RegroupAsync(bool force)
    {
        if (this._faces is not { } faces || this._index is not { } index)
        {
            return;
        }

        if (this._grouping)
        {
            this._groupAgain = true;
            return;
        }

        // The secondary sources' faces join the base folder's (workfiles/20261105-secondary-file-sources.md § Faces in the People View).
        var sources = this._sources.Where(source => source.Faces is not null && source.Index is not null)
            .Select(source => (Index: source.Index!, Faces: source.Faces!)).ToArray();
        var changed = new HashCode();
        changed.Add(faces.Changes);
        foreach (var (sourceIndex, sourceFaces) in sources)
        {
            changed.Add(sourceIndex);
            changed.Add(sourceFaces);
            changed.Add(sourceFaces.Changes);
        }

        int changes = changed.ToHashCode();
        if (!force && changes == this._groupedAt)
        {
            return;
        }

        this._grouping = true;
        var people = this._peopleStore.Snapshot();
        float threshold = FaceGrouping.ThresholdOf(this._faceGrouping);
        PeopleSnapshot snapshot;
        try
        {
            snapshot = await Task.Run(() =>
            {
                var found = new List<FaceRef>();
                foreach (var (root, store) in sources.Prepend((index, faces)))
                {
                    var entries = root.Entries.ToDictionary(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase);
                    foreach (var (relative, boxes) in store.WithFaces())
                    {
                        if (entries.TryGetValue(relative, out var entry))
                        {
                            string path = root.FullPath(entry);
                            found.AddRange(boxes.Select(box => new FaceRef(path, box, entry.Created)));
                        }
                    }
                }

                return FaceGrouping.Compute(found, people, threshold);
            });
        }
        finally
        {
            this._grouping = false;
        }

        if (this.IsDisposed)
        {
            return;
        }

        if (faces != this._faces || index != this._index)
        {
            // Replaced meanwhile — the rescan at start-up replaces the cached index: the groups computed
            // again on what replaced them, never left empty.
            this._groupAgain = false;
            this.Regroup(force: true);
            return;
        }

        this._snapshot = snapshot;
        this._groupedAt = changes;
        if (this._openAfterRegroup is { } created && this._peopleView && snapshot.ById.ContainsKey(created))
        {
            this.OpenGroup(created);
        }
        else
        {
            this.RefreshPeopleIfShown();
        }

        if (this._groupAgain)
        {
            this._groupAgain = false;
            this.Regroup(force: true);
        }
    }

    /// <summary>The person behind a group — created from its faces, pinned, for a group only the grouping made.</summary>
    private Person PersonOf(FaceGroup group) =>
        group.PersonId is { } id && this._peopleStore.Find(id) is { } person
            ? person
            : this._peopleStore.Create(group.Faces.Select(face => (face.Key, face.Face.Embedding)));

    /// <summary>
    /// A decision on the people taken: their state as the undo history reads it, then written, the status
    /// line saying <paramref name="done"/>, the groups computed again.
    /// </summary>
    private void SavePeople(string done)
    {
        this.PeopleState = new PeopleState(this._peopleStore.Snapshot(), this.PeopleState.Generation, done);
        this.WritePeople(done, quiet: false);
    }

    /// <summary>
    /// The people put back as an undo or a redo found them — written, the groups computed again — unless
    /// <paramref name="state"/> belongs to another generation (§ Undo: loading and Forget faces… are never
    /// undone); false then, the people left as they are.
    /// </summary>
    public bool RestorePeople(PeopleState state)
    {
        if (state.Generation != this.PeopleState.Generation)
        {
            return false;
        }

        this._peopleStore.Restore(state.People);
        this.PeopleState = state;
        this.WritePeople(Strings.ExplorerPeopleRestored, quiet: true);
        return true;
    }

    /// <summary>
    /// The people as the store holds them taken as a new generation — loaded, or forgotten — no decision
    /// of the user: the undo history takes it without a step (<see cref="PeopleReset"/>).
    /// </summary>
    private void ResetPeopleState()
    {
        this.PeopleState = new PeopleState(this._peopleStore.Snapshot(), this.PeopleState.Generation + 1, "");
        this.PeopleReset?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The people written, the status line saying <paramref name="done"/> — only a failure when <paramref name="quiet"/> — the groups computed again.</summary>
    private void WritePeople(string done, bool quiet)
    {
        string? error = null;
        try
        {
            this._peopleStore.Save();
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            error = ex.Message;
        }

        if (!quiet || error is not null)
        {
            this.Report(error is null ? done : Texts.Format(Strings.ExplorerForSessionNotSaved, done, error), error is not null);
        }

        this.Regroup(force: true);
    }

    /// <summary>The group a tile stands for; null for any other tile, the hidden ones' and the low-quality faces' tiles included.</summary>
    private FaceGroup? GroupOf(ExplorerRow? row) =>
        row is { IsGroup: true } && !IsPeopleTile(row.GroupId) ? this._snapshot.ById.GetValueOrDefault(row.GroupId!) : null;

    /// <summary>Whether a tile's group id is the hidden ones' or the low-quality faces' tile: opened, never a person.</summary>
    private static bool IsPeopleTile(string? groupId) => groupId is HiddenGroups or LowQualityFaces;

    /// <summary>Rename… or F2 on a group's tile: its name asked, kept.</summary>
    private void RenameGroup(ExplorerRow row)
    {
        if (this.GroupOf(row) is not { } group)
        {
            return;
        }

        this.Rename(group);
    }

    /// <summary>The title of the group open clicked: its name asked, kept, as Rename… does.</summary>
    private void RenameOpenGroup()
    {
        if (this._openGroup is { } id && id != HiddenGroups && this._snapshot.ById.GetValueOrDefault(id) is { } group)
        {
            this.Rename(group);
        }
    }

    /// <summary>A group's name asked in a popup centered on the explorer, then kept.</summary>
    private void Rename(FaceGroup group)
    {
        using var dialog = new NameDialog(Strings.ExplorerRenameTitle, Strings.ExplorerRenamePrompt, group.Name);
        dialog.CenterOn(this);
        if (dialog.ShowDialog(this.FindForm()) != DialogResult.OK)
        {
            return;
        }

        var person = this.PersonOf(group);
        person.Name = dialog.Value;
        this.SavePeople(person.Name.Length > 0 ? Texts.Format(Strings.ExplorerNamed, person.Name) : Strings.ExplorerNameRemoved);
    }

    /// <summary>
    /// The People submenu of a file's tile: one entry per person of its faces (<see cref="PhotoPeople"/>) —
    /// its face cut out of the file, loaded off the UI thread, its name and its files; hidden for a file
    /// without one, and while the face analysis is off.
    /// </summary>
    private void FillPhotoPeople(ExplorerRow? row)
    {
        var old = this._photoPeople.DropDownItems.Cast<ToolStripItem>().ToArray();
        this._photoPeople.DropDownItems.Clear();
        foreach (var item in old)
        {
            item.Image?.Dispose();
            item.Dispose();
        }

        IReadOnlyList<PhotoPerson> people = [];
        if (row is not null && this._faceAnalysis && this.Locate(row.FullPath) is { } at && this.FacesAt(at.Source) is { } faces)
        {
            people = PhotoPeople.Of(this._snapshot, row.FullPath, faces.FacesOf(at.Relative));
        }

        this._photoPeople.Visible = people.Count > 0;
        if (people.Count == 0)
        {
            return;
        }

        var box = new Size(this.LogicalToDeviceUnits(PhotoFaceWidth), this.LogicalToDeviceUnits(PhotoFaceWidth * 3 / 4));
        var entries = people.Select(person =>
        {
            // A blank picture meanwhile, so the entries keep their height when the faces come.
            var entry = new ToolStripMenuItem(PhotoPersonText(person), new Bitmap(box.Width, box.Height)) { ImageScaling = ToolStripItemImageScaling.None };
            entry.Click += (_, _) => this.OpenPhotoPerson(person);
            return entry;
        }).ToArray();
        this._photoPeople.DropDownItems.AddRange(entries);
        _ = this.LoadPhotoFacesAsync(entries, people, box);
    }

    /// <summary>An entry of the People submenu: <c>Alice (12 files)</c>, <c>Unnamed (1 file)</c>, <c>(hidden)</c> after a hidden one.</summary>
    private static string PhotoPersonText(PhotoPerson person) =>
        Texts.Format("{0} ({1}){2}", person.Name.Length > 0 ? person.Name : Strings.ExplorerUnnamed, Files(person.FileCount), person.Hidden ? Strings.ExplorerHiddenSuffix : "");

    /// <summary>The faces of the People submenu cut out of their file one after the other, each shown as it comes.</summary>
    private async Task LoadPhotoFacesAsync(ToolStripMenuItem[] entries, IReadOnlyList<PhotoPerson> people, Size box)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            var face = people[i].Face;
            Bitmap? picture = null;
            try
            {
                picture = await Task.Run(() => FaceCrops.Load(face.FullPath, face.Face.Bounds, box));
            }
            catch (Exception ex) when (ex is ExternalException or ArgumentException or OutOfMemoryException)
            {
            }

            if (picture is null)
            {
                continue;
            }

            if (entries[i].IsDisposed)
            {
                picture.Dispose();
                continue;
            }

            var blank = entries[i].Image;
            entries[i].Image = picture;
            blank?.Dispose();
        }

        FaceCrops.Release();
    }

    /// <summary>
    /// An entry of the People submenu clicked: the People view shows, the person's group open; a face
    /// alone first made a person of its own — its face pinned, saved — opened once the groups hold it.
    /// </summary>
    private void OpenPhotoPerson(PhotoPerson person)
    {
        this.ChangeMode(ExplorerMode.People);
        if (person.Group is { } group)
        {
            this.OpenGroup(group.Id);
            return;
        }

        var created = this._peopleStore.Create([(person.Face.Key, person.Face.Face.Embedding)]);
        this._openAfterRegroup = created.Id;
        this.SavePeople(Strings.ExplorerNewPerson);
    }

    /// <summary>Hide or Unhide on a group's tile: it leaves the view for the hidden ones' tile, or comes back.</summary>
    private void SetGroupHidden(ExplorerRow? row, bool hidden)
    {
        if (this.GroupOf(row) is not { Parent: null } group)
        {
            return;
        }

        this.PersonOf(group).Hidden = hidden;
        this.SavePeople(hidden ? Texts.Format(Strings.ExplorerDecisionHidden, GroupName(group)) : Texts.Format(Strings.ExplorerDecisionShown, GroupName(group)));
    }

    /// <summary>Un-merge on a sub-group's tile: a group of its own again, as it was before the merge.</summary>
    private void UnmergeGroup(ExplorerRow? row)
    {
        if (this.GroupOf(row) is not { Parent: { } parent } group || group.PersonId is not { } id || this._peopleStore.Find(id) is not { } person)
        {
            return;
        }

        person.MergedInto = null;
        this.SavePeople(Texts.Format(Strings.ExplorerDecisionUnmerged, GroupName(group), GroupName(parent)));
    }

    /// <summary>
    /// Not <c>name</c> on a file inside an open group: its face of that group — or of the sub-group it
    /// belongs to — excluded from it for good, back to the automatic grouping.
    /// </summary>
    private void RemoveFace(ExplorerRow? row)
    {
        if (row?.FaceKey is not { } key || this._snapshot.OwnerOf.GetValueOrDefault(key) is not { } owner)
        {
            return;
        }

        People.Exclude(key, this.PersonOf(owner));
        this.SavePeople(Texts.Format(Strings.ExplorerDecisionNotPerson, row.Name, GroupName(owner)));
    }

    /// <summary>
    /// The group tile a drag of the People view's own tiles would drop onto: for a group, another group of
    /// the top level — not the one it is in — for a face, any group but its own; null for none.
    /// </summary>
    private ExplorerRow? PeopleDropTarget(DragEventArgs e)
    {
        if (!this._peopleView || e.Data is not { } data
            || this._grid.RowAt(this._grid.PointToClient(new Point(e.X, e.Y))) is not { } row
            || this.GroupOf(row) is not { } target)
        {
            return null;
        }

        if (data.GetData(GroupDataFormat) is string id)
        {
            return id != target.Id && target.Parent is null && this._snapshot.ById.GetValueOrDefault(id)?.Parent != target ? row : null;
        }

        return data.GetData(FaceDataFormat) is string key && this._snapshot.OwnerOf.GetValueOrDefault(key) != target ? row : null;
    }

    /// <summary>
    /// A drop onto a group's tile: a group merged into it once the user confirms, shown inside it as a
    /// sub-group; a face added to it, out of the group it was in.
    /// </summary>
    private void DropOnGroup(string? groupId, string? faceKey, ExplorerRow targetRow)
    {
        if (this.GroupOf(targetRow) is not { } target)
        {
            return;
        }

        if (groupId is not null && this._snapshot.ById.GetValueOrDefault(groupId) is { } source)
        {
            if (MessageBox.Show(
                    this.FindForm(),
                    Texts.Format(Strings.ExplorerMergePrompt, GroupName(source), GroupName(target)),
                    Strings.ExplorerMergeTitle,
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }

            var into = this.PersonOf(target);
            this._peopleStore.Merge(this.PersonOf(source), into);
            this.SavePeople(Texts.Format(Strings.ExplorerDecisionMerged, GroupName(source), GroupName(target)));
            return;
        }

        if (faceKey is not null && this._snapshot.FaceByKey.GetValueOrDefault(faceKey) is { } face)
        {
            this._peopleStore.Pin(faceKey, face.Face.Embedding, this.PersonOf(target));
            this.SavePeople(Texts.Format(Strings.ExplorerDecisionFaceAdded, Path.GetFileName(face.FullPath), GroupName(target)));
        }
    }
}

/// <summary>
/// Where the content extraction stands: the files done out of those to do, the file it reads now
/// (null while it waits or between two), how it has it, and whether the search shown is to be refreshed.
/// </summary>
/// <summary>
/// Where a pass stands: the file it reads — its rank <paramref name="Done"/> among the
/// <paramref name="Total"/> the pass reads this time, null while it waits — the files done out of
/// those the pass reads in the whole index, for its bar; <paramref name="Refresh"/>: the search shown
/// — or the people, for the face pass — to run again; <paramref name="Finished"/>: the pass complete or
/// skipped, its bar hidden; <paramref name="Note"/>: a trouble to say.
/// </summary>
internal readonly record struct ExtractionProgress(ContentPass Pass, int Done, int Total, int PassDone, int PassNeeded, string? File, ContentKind Kind, bool Refresh, bool Finished, string? Note = null);
