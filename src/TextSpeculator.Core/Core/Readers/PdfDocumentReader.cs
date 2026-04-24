using System.Text;
using UglyToad.PdfPig;

namespace TextSpeculator.Core.Readers;

public sealed class PdfDocumentReader : ITextDocumentReader
{
    public bool CanRead(string extension) => extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();

            using var pdf = PdfDocument.Open(path);
            foreach (var page in pdf.GetPages())
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine(text);
            }

            return sb.ToString();
        }, cancellationToken);
    }
}