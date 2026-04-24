using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace TextSpeculator.Core.Readers;

public sealed class DocxDocumentReader : ITextDocumentReader
{
    public bool CanRead(string extension) => extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

    public Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();

            using var document = WordprocessingDocument.Open(path, false);
            var body = document.MainDocumentPart?.Document.Body;

            if (body is null)
                return string.Empty;

            foreach (var text in body.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                    sb.Append(text.Text).Append(' ');
            }

            return sb.ToString().Trim();
        }, cancellationToken);
    }
}