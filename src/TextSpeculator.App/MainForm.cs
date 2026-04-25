using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TextSpeculator.Core.Models;
using TextSpeculator.Core.Readers;
using TextSpeculator.Core.Services;

namespace TextSpeculator.App;

public class MainForm : Form
{
    // Controles
    private Button _btnLoad;
    private TextBox _txtInput;
    private Label _lblStatus;
    private GhostOverlay _overlay;

    // Motor de especulación
    private List<CorpusDocument> _documents = new();
    private IReadOnlyList<IndexedSegment> _segments = Array.Empty<IndexedSegment>();
    private SpeculationEngine? _engine;

    // Texto fantasma actual (para cuando se pulse Tab)
    private string _currentGhostText = "";

    public MainForm()
    {
        InitializeComponent();
        WireEvents();
    }

    private void InitializeComponent()
    {
        // -------------------- Form --------------------
        Text = "Text Speculator";
        Width = 900;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        KeyPreview = true;   // Para capturar Tab incluso cuando el TextBox tiene el foco

        // -------------------- Botón cargar --------------------
        _btnLoad = new Button
        {
            Text = "Cargar corpus",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
            Width = 140,
            Height = 32,
            Location = new Point(12, 8),
            Cursor = Cursors.Hand
        };
        _btnLoad.FlatAppearance.BorderSize = 0;

        // -------------------- Etiqueta de estado --------------------
        _lblStatus = new Label
        {
            Text = "Sin corpus cargado",
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = Color.FromArgb(28, 28, 28),
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0)
        };

        // -------------------- Área de texto --------------------
        _txtInput = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.FromArgb(235, 235, 235),
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 12f, FontStyle.Regular),
            Padding = new Padding(16),
            Location = new Point(0, 48),
            Size = new Size(ClientSize.Width, ClientSize.Height - 48 - 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        // -------------------- Overlay fantasma --------------------
        _overlay = new GhostOverlay();

        // -------------------- Añadir controles --------------------
        Controls.Add(_txtInput);
        Controls.Add(_btnLoad);
        Controls.Add(_lblStatus);
    }

    private void WireEvents()
    {
        _btnLoad.Click += async (s, e) => await LoadCorpusAsync();
        _txtInput.TextChanged += (s, e) => UpdateSuggestion();
        _txtInput.SizeChanged += (s, e) => UpdateSuggestion();
        // Eventos del formulario para capturar Tab y reposicionar overlay
        KeyDown += Form_KeyDown;
        Move += (s, e) => UpdateSuggestion();
        Resize += (s, e) => UpdateSuggestion();
    }

    // ======================== Carga de corpus ========================
    private async Task LoadCorpusAsync()
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Documentos soportados|*.txt;*.docx;*.pdf|" +
                     "Texto|*.txt|Word|*.docx|PDF|*.pdf"
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        _btnLoad.Enabled = false;
        _lblStatus.Text = "Procesando corpus...";

        try
        {
            var readers = new ITextDocumentReader[]
            {
                new TxtDocumentReader(), new DocxDocumentReader(), new PdfDocumentReader()
            };

            var loader = new CorpusLoader(readers);
            _documents = (await loader.LoadDocumentsParallelAsync(dlg.FileNames)).ToList();

            var indexer = new CorpusIndexer();
            _segments = indexer.BuildIndexParallel(_documents);

            _engine = new SpeculationEngine(_segments);
            _lblStatus.Text = $"Corpus cargado: {_documents.Count} documentos, {_segments.Count} segmentos";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblStatus.Text = "Error al cargar el corpus";
        }
        finally
        {
            _btnLoad.Enabled = true;
        }
    }

    // ======================== Sugerencia fantasma ========================
    private void UpdateSuggestion()
    {
        if (_engine == null || string.IsNullOrWhiteSpace(_txtInput.Text))
        {
            _overlay.HideGhost();
            _currentGhostText = "";
            return;
        }

        var suggestions = _engine.Suggest(_txtInput.Text, topK: 1, maxWords: 6);
        if (suggestions.Count == 0)
        {
            _overlay.HideGhost();
            _lblStatus.Text = "Sin sugerencia";
            return;
        }

        _currentGhostText = suggestions[0].Text;

        // Calcular la posición en pantalla del cursor
        Point caretPos = _txtInput.GetPositionFromCharIndex(_txtInput.SelectionStart);
        Point screenPos = _txtInput.PointToScreen(caretPos);

        _overlay.UpdateGhost(_currentGhostText, _txtInput.Font, screenPos);
        _overlay.ShowIfNeeded();
        _lblStatus.Text = "Presiona Tab para aceptar la sugerencia";
    }

    // ======================== Manejo de Tab ========================
    private void Form_KeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.KeyCode == Keys.Tab || e.KeyCode == Keys.Enter) && _overlay.Visible)
        {
            AcceptSuggestion();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void AcceptSuggestion()
    {
        if (string.IsNullOrEmpty(_currentGhostText)) return;

        string text = _txtInput.Text;

        // Encontrar el inicio de la última palabra incompleta
        int lastWordStart = text.Length - 1;
        while (lastWordStart >= 0 && char.IsLetterOrDigit(text[lastWordStart]))
            lastWordStart--;
        lastWordStart++;

        // Reemplazar la palabra incompleta por la sugerencia completa
        _txtInput.Text = text.Remove(lastWordStart) + _currentGhostText;
        _txtInput.SelectionStart = _txtInput.Text.Length;

        // Ocultar overlay
        _overlay.HideGhost();
        _currentGhostText = "";
        _lblStatus.Text = "Sugerencia aceptada";
        _txtInput.Focus();
    }
}