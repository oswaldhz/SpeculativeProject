using TextSpeculator.Core.Models;
using TextSpeculator.Core.Readers;
using TextSpeculator.Core.Services;

namespace TextSpeculator.App;

public class MainForm : Form
{
    private readonly Button btnLoad = new() { Text = "Cargar documentos" };
    private readonly TextBox txtInput = new() { Multiline = true, Dock = DockStyle.Fill };
    private readonly TextBox txtPreview = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Label lblStatus = new() { Text = "Sin corpus cargado", Dock = DockStyle.Top };
    private readonly ListBox lstSuggestions = new() { Dock = DockStyle.Fill };

    private List<CorpusDocument> _documents = new();
    private IReadOnlyList<IndexedSegment> _segments = Array.Empty<IndexedSegment>();
    private SpeculationEngine? _engine;

    public MainForm()
    {
        Text = "Text Speculator";
        Width = 1200;
        Height = 700;

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 500 };
        var rightPanel = new Panel { Dock = DockStyle.Fill };

        btnLoad.Dock = DockStyle.Top;
        btnLoad.Height = 40;
        btnLoad.Click += async (_, _) => await LoadCorpusAsync();

        txtInput.TextChanged += (_, _) => UpdateSuggestions();

        leftPanel.Controls.Add(txtInput);
        leftPanel.Controls.Add(btnLoad);

        rightPanel.Controls.Add(lstSuggestions);
        rightPanel.Controls.Add(txtPreview);
        rightPanel.Controls.Add(lblStatus);

        Controls.Add(rightPanel);
        Controls.Add(leftPanel);
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
            txtPreview.Text = txtInput.Text;
            lstSuggestions.Items.Clear();
            return;
        }

        var suggestions = _engine.Suggest(txtInput.Text, topK: 3, maxWords: 4);

        lstSuggestions.Items.Clear();

        if (suggestions.Count == 0)
        {
            txtPreview.Text = txtInput.Text;
            return;
        }

        foreach (var s in suggestions)
            lstSuggestions.Items.Add(s.Text);

        txtPreview.Text = suggestions[0].Preview;
    }
}