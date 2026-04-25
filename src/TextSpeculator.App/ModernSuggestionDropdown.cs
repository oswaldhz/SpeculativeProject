using System.Drawing.Drawing2D;

namespace TextSpeculator.App;

/// <summary>
/// A modern, stylish dropdown for text suggestions.
/// Features rounded corners, smooth fade‑in, and arrow key navigation.
/// </summary>
public class ModernSuggestionDropdown : ToolStripDropDown
{
    private const int CornerRadius = 6;
    private const int ItemHeight = 32;
    private const int MaxVisibleItems = 4;

    private readonly ListBox _listBox = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        Font = new Font("Segoe UI", 11f),
        BackColor = Color.FromArgb(35, 35, 35),
        ForeColor = Color.FromArgb(230, 230, 230),
        ItemHeight = ItemHeight
    };

    private readonly System.Windows.Forms.Timer _fadeTimer;
    private double _opacity = 0.0;
    private bool _isShowing;

    /// <summary>Fires when a suggestion is accepted (Enter or double‑click).</summary>
    public event EventHandler<string>? SuggestionConfirmed;

    public ModernSuggestionDropdown()
    {
        _listBox.DrawItem += DrawItem;
        _listBox.MouseDoubleClick += (_, _) => ConfirmSelection();

        var host = new ToolStripControlHost(_listBox)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        Items.Add(host);

        Padding = new Padding(1);
        BackColor = Color.FromArgb(45, 45, 45);

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 fps
        _fadeTimer.Tick += (_, _) => StepFade();
    }

    public void Show(Control owner, Point screenLocation, List<string> items)
    {
        if (items.Count == 0) return;

        _listBox.Items.Clear();
        items.ForEach(i => _listBox.Items.Add(i));
        _listBox.SelectedIndex = 0;

        int desiredHeight = Math.Min(items.Count * ItemHeight, MaxVisibleItems * ItemHeight) + 4;
        var host = (ToolStripControlHost)Items[0];
        host.Size = new Size(240, desiredHeight);
        Size = new Size(242, desiredHeight + 2);

        // Start fade animation
        _opacity = 0.0;
        _isShowing = true;
        _fadeTimer.Start();

        base.Show(screenLocation, ToolStripDropDownDirection.BelowRight);
    }

    public void SelectNext() => MoveSelection(1);
    public void SelectPrevious() => MoveSelection(-1);

    public void ConfirmSelection()
    {
        if (_listBox.SelectedItem is string selected)
            SuggestionConfirmed?.Invoke(this, selected);
        else
            Hide();
    }

    private void MoveSelection(int delta)
    {
        if (_listBox.Items.Count == 0) return;
        int idx = _listBox.SelectedIndex + delta;
        if (idx < 0) idx = _listBox.Items.Count - 1;
        if (idx >= _listBox.Items.Count) idx = 0;
        _listBox.SelectedIndex = idx;
    }

    private void StepFade()
    {
        if (!_isShowing) return;
        _opacity += 0.15;
        if (_opacity >= 0.92)
        {
            _opacity = 0.92;
            _fadeTimer.Stop();
            _isShowing = false;
        }
        Invalidate();
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        var item = _listBox.Items[e.Index].ToString();

        // Background
        using var bg = new SolidBrush(selected
            ? Color.FromArgb(55, 55, 55)
            : Color.FromArgb(35, 35, 35));
        e.Graphics.FillRectangle(bg, e.Bounds);

        // Text
        var textRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y,
            e.Bounds.Width - 12, e.Bounds.Height);
        using var textBrush = new SolidBrush(selected
            ? Color.White
            : Color.FromArgb(210, 210, 210));
        e.Graphics.DrawString(item, _listBox.Font, textBrush, textRect,
            new StringFormat { LineAlignment = StringAlignment.Center });
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = GetRoundedPath(ClientRectangle, CornerRadius);
        using var brush = new SolidBrush(Color.FromArgb((int)(_opacity * 255), BackColor));
        e.Graphics.FillPath(brush, path);
        using var pen = new Pen(Color.FromArgb(70, 70, 70), 1);
        e.Graphics.DrawPath(pen, path);
    }

    private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
            _listBox.Dispose();
        }
        base.Dispose(disposing);
    }
}