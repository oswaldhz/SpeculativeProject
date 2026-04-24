using TextSpeculator.Core.Models;
using TextSpeculator.Core.Readers;
using TextSpeculator.Core.Services;

namespace TextSpeculator.App;

public class MainForm : Form
{
    private readonly Button btnLoad = new() { Text = "Cargar documentos" };
    private readonly TextBox txtInput = new() { Multiline = true, Dock = DockStyle.Fill };
    private readonly Label lblStatus = new() { Text = "Sin corpus cargado", Dock = DockStyle.Top };

    private List<CorpusDocument> _documents = new();
    private IReadOnlyList<IndexedSegment> _segments = Array.Empty<IndexedSegment>();
    private SpeculationEngine? _engine;

    // Autocomplete popup
    private readonly ToolStripDropDown _suggestionPopup = new();
    private readonly ListBox _suggestionListBox = new();

    public MainForm()
    {
        Text = "Text Speculator";
        Width = 700;
        Height = 500;

        // Layout – only the input and load button remain on the left
        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 500 };
        var rightPanel = new Panel { Dock = DockStyle.Fill };

        btnLoad.Dock = DockStyle.Top;
        btnLoad.Height = 40;
        btnLoad.Click += async (_, _) => await LoadCorpusAsync();

        txtInput.TextChanged += (_, _) => UpdateSuggestions();
        txtInput.KeyDown += TxtInput_KeyDown;

        leftPanel.Controls.Add(txtInput);
        leftPanel.Controls.Add(btnLoad);

        rightPanel.Controls.Add(lblStatus);   // Only status on the right now

        Controls.Add(rightPanel);
        Controls.Add(leftPanel);

        // Setup popup
        _suggestionListBox.Dock = DockStyle.Fill;
        _suggestionListBox.BorderStyle = BorderStyle.None;
        _suggestionListBox.MouseDoubleClick += (_, _) => AcceptSuggestion();
        _suggestionListBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                AcceptSuggestion();
                e.Handled = true;
            }
        };

        var host = new ToolStripControlHost(_suggestionListBox)
        {
            AutoSize = false,
            Size = new System.Drawing.Size(200, 100)
        };
        _suggestionPopup.Items.Add(host);
        _suggestionPopup.AutoSize = true;
    }

    private async Task LoadCorpusAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Supported files|*.txt;*.docx;*.pdf|Text|*.txt|Word|*.docx|PDF|*.pdf"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        btnLoad.Enabled = false;
        lblStatus.Text = "Procesando corpus...";

        try
        {
            var readers = new ITextDocumentReader[]
            {
                new TxtDocumentReader(),
                new DocxDocumentReader(),
                new PdfDocumentReader()
            };

            var loader = new CorpusLoader(readers);
            _documents = (await loader.LoadDocumentsParallelAsync(dialog.FileNames)).ToList();

            var indexer = new CorpusIndexer();
            _segments = indexer.BuildIndexParallel(_documents);

            _engine = new SpeculationEngine(_segments);

            lblStatus.Text = $"Corpus cargado: {_documents.Count} documentos, {_segments.Count} segmentos";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblStatus.Text = "Error al cargar corpus";
        }
        finally
        {
            btnLoad.Enabled = true;
        }
    }

    private void UpdateSuggestions()
    {
        if (_engine is null)
        {
            _suggestionPopup.Hide();
            return;
        }

        var suggestions = _engine.Suggest(txtInput.Text, topK: 3, maxWords: 4);

        if (suggestions.Count == 0)
        {
            _suggestionPopup.Hide();
            return;
        }

        _suggestionListBox.Items.Clear();
        foreach (var s in suggestions)
            _suggestionListBox.Items.Add(s.Text);

        // Position popup below the current caret
        var caretPos = txtInput.GetPositionFromCharIndex(txtInput.SelectionStart);
        var screenPoint = txtInput.PointToScreen(caretPos);
        screenPoint.Y += (int)txtInput.Font.GetHeight() + 2;
        _suggestionPopup.Show(screenPoint, ToolStripDropDownDirection.Default);

        // If the input changes but the popup is still visible, ensure it stays on top
        if (_suggestionPopup.Visible)
        {
            // The popup might need to be re-shown at the new position (it already did above)
        }
    }

    private void TxtInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && _suggestionPopup.Visible)
        {
            _suggestionPopup.Hide();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter && _suggestionPopup.Visible)
        {
            AcceptSuggestion();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    /// <summary>
    /// Replace the currently typed word fragment with the selected suggestion.
    /// </summary>
    private void AcceptSuggestion()
    {
        if (_suggestionListBox.SelectedItem is not string suggestion)
            return;

        var text = txtInput.Text;
        // Find the start of the last word fragment (the one being typed)
        int lastWordStart = text.Length - 1;
        while (lastWordStart >= 0 && char.IsLetterOrDigit(text[lastWordStart]))
            lastWordStart--;
        lastWordStart++; // first character of the fragment

        txtInput.Text = text.Remove(lastWordStart) + suggestion;
        txtInput.SelectionStart = txtInput.Text.Length;
        _suggestionPopup.Hide();
    }
}