using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TextSpeculator.App;

internal sealed class GhostTextBox : TextBox
{
    private const int WmPaint = 0x000F;

    private string _ghostText = string.Empty;
    private int _ghostCaretIndex = -1;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color GhostColor { get; set; } = Color.FromArgb(120, 190, 190, 190);

    public void UpdateGhost(string ghostText, int caretIndex)
    {
        _ghostText = ghostText ?? string.Empty;
        _ghostCaretIndex = caretIndex;
        Invalidate();
    }

    public void ClearGhost()
    {
        if (string.IsNullOrEmpty(_ghostText) && _ghostCaretIndex < 0)
            return;

        _ghostText = string.Empty;
        _ghostCaretIndex = -1;
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WmPaint)
            DrawGhostText();
    }

    private void DrawGhostText()
    {
        if (!ShouldDrawGhost())
            return;

        var caretPosition = GetCaretClientPosition();
        if (caretPosition.X < 0 || caretPosition.Y < 0)
            return;

        var drawBounds = new Rectangle(
            caretPosition.X + 1,
            caretPosition.Y,
            Math.Max(1, ClientSize.Width - caretPosition.X - 4),
            Math.Max(Font.Height * 2, ClientSize.Height - caretPosition.Y));

        using var graphics = CreateGraphics();

        // Draw after the native textbox paints so the suggestion sits inline
        // without covering the user's actual text or stealing focus.
        TextRenderer.DrawText(
            graphics,
            _ghostText,
            Font,
            drawBounds,
            GhostColor,
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.Left |
            TextFormatFlags.Top |
            TextFormatFlags.WordBreak);
    }

    private bool ShouldDrawGhost()
    {
        return Focused &&
               SelectionLength == 0 &&
               SelectionStart == _ghostCaretIndex &&
               !string.IsNullOrWhiteSpace(_ghostText);
    }

    private Point GetCaretClientPosition()
    {
        if (GetCaretPos(out var caretPosition))
            return caretPosition;

        return GetPositionFromCharIndex(SelectionStart);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCaretPos(out Point lpPoint);
}
