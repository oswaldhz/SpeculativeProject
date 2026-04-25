using System;
using System.Drawing;
using System.Windows.Forms;

namespace TextSpeculator.App;

/// <summary>
/// Ventana transparente que muestra una sugerencia flotante
/// justo después del cursor en el TextBox principal.
/// </summary>
internal class GhostOverlay : Form
{
    private string _ghostText = "";
    private Font _font = new Font("Segoe UI", 12f);

    public GhostOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Fuchsia;          // color que se vuelve transparente
        TransparencyKey = BackColor;
        DoubleBuffered = true;
        Size = new Size(1, 1);
        Enabled = false;                    // nunca recibe foco
    }

    /// <summary>
    /// Actualiza el texto, fuente y posición en pantalla de la sugerencia.
    /// </summary>
    public void UpdateGhost(string text, Font font, Point screenPosition)
    {
        _ghostText = text ?? "";
        _font = font;

        // Medir el ancho necesario
        using (var g = CreateGraphics())
        {
            var size = TextRenderer.MeasureText(g, _ghostText, _font,
                Size.Empty, TextFormatFlags.NoPadding);
            Size = new Size(size.Width + 4, size.Height + 2);
        }

        Location = screenPosition;
        Refresh();
    }

    /// <summary>
    /// Muestra la ventana si hay texto fantasma.
    /// </summary>
    public void ShowIfNeeded()
    {
        if (!string.IsNullOrEmpty(_ghostText) && !Visible)
            Show();
        else if (string.IsNullOrEmpty(_ghostText) && Visible)
            Hide();
    }

    /// <summary>
    /// Oculta la ventana.
    /// </summary>
    public void HideGhost()
    {
        if (Visible) Hide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (string.IsNullOrEmpty(_ghostText)) return;

        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        // Dibujamos el texto en verde fluorescente, sin fondo
        TextRenderer.DrawText(e.Graphics, _ghostText, _font,
            new Point(2, 0), Color.FromArgb(100, 255, 100), Color.Transparent,
            TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.Top);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _font?.Dispose();
        base.Dispose(disposing);
    }
}