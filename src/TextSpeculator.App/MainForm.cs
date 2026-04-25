using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TextSpeculator.Core.Models;
using TextSpeculator.Core.Processing;
using TextSpeculator.Core.Readers;
using TextSpeculator.Core.Services;

namespace TextSpeculator.App;

public class MainForm : Form
{
    private static readonly Color AppBackground = Color.FromArgb(241, 245, 249);
    private static readonly Color HeaderBackground = Color.White;
    private static readonly Color SurfaceBackground = Color.White;
    private static readonly Color SurfaceAltBackground = Color.FromArgb(248, 250, 252);
    private static readonly Color BorderColor = Color.FromArgb(220, 226, 233);
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);
    private static readonly Color AccentSoft = Color.FromArgb(230, 238, 255);
    private static readonly Color PrimaryText = Color.FromArgb(31, 41, 55);
    private static readonly Color SecondaryText = Color.FromArgb(100, 116, 139);
    private const int SidebarTargetWidth = 332;
    private const int SidebarMinWidth = 290;
    private const int EditorMinWidth = 520;
    private const int HistoryListHeight = 148;

    private readonly Button _btnLoad = new();
    private readonly Button _btnActivateHistory = new();
    private readonly GhostTextBox _txtInput = new();
    private readonly SplitContainer _workspaceSplit = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly Label _lblCorpusBadge = new();
    private readonly Label _lblDocsValue = new();
    private readonly Label _lblSegmentsValue = new();
    private readonly Label _lblCandidatesValue = new();
    private readonly Label _lblCorpusDetail = new();
    private readonly Label _lblWorkspaceMeta = new();
    private readonly Label _lblCompletionTitle = new();
    private readonly Label _lblCompletionDetail = new();
    private readonly ListView _lstHistory = new();
    private readonly ColumnHeader _colHistoryCorpus = new();
    private readonly ColumnHeader _colHistoryUpdated = new();
    private readonly ListView _lstDocuments = new();
    private readonly ColumnHeader _colDocument = new();
    private readonly ColumnHeader _colType = new();
    private readonly ColumnHeader _colWords = new();
    private readonly CorpusHistoryStore _historyStore = new();

    private List<CorpusDocument> _documents = new();
    private List<DocumentSummary> _documentSummaries = new();
    private List<CorpusHistoryEntry> _historyEntries = new();
    private IReadOnlyList<IndexedSegment> _segments = Array.Empty<IndexedSegment>();
    private IReadOnlyList<SpeculationSuggestion> _currentSuggestions = Array.Empty<SpeculationSuggestion>();
    private SpeculationEngine? _engine;
    private string[] _currentCorpusPaths = Array.Empty<string>();
    private string _currentSuggestionText = string.Empty;
    private int _currentSuggestionCaret = -1;
    private int _currentSuggestionStart = -1;
    private string _baseStatusText = "Load a corpus to begin drafting.";
    private bool _isApplyingSuggestion;

    public MainForm()
    {
        InitializeComponent();
        WireEvents();
        LoadHistory();
        UpdateCorpusPresentation();
        ClearSuggestion();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Text Speculator";
        Width = 1180;
        Height = 760;
        MinimumSize = new Size(1080, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppBackground;
        ForeColor = PrimaryText;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular);

        ConfigurePrimaryButton();
        ConfigureSecondaryButton();
        ConfigureStatusStrip();
        ConfigureCorpusBadge();
        ConfigureMetricLabels();
        ConfigureWorkspaceMeta();
        ConfigureCompletionLabels();
        ConfigureHistoryList();
        ConfigureDocumentList();
        ConfigureTextInput();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildWorkspace(), 0, 1);
        root.Controls.Add(_statusStrip, 0, 2);

        Controls.Add(root);

        ResumeLayout(performLayout: true);
    }

    private void ConfigurePrimaryButton()
    {
        _btnLoad.Text = "Load Corpus";
        _btnLoad.AutoSize = true;
        _btnLoad.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnLoad.Padding = new Padding(18, 10, 18, 10);
        _btnLoad.Margin = new Padding(12, 0, 0, 0);
        _btnLoad.FlatStyle = FlatStyle.Flat;
        _btnLoad.BackColor = AccentColor;
        _btnLoad.ForeColor = Color.White;
        _btnLoad.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _btnLoad.Cursor = Cursors.Hand;
        _btnLoad.FlatAppearance.BorderSize = 0;
        _btnLoad.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
        _btnLoad.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
    }

    private void ConfigureSecondaryButton()
    {
        _btnActivateHistory.Text = "Open";
        _btnActivateHistory.AutoSize = true;
        _btnActivateHistory.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _btnActivateHistory.Padding = new Padding(12, 7, 12, 7);
        _btnActivateHistory.Margin = Padding.Empty;
        _btnActivateHistory.FlatStyle = FlatStyle.Flat;
        _btnActivateHistory.BackColor = SurfaceBackground;
        _btnActivateHistory.ForeColor = PrimaryText;
        _btnActivateHistory.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _btnActivateHistory.Cursor = Cursors.Hand;
        _btnActivateHistory.Enabled = false;
        _btnActivateHistory.FlatAppearance.BorderColor = BorderColor;
        _btnActivateHistory.FlatAppearance.BorderSize = 1;
        _btnActivateHistory.FlatAppearance.MouseOverBackColor = SurfaceAltBackground;
        _btnActivateHistory.FlatAppearance.MouseDownBackColor = AccentSoft;
    }

    private void ConfigureStatusStrip()
    {
        _statusStrip.Dock = DockStyle.Fill;
        _statusStrip.SizingGrip = false;
        _statusStrip.BackColor = HeaderBackground;
        _statusStrip.ForeColor = SecondaryText;
        _statusStrip.Padding = new Padding(16, 4, 16, 4);
        _statusStrip.Items.Add(_statusLabel);

        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = SecondaryText;
    }

    private void ConfigureCorpusBadge()
    {
        _lblCorpusBadge.AutoSize = true;
        _lblCorpusBadge.Margin = new Padding(0, 2, 0, 0);
        _lblCorpusBadge.Padding = new Padding(12, 8, 12, 8);
        _lblCorpusBadge.BackColor = AccentSoft;
        _lblCorpusBadge.ForeColor = AccentColor;
        _lblCorpusBadge.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
    }

    private void ConfigureMetricLabels()
    {
        ConfigureMetricValue(_lblDocsValue);
        ConfigureMetricValue(_lblSegmentsValue);
        ConfigureMetricValue(_lblCandidatesValue);
    }

    private static void ConfigureMetricValue(Label label)
    {
        label.AutoSize = true;
        label.ForeColor = PrimaryText;
        label.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
    }

    private void ConfigureWorkspaceMeta()
    {
        _lblWorkspaceMeta.AutoSize = true;
        _lblWorkspaceMeta.Dock = DockStyle.Right;
        _lblWorkspaceMeta.TextAlign = ContentAlignment.MiddleRight;
        _lblWorkspaceMeta.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _lblWorkspaceMeta.ForeColor = SecondaryText;
    }

    private void ConfigureCompletionLabels()
    {
        _lblCompletionTitle.Dock = DockStyle.Top;
        _lblCompletionTitle.AutoSize = false;
        _lblCompletionTitle.Height = 52;
        _lblCompletionTitle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _lblCompletionTitle.ForeColor = PrimaryText;

        _lblCompletionDetail.Dock = DockStyle.Fill;
        _lblCompletionDetail.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _lblCompletionDetail.ForeColor = SecondaryText;
    }

    private void ConfigureHistoryList()
    {
        _colHistoryCorpus.Text = "Recent Corpus";
        _colHistoryUpdated.Text = "Updated";

        _lstHistory.Columns.AddRange(new[] { _colHistoryCorpus, _colHistoryUpdated });
        _lstHistory.Dock = DockStyle.Fill;
        _lstHistory.View = View.Details;
        _lstHistory.FullRowSelect = true;
        _lstHistory.MultiSelect = false;
        _lstHistory.HideSelection = false;
        _lstHistory.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _lstHistory.BorderStyle = BorderStyle.None;
        _lstHistory.BackColor = SurfaceBackground;
        _lstHistory.ForeColor = PrimaryText;
        _lstHistory.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _lstHistory.ShowItemToolTips = true;
    }

    private void ConfigureDocumentList()
    {
        _colDocument.Text = "Document";
        _colType.Text = "Type";
        _colWords.Text = "Words";

        _lstDocuments.Columns.AddRange(new[] { _colDocument, _colType, _colWords });
        _lstDocuments.Dock = DockStyle.Fill;
        _lstDocuments.View = View.Details;
        _lstDocuments.FullRowSelect = true;
        _lstDocuments.MultiSelect = false;
        _lstDocuments.HideSelection = false;
        _lstDocuments.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _lstDocuments.BorderStyle = BorderStyle.None;
        _lstDocuments.BackColor = SurfaceBackground;
        _lstDocuments.ForeColor = PrimaryText;
        _lstDocuments.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
    }

    private void ConfigureTextInput()
    {
        _txtInput.Multiline = true;
        _txtInput.AcceptsReturn = true;
        _txtInput.AcceptsTab = false;
        _txtInput.ScrollBars = ScrollBars.Vertical;
        _txtInput.Dock = DockStyle.Fill;
        _txtInput.BackColor = SurfaceBackground;
        _txtInput.ForeColor = PrimaryText;
        _txtInput.BorderStyle = BorderStyle.None;
        _txtInput.Font = new Font("Segoe UI", 12f, FontStyle.Regular);
        _txtInput.HideSelection = false;
        _txtInput.Margin = Padding.Empty;
        _txtInput.GhostColor = Color.FromArgb(120, 148, 163, 184);
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = HeaderBackground,
            Height = 108,
            Padding = new Padding(24, 20, 24, 18),
            Margin = Padding.Empty
        };

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblTitle = new Label
        {
            AutoSize = true,
            Text = "Text Speculator",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = PrimaryText,
            Margin = Padding.Empty
        };

        var lblSubtitle = new Label
        {
            AutoSize = true,
            Text = "Corpus-guided drafting workspace",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = SecondaryText,
            Margin = new Padding(0, 4, 0, 0)
        };

        titlePanel.Controls.Add(lblTitle, 0, 0);
        titlePanel.Controls.Add(lblSubtitle, 0, 1);

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        actionsPanel.Controls.Add(_lblCorpusBadge);
        actionsPanel.Controls.Add(_btnLoad);

        var border = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = BorderColor
        };

        header.Controls.Add(actionsPanel);
        header.Controls.Add(titlePanel);
        header.Controls.Add(border);
        return header;
    }

    private Control BuildWorkspace()
    {
        _workspaceSplit.Dock = DockStyle.Fill;
        _workspaceSplit.BorderStyle = BorderStyle.None;
        _workspaceSplit.BackColor = AppBackground;
        _workspaceSplit.FixedPanel = FixedPanel.Panel1;
        _workspaceSplit.SplitterWidth = 10;
        _workspaceSplit.Margin = Padding.Empty;
        _workspaceSplit.Padding = new Padding(0, 0, 0, 12);

        _workspaceSplit.Panel1.BackColor = AppBackground;
        _workspaceSplit.Panel2.BackColor = AppBackground;
        _workspaceSplit.Panel1.Padding = new Padding(24, 20, 12, 0);
        _workspaceSplit.Panel2.Padding = new Padding(0, 20, 24, 0);
        _workspaceSplit.Panel1.Controls.Add(BuildSidebar());
        _workspaceSplit.Panel2.Controls.Add(BuildEditor());

        return _workspaceSplit;
    }

    private Control BuildSidebar()
    {
        var sidebarShell = CreateSurfacePanel(new Padding(18), SurfaceAltBackground);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, HistoryListHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var introPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        var lblSidebarTitle = new Label
        {
            AutoSize = true,
            Text = "Corpus Library",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = PrimaryText,
            Margin = Padding.Empty,
            Location = new Point(0, 0)
        };

        var lblSidebarSubtitle = new Label
        {
            AutoSize = true,
            Text = "Active reference documents and recent sets",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = SecondaryText,
            Margin = Padding.Empty,
            Location = new Point(0, 28)
        };

        introPanel.Controls.Add(lblSidebarTitle);
        introPanel.Controls.Add(lblSidebarSubtitle);

        var metricsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 18),
            Padding = Padding.Empty
        };
        metricsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        metricsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        metricsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        metricsLayout.Controls.Add(CreateMetricCard("Documents", "Loaded into the current corpus", _lblDocsValue), 0, 0);
        metricsLayout.Controls.Add(CreateMetricCard("Segments", "Indexed for speculation", _lblSegmentsValue), 0, 1);
        metricsLayout.Controls.Add(CreateMetricCard("Candidates", "Available for the active caret", _lblCandidatesValue), 0, 2);

        var historyHeader = CreateSectionHeader("Recent Corpora", _btnActivateHistory, new Padding(0, 0, 0, 10));
        var historyShell = CreateSurfacePanel(new Padding(1), SurfaceBackground);
        ((Panel)historyShell.Controls[0]).Controls.Add(_lstHistory);

        var documentsHeader = CreateSectionHeader("Documents", null, new Padding(0, 14, 0, 10));
        var documentsShell = CreateSurfacePanel(new Padding(1), SurfaceBackground);
        ((Panel)documentsShell.Controls[0]).Controls.Add(_lstDocuments);

        _lblCorpusDetail.AutoSize = true;
        _lblCorpusDetail.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _lblCorpusDetail.ForeColor = SecondaryText;
        _lblCorpusDetail.Margin = new Padding(0, 12, 0, 0);

        layout.Controls.Add(introPanel, 0, 0);
        layout.Controls.Add(metricsLayout, 0, 1);
        layout.Controls.Add(historyHeader, 0, 2);
        layout.Controls.Add(historyShell, 0, 3);
        layout.Controls.Add(documentsHeader, 0, 4);
        layout.Controls.Add(documentsShell, 0, 5);
        layout.Controls.Add(_lblCorpusDetail, 0, 6);

        ((Panel)sidebarShell.Controls[0]).Controls.Add(layout);
        return sidebarShell;
    }

    private Control BuildEditor()
    {
        var editorLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        var lblEditorTitle = new Label
        {
            AutoSize = true,
            Text = "Draft",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = PrimaryText,
            Location = new Point(0, 0)
        };

        var lblEditorSubtitle = new Label
        {
            AutoSize = true,
            Text = "Write with inline corpus completions",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = SecondaryText,
            Location = new Point(0, 28)
        };

        headerPanel.Controls.Add(lblEditorTitle);
        headerPanel.Controls.Add(lblEditorSubtitle);
        headerPanel.Controls.Add(_lblWorkspaceMeta);

        var editorShell = CreateSurfacePanel(new Padding(18), SurfaceBackground);
        ((Panel)editorShell.Controls[0]).Controls.Add(_txtInput);

        var completionShell = CreateSurfacePanel(new Padding(16), SurfaceAltBackground);
        completionShell.Height = 112;
        completionShell.Margin = new Padding(0, 14, 0, 0);

        var lblCompletionHeader = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 20,
            Text = "Current Completion",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = SecondaryText
        };

        var completionInner = (Panel)completionShell.Controls[0];
        completionInner.Controls.Add(_lblCompletionDetail);
        completionInner.Controls.Add(_lblCompletionTitle);
        completionInner.Controls.Add(lblCompletionHeader);

        editorLayout.Controls.Add(headerPanel, 0, 0);
        editorLayout.Controls.Add(editorShell, 0, 1);
        editorLayout.Controls.Add(completionShell, 0, 2);

        return editorLayout;
    }

    private Control CreateSectionHeader(string title, Control? actionControl, Padding margin)
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = margin,
            Padding = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var label = new Label
        {
            AutoSize = true,
            Text = title,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = PrimaryText,
            Margin = new Padding(0, 6, 0, 0)
        };

        header.Controls.Add(label, 0, 0);
        if (actionControl is not null)
            header.Controls.Add(actionControl, 1, 0);

        return header;
    }

    private static Panel CreateSurfacePanel(Padding padding, Color backgroundColor)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BorderColor,
            Padding = new Padding(1),
            Margin = Padding.Empty
        };

        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = backgroundColor,
            Padding = padding,
            Margin = Padding.Empty
        };

        outer.Controls.Add(inner);
        return outer;
    }

    private static Panel CreateMetricCard(string title, string subtitle, Label valueLabel)
    {
        var card = CreateSurfacePanel(new Padding(14, 12, 14, 12), SurfaceBackground);
        card.Margin = new Padding(0, 0, 0, 10);
        card.Height = 88;

        var lblTitle = new Label
        {
            AutoSize = true,
            Text = title,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = SecondaryText,
            Location = new Point(0, 0)
        };

        var lblSubtitle = new Label
        {
            AutoSize = true,
            Text = subtitle,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = SecondaryText,
            Location = new Point(0, 52)
        };

        var inner = (Panel)card.Controls[0];
        valueLabel.Location = new Point(0, 20);

        inner.Controls.Add(lblTitle);
        inner.Controls.Add(valueLabel);
        inner.Controls.Add(lblSubtitle);

        return card;
    }

    private void WireEvents()
    {
        _btnLoad.Click += async (s, e) => await PromptLoadCorpusAsync();
        _btnActivateHistory.Click += async (s, e) => await ActivateSelectedHistoryAsync();
        _lstHistory.ItemActivate += async (s, e) => await ActivateSelectedHistoryAsync();
        _lstHistory.SelectedIndexChanged += (s, e) => UpdateHistorySelectionState();
        _lstHistory.SizeChanged += (s, e) => UpdateHistoryColumns();
        _txtInput.TextChanged += (s, e) => UpdateSuggestion();
        _txtInput.SizeChanged += (s, e) => UpdateSuggestion();
        _txtInput.KeyUp += (s, e) => UpdateSuggestion();
        _txtInput.MouseUp += (s, e) => UpdateSuggestion();
        _txtInput.MouseWheel += (s, e) => UpdateSuggestion();
        _txtInput.GotFocus += (s, e) => UpdateSuggestion();
        _txtInput.LostFocus += (s, e) => ClearSuggestion();
        _lstDocuments.SizeChanged += (s, e) => UpdateDocumentColumns();
        Shown += (s, e) => InitializeWorkspaceSplit();
        Move += (s, e) => UpdateSuggestion();
        Resize += (s, e) => UpdateSuggestion();
        Deactivate += (s, e) => ClearSuggestion();
    }

    private void LoadHistory()
    {
        _historyEntries = _historyStore.Load().ToList();
        UpdateHistoryPresentation();
    }

    private async Task PromptLoadCorpusAsync()
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Select corpus documents",
            Filter = "Supported documents|*.txt;*.docx;*.pdf|Text|*.txt|Word|*.docx|PDF|*.pdf"
        };

        if (dlg.ShowDialog() != DialogResult.OK)
            return;

        await LoadCorpusFromPathsAsync(dlg.FileNames, "Indexing corpus...");
    }

    private async Task ActivateSelectedHistoryAsync()
    {
        if (_lstHistory.SelectedItems.Count == 0)
            return;

        if (_lstHistory.SelectedItems[0].Tag is not CorpusHistoryEntry entry)
            return;

        var existingPaths = entry.Paths.Where(File.Exists).ToList();
        if (existingPaths.Count == 0)
        {
            _historyEntries.Remove(entry);
            SaveHistory();
            UpdateHistoryPresentation();
            MessageBox.Show(
                "The files for this history entry could not be found anymore.",
                "History Entry Missing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (existingPaths.Count != entry.Paths.Length)
        {
            MessageBox.Show(
                "Some files from this history entry were missing. The available documents will be opened.",
                "Partial History Entry",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        await LoadCorpusFromPathsAsync(existingPaths, "Opening recent corpus...");
    }

    private async Task LoadCorpusFromPathsAsync(IEnumerable<string> paths, string loadingStatus)
    {
        var normalizedPaths = CorpusHistoryStore.NormalizePaths(paths);
        if (normalizedPaths.Count == 0)
            return;

        SetBusyState(true, loadingStatus);

        try
        {
            var readers = new ITextDocumentReader[]
            {
                new TxtDocumentReader(),
                new DocxDocumentReader(),
                new PdfDocumentReader()
            };

            var loader = new CorpusLoader(readers);
            _documents = (await loader.LoadDocumentsParallelAsync(normalizedPaths)).ToList();

            var indexer = new CorpusIndexer();
            _segments = indexer.BuildIndexParallel(_documents);
            _engine = _segments.Count > 0 ? new SpeculationEngine(_segments) : null;
            _currentCorpusPaths = normalizedPaths.ToArray();

            _documentSummaries = _documents
                .Select(document => new DocumentSummary(
                    document.Name,
                    Path.GetExtension(document.Name).TrimStart('.').ToUpperInvariant(),
                    TextTokenizer.Tokenize(document.Content).Count(TextTokenizer.IsWord)))
                .OrderBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _historyEntries = _historyStore.Upsert(_historyEntries, normalizedPaths);
            SaveHistory();

            _baseStatusText = _engine is null
                ? "No readable content was indexed from the selected files."
                : $"Ready. {_documents.Count} documents indexed into {_segments.Count} segments.";

            UpdateHistoryPresentation();
            UpdateCorpusPresentation();
            ClearSuggestion(_baseStatusText);
            _txtInput.Focus();
            BeginInvoke(new Action(UpdateSuggestion));
        }
        catch (Exception ex)
        {
            _baseStatusText = "Corpus loading failed.";
            MessageBox.Show(ex.Message, "Corpus Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ClearSuggestion(_baseStatusText);
        }
        finally
        {
            SetBusyState(false, _baseStatusText);
        }
    }

    private void UpdateSuggestion()
    {
        if (_isApplyingSuggestion)
            return;

        if (_engine == null || !_txtInput.Focused || _txtInput.SelectionLength > 0)
        {
            ClearSuggestion();
            return;
        }

        var caretIndex = _txtInput.SelectionStart;
        var textBeforeCaret = _txtInput.Text[..caretIndex];
        if (string.IsNullOrWhiteSpace(textBeforeCaret))
        {
            ClearSuggestion();
            return;
        }

        var suggestions = _engine.Suggest(textBeforeCaret, topK: 3, maxWords: 8);
        if (suggestions.Count == 0)
        {
            ClearSuggestion();
            return;
        }

        var suggestionText = suggestions[0].Text;
        if (!TryBuildGhostDisplay(textBeforeCaret, suggestionText, out var ghostText, out var suggestionStart))
        {
            ClearSuggestion();
            return;
        }

        _currentSuggestions = suggestions;
        _currentSuggestionText = suggestionText;
        _currentSuggestionCaret = caretIndex;
        _currentSuggestionStart = suggestionStart;
        _txtInput.UpdateGhost(ghostText, caretIndex);

        _lblCandidatesValue.Text = suggestions.Count.ToString("N0");
        _lblCompletionTitle.Text = suggestionText;
        _lblCompletionDetail.Text = BuildCompletionDetail(suggestions);
        HighlightSourceDocument(suggestions[0].SourceDocument);
        SetStatus(BuildSuggestionStatus(ghostText, suggestionText, suggestions[0].SourceDocument));
    }

    private void AcceptSuggestion()
    {
        if (string.IsNullOrEmpty(_currentSuggestionText))
            return;

        var suggestionText = _currentSuggestionText;
        var suggestionStart = _currentSuggestionStart;
        var caretIndex = _txtInput.SelectionStart;
        if (caretIndex != _currentSuggestionCaret ||
            suggestionStart < 0 ||
            suggestionStart > caretIndex)
        {
            ClearSuggestion();
            return;
        }

        _isApplyingSuggestion = true;
        try
        {
            _txtInput.SelectionStart = suggestionStart;
            _txtInput.SelectionLength = caretIndex - suggestionStart;
            _txtInput.SelectedText = suggestionText;
            _txtInput.SelectionStart = suggestionStart + suggestionText.Length;
            _txtInput.SelectionLength = 0;
            _txtInput.ScrollToCaret();
            _txtInput.Focus();
        }
        finally
        {
            _isApplyingSuggestion = false;
        }

        ClearSuggestion("Suggestion accepted.");
        BeginInvoke(new Action(UpdateSuggestion));
    }

    private bool TryBuildGhostDisplay(
        string textBeforeCaret,
        string suggestionText,
        out string ghostText,
        out int suggestionStart)
    {
        suggestionStart = textBeforeCaret.Length;
        ghostText = suggestionText;

        if (string.IsNullOrWhiteSpace(suggestionText))
            return false;

        if (!TryGetTrailingFragment(textBeforeCaret, out var fragment, out suggestionStart))
            return true;

        var suggestionTokens = TextTokenizer.Tokenize(suggestionText);
        var firstWord = suggestionTokens.FirstOrDefault(TextTokenizer.IsWord);
        if (string.IsNullOrEmpty(firstWord))
            return false;

        if (!TextTokenizer.Normalize(firstWord)
            .StartsWith(TextTokenizer.Normalize(fragment), StringComparison.Ordinal))
        {
            return false;
        }

        ghostText = suggestionText.Length > fragment.Length
            ? suggestionText[fragment.Length..]
            : string.Empty;

        return !string.IsNullOrWhiteSpace(ghostText);
    }

    private static bool TryGetTrailingFragment(
        string textBeforeCaret,
        out string fragment,
        out int fragmentStart)
    {
        fragment = string.Empty;
        fragmentStart = textBeforeCaret.Length;

        if (string.IsNullOrEmpty(textBeforeCaret) || !char.IsLetterOrDigit(textBeforeCaret[^1]))
            return false;

        fragmentStart = textBeforeCaret.Length - 1;
        while (fragmentStart > 0)
        {
            var previous = textBeforeCaret[fragmentStart - 1];
            if (!char.IsLetterOrDigit(previous) && previous is not '\'' and not '\u2019')
                break;

            fragmentStart--;
        }

        fragment = textBeforeCaret[fragmentStart..];
        return !string.IsNullOrWhiteSpace(fragment);
    }

    private void UpdateHistoryPresentation()
    {
        PopulateHistoryList();
        HighlightActiveHistoryEntry();
        UpdateHistorySelectionState();
    }

    private void UpdateCorpusPresentation()
    {
        _lblDocsValue.Text = _documents.Count.ToString("N0");
        _lblSegmentsValue.Text = _segments.Count.ToString("N0");
        _lblCandidatesValue.Text = _currentSuggestions.Count.ToString("N0");

        _lblCorpusBadge.Text = _documents.Count == 0
            ? "No Corpus Loaded"
            : $"{_documents.Count} Active Documents";

        _lblCorpusDetail.Text = _documents.Count == 0
            ? _historyEntries.Count == 0
                ? "Load documents to create your first reference corpus."
                : $"{_historyEntries.Count} recent corpora are ready to reopen."
            : $"{string.Join(", ", _documentSummaries.Select(summary => summary.Type).Distinct(StringComparer.OrdinalIgnoreCase))} files indexed for inline completion. {_historyEntries.Count} recent corpora saved.";

        _lblWorkspaceMeta.Text = _documents.Count == 0
            ? "Waiting for corpus data"
            : $"{_segments.Count:N0} indexed segments ready";

        PopulateDocumentList();
        SetStatus(_baseStatusText);
    }

    private void PopulateHistoryList()
    {
        _lstHistory.BeginUpdate();
        _lstHistory.Items.Clear();

        foreach (var entry in _historyEntries)
        {
            var item = new ListViewItem(entry.DisplayName)
            {
                Tag = entry,
                ToolTipText = string.Join(Environment.NewLine, entry.Paths.Select(Path.GetFileName))
            };
            item.SubItems.Add(FormatHistoryTimestamp(entry.LastUsedUtc));
            _lstHistory.Items.Add(item);
        }

        _lstHistory.EndUpdate();
        UpdateHistoryColumns();
    }

    private void PopulateDocumentList()
    {
        _lstDocuments.BeginUpdate();
        _lstDocuments.Items.Clear();

        foreach (var summary in _documentSummaries)
        {
            var item = new ListViewItem(summary.Name)
            {
                Tag = summary.Name
            };
            item.SubItems.Add(summary.Type);
            item.SubItems.Add(summary.WordCount.ToString("N0"));
            _lstDocuments.Items.Add(item);
        }

        _lstDocuments.EndUpdate();
        UpdateDocumentColumns();
    }

    private void UpdateHistoryColumns()
    {
        if (_lstHistory.ClientSize.Width <= 0)
            return;

        var totalWidth = _lstHistory.ClientSize.Width;
        _colHistoryUpdated.Width = 88;
        _colHistoryCorpus.Width = Math.Max(150, totalWidth - _colHistoryUpdated.Width - 4);
    }

    private void UpdateDocumentColumns()
    {
        if (_lstDocuments.ClientSize.Width <= 0)
            return;

        var totalWidth = _lstDocuments.ClientSize.Width;
        _colType.Width = 62;
        _colWords.Width = 82;
        _colDocument.Width = Math.Max(160, totalWidth - _colType.Width - _colWords.Width - 4);
    }

    private void HighlightActiveHistoryEntry()
    {
        _lstHistory.BeginUpdate();
        _lstHistory.SelectedItems.Clear();

        if (_currentCorpusPaths.Length > 0)
        {
            foreach (ListViewItem item in _lstHistory.Items)
            {
                if (item.Tag is CorpusHistoryEntry entry &&
                    CorpusHistoryStore.PathsMatch(entry.Paths, _currentCorpusPaths))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        _lstHistory.EndUpdate();
    }

    private void HighlightSourceDocument(string? documentName)
    {
        _lstDocuments.BeginUpdate();
        _lstDocuments.SelectedItems.Clear();

        if (!string.IsNullOrWhiteSpace(documentName))
        {
            foreach (ListViewItem item in _lstDocuments.Items)
            {
                if (string.Equals(item.Tag as string, documentName, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        _lstDocuments.EndUpdate();
    }

    private string BuildCompletionDetail(IReadOnlyList<SpeculationSuggestion> suggestions)
    {
        if (suggestions.Count == 0)
            return "No live completion is currently available.";

        var source = suggestions[0].SourceDocument;
        var alternativeText = suggestions.Count > 1
            ? $"  {suggestions.Count - 1} alternate completions are also ready."
            : "  This is the strongest active match.";

        return $"Source: {source}.{alternativeText}";
    }

    private void ClearSuggestion(string? statusText = null)
    {
        _txtInput.ClearGhost();
        _currentSuggestions = Array.Empty<SpeculationSuggestion>();
        _currentSuggestionText = string.Empty;
        _currentSuggestionCaret = -1;
        _currentSuggestionStart = -1;
        _lblCandidatesValue.Text = "0";
        _lblCompletionTitle.Text = _documents.Count == 0
            ? "No active completion"
            : "Continue typing to surface the best corpus match.";
        _lblCompletionDetail.Text = _documents.Count == 0
            ? "Load one or more reference documents to enable speculation."
            : "Suggestions appear inline when the current caret context matches your active corpus.";
        HighlightSourceDocument(null);
        SetStatus(statusText ?? _baseStatusText);
    }

    private static string BuildSuggestionStatus(string ghostText, string suggestionText, string sourceDocument)
    {
        var inlineText = string.IsNullOrWhiteSpace(ghostText) ? suggestionText : ghostText;
        const int maxLength = 72;

        if (inlineText.Length > maxLength)
            inlineText = inlineText[..(maxLength - 3)] + "...";

        return $"Tab accepts \"{inlineText}\" from {sourceDocument}.";
    }

    private void SetStatus(string text)
    {
        _statusLabel.Text = text;
    }

    private void SetBusyState(bool isBusy, string statusText)
    {
        UseWaitCursor = isBusy;
        _btnLoad.Enabled = !isBusy;
        _lstHistory.Enabled = !isBusy;
        UpdateHistorySelectionState();
        SetStatus(statusText);
    }

    private void UpdateHistorySelectionState()
    {
        _btnActivateHistory.Enabled = _lstHistory.Enabled && _lstHistory.SelectedItems.Count > 0;
    }

    private void SaveHistory()
    {
        _historyStore.Save(_historyEntries);
    }

    private void InitializeWorkspaceSplit()
    {
        if (_workspaceSplit.Width <= 0)
            return;

        _workspaceSplit.Panel1MinSize = SidebarMinWidth;
        _workspaceSplit.Panel2MinSize = EditorMinWidth;

        var targetWidth = Math.Max(_workspaceSplit.Panel1MinSize, SidebarTargetWidth);
        var maxWidth = Math.Max(
            _workspaceSplit.Panel1MinSize,
            _workspaceSplit.Width - _workspaceSplit.Panel2MinSize - _workspaceSplit.SplitterWidth);

        _workspaceSplit.SplitterDistance = Math.Min(targetWidth, maxWidth);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Tab && _txtInput.Focused && HasActiveSuggestion())
        {
            AcceptSuggestion();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool HasActiveSuggestion()
    {
        return !string.IsNullOrEmpty(_currentSuggestionText) &&
               _currentSuggestionStart >= 0 &&
               _currentSuggestionCaret == _txtInput.SelectionStart &&
               _txtInput.SelectionLength == 0;
    }

    private static string FormatHistoryTimestamp(DateTimeOffset timestamp)
    {
        var localTimestamp = timestamp.ToLocalTime();
        var age = DateTimeOffset.Now - localTimestamp;

        if (age < TimeSpan.FromMinutes(1))
            return "Just now";

        if (age < TimeSpan.FromHours(1))
            return $"{Math.Max(1, (int)age.TotalMinutes)}m ago";

        if (age < TimeSpan.FromDays(1))
            return localTimestamp.ToString("h:mm tt");

        if (age < TimeSpan.FromDays(7))
            return localTimestamp.ToString("ddd");

        return localTimestamp.ToString("MMM d");
    }

    private sealed record DocumentSummary(string Name, string Type, int WordCount);
}
