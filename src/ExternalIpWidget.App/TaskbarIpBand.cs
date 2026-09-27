using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ExternalIpWidget.Core;

namespace ExternalIpWidget;

/// <summary>
/// Узкая плашка на панели задач с внешним IP. Щелчок открывает окно, перетаскивание сдвигает плашку вдоль панели.
/// </summary>
internal sealed class TaskbarIpBand : Form
{
    private const int SwpNoSize = 0x0001;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoActivate = 0x0010;
    private const int SwpShowWindow = 0x0040;
    private const int SwpHideWindow = 0x0080;

    private readonly Action _showMain;
    private readonly Action _refresh;
    private readonly Action _copy;
    private readonly Action _hideFromTaskbar;
    private readonly Action<int> _nudgeChanged;
    private readonly System.Windows.Forms.Timer _followTimer = new() { Interval = 150 };
    private readonly ContextMenuStrip _menu = new();

    private string _address = "…";
    private AddressLines _lines = AddressLines.From(null);
    private int _nudge;
    private int _fontForThickness = -1;
    private Font _textFont = new("Segoe UI", 22f, FontStyle.Bold, GraphicsUnit.Pixel);
    private const TextFormatFlags MeasureFlags =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
    private bool _enabled = true;
    private bool _shownOnBar;
    private bool _dragging;
    private bool _dragged;
    private Point _dragCursor;
    private int _dragNudge;
    private int _lastX = int.MinValue;
    private int _lastY = int.MinValue;
    private int _lastW = int.MinValue;
    private int _lastH = int.MinValue;
    private bool _lightTheme;
    private bool _placing;
    private uint _shellHookMessage;

    public TaskbarIpBand(
        Action showMain,
        Action refresh,
        Action copy,
        Action hideFromTaskbar,
        Action<int> nudgeChanged,
        int nudge)
    {
        _showMain = showMain;
        _refresh = refresh;
        _copy = copy;
        _hideFromTaskbar = hideFromTaskbar;
        _nudgeChanged = nudgeChanged;
        _nudge = nudge;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Location = new Point(-32000, -32000);
        Size = new Size(84, 60);
        TopMost = true;
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        Font = _textFont;
        ApplyTheme(force: true);

        _menu.Items.Add("Открыть", null, (_, _) => _showMain());
        _menu.Items.Add("Обновить", null, (_, _) => _refresh());
        _menu.Items.Add("Копировать IP", null, (_, _) => _copy());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Убрать с панели задач", null, (_, _) => _hideFromTaskbar());

        _followTimer.Tick += (_, _) => Place();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            const int wsExNoActivate = 0x08000000;
            const int wsExTopMost = 0x00000008;
            var parameters = base.CreateParams;
            parameters.ExStyle |= wsExToolWindow | wsExNoActivate | wsExTopMost;
            return parameters;
        }
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
        {
            _followTimer.Stop();
            if (IsHandleCreated)
                Hide();
            return;
        }

        if (!Visible)
            Show();
        _followTimer.Start();
        Place();
    }

    public void SetAddress(string? address)
    {
        var text = string.IsNullOrWhiteSpace(address) ? "…" : address;
        if (text == _address)
            return;
        _address = text;
        _lines = AddressLines.From(text);
        _lastW = int.MinValue;
        Place();
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _shellHookMessage = RegisterWindowMessage("SHELLHOOK");
        RegisterShellHookWindow(Handle);
        if (_enabled)
            Place();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DeregisterShellHookWindow(Handle);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (_shellHookMessage != 0 && m.Msg == (int)_shellHookMessage && _enabled && !_placing)
            Place();

        const int wmWindowPosChanging = 0x0046;
        if (m.Msg == wmWindowPosChanging && _enabled && _shownOnBar)
            KeepAboveTaskbar(m.LParam);

        base.WndProc(ref m);
    }

    private static void KeepAboveTaskbar(IntPtr lParam)
    {
        var position = Marshal.PtrToStructure<WindowPos>(lParam);
        if ((position.flags & 0x0080u) != 0)
            position.flags &= ~0x0080u;
        position.hwndInsertAfter = new IntPtr(-1);
        position.flags &= ~0x0004u;
        Marshal.StructureToPtr(position, lParam, false);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Place();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        _dragging = true;
        _dragged = false;
        _dragCursor = Cursor.Position;
        _dragNudge = _nudge;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
            return;

        var cursor = Cursor.Position;
        var dx = cursor.X - _dragCursor.X;
        var dy = cursor.Y - _dragCursor.Y;
        if (!_dragged && dx * dx + dy * dy < 16)
            return;

        _dragged = true;
        var along = Math.Abs(dx) >= Math.Abs(dy) ? dx : dy;
        _nudge = _dragNudge + along;
        Place();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right)
        {
            _menu.Show(Cursor.Position);
            return;
        }

        if (e.Button != MouseButtons.Left)
            return;

        var moved = _dragged;
        _dragging = false;
        _dragged = false;
        if (moved)
        {
            _nudgeChanged(_nudge);
            return;
        }

        _showMain();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Inflate(-1, -1);
        using var path = Rounded(bounds, 6);
        using var fill = new SolidBrush(BackColor);
        using var border = new Pen(_lightTheme ? Color.FromArgb(196, 196, 196) : Color.FromArgb(72, 72, 72));
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        DrawAddress(e.Graphics);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        using var path = Rounded(ClientRectangle, 8);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _followTimer.Dispose();
            _menu.Dispose();
            Font = SystemFonts.MessageBoxFont;
            _textFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Place()
    {
        if (!_enabled || !IsHandleCreated || _placing)
            return;

        _placing = true;
        try
        {
            PlaceCore();
        }
        finally
        {
            _placing = false;
        }
    }

    private void PlaceCore()
    {
        ApplyTheme(force: false);
        if (!TaskbarMetrics.TryGet(out var taskbar, out var tray, out var monitor))
        {
            HideBand();
            return;
        }

        var thickness = taskbar.Width >= taskbar.Height ? taskbar.Height : taskbar.Width;
        FitFont(thickness);
        var measured = MeasureLabel();
        if (!TaskbarBandLayout.TryGetBounds(taskbar, tray, monitor, measured.Width, measured.Height, _nudge, out var bounds))
        {
            HideBand();
            return;
        }

        var samePlace = _shownOnBar
            && bounds.X == _lastX
            && bounds.Y == _lastY
            && bounds.Width == _lastW
            && bounds.Height == _lastH;
        if (samePlace)
        {
            // Щелчок по панели задач поднимает её саму поверх плашки. Координаты те же,
            // поэтому окно нужно снова поставить выше, не меняя положение.
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
            return;
        }

        _shownOnBar = true;
        _lastX = bounds.X;
        _lastY = bounds.Y;
        _lastW = bounds.Width;
        _lastH = bounds.Height;
        SetWindowPos(Handle, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoActivate | SwpShowWindow);
    }

    private void HideBand()
    {
        if (!IsHandleCreated || !_shownOnBar)
            return;

        _shownOnBar = false;
        _lastX = int.MinValue;
        SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpHideWindow);
    }

    private void FitFont(int barThickness)
    {
        if (_fontForThickness == barThickness)
            return;

        var px = TaskbarLabelStyle.FontPx(barThickness, LineHeight);
        if (Math.Abs(_textFont.Size - px) > 0.5f || _textFont.Unit != GraphicsUnit.Pixel)
            ReplaceFont(px);
        _fontForThickness = barThickness;
    }

    private static int LineHeight(int fontPx)
    {
        using var font = CreateFont(fontPx);
        return TextRenderer.MeasureText(
            "255",
            font,
            new Size(int.MaxValue, int.MaxValue),
            MeasureFlags).Height;
    }

    private Size MeasureLabel()
    {
        var top = TextRenderer.MeasureText(_lines.Top, _textFont, new Size(int.MaxValue, int.MaxValue), MeasureFlags);
        if (!_lines.HasSecondLine)
            return new Size(top.Width + 14, top.Height + 8);

        var bottom = TextRenderer.MeasureText(_lines.Bottom, _textFont, new Size(int.MaxValue, int.MaxValue), MeasureFlags);
        return new Size(Math.Max(top.Width, bottom.Width) + 14, top.Height + bottom.Height + 6);
    }

    private void DrawAddress(Graphics graphics)
    {
        var flags = MeasureFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        if (!_lines.HasSecondLine)
        {
            TextRenderer.DrawText(graphics, _lines.Top, _textFont, ClientRectangle, ForeColor, flags);
            return;
        }

        var top = TextRenderer.MeasureText(_lines.Top, _textFont, new Size(int.MaxValue, int.MaxValue), MeasureFlags);
        var bottom = TextRenderer.MeasureText(_lines.Bottom, _textFont, new Size(int.MaxValue, int.MaxValue), MeasureFlags);
        var start = Math.Max(0, (ClientRectangle.Height - top.Height - bottom.Height) / 2);
        TextRenderer.DrawText(
            graphics,
            _lines.Top,
            _textFont,
            new Rectangle(0, start, ClientRectangle.Width, top.Height),
            ForeColor,
            flags);
        TextRenderer.DrawText(
            graphics,
            _lines.Bottom,
            _textFont,
            new Rectangle(0, start + top.Height, ClientRectangle.Width, bottom.Height),
            ForeColor,
            flags);
    }

    private void ReplaceFont(int fontPx)
    {
        var next = CreateFont(fontPx);
        var previous = _textFont;
        _textFont = next;
        Font = next;
        previous.Dispose();
    }

    private static Font CreateFont(int fontPx)
    {
        try
        {
            return new Font("Segoe UI", fontPx, FontStyle.Bold, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, fontPx, FontStyle.Bold, GraphicsUnit.Pixel);
        }
    }

    private void ApplyTheme(bool force)
    {
        var light = SystemUsesLightTheme();
        if (!force && light == _lightTheme)
            return;
        _lightTheme = light;
        BackColor = light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
        ForeColor = light ? Color.FromArgb(20, 20, 20) : Color.FromArgb(245, 245, 245);
        Invalidate();
    }

    private static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);
        if (bounds.Width < diameter || bounds.Height < diameter)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    private static extern bool RegisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool DeregisterShellHookWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }
}
