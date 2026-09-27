namespace ExternalIpWidget;

internal sealed class RequestLogForm : Form
{
    private readonly TextBox _trace;
    private readonly Label _status;
    private bool _allowClose;

    public RequestLogForm()
    {
        Text = "Журнал запросов";
        Font = CreateFont();
        BackColor = Color.FromArgb(244, 247, 251);
        ForeColor = Color.FromArgb(15, 23, 42);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        ShowInTaskbar = true;
        ClientSize = new Size(640, 420);
        MinimumSize = new Size(420, 280);

        _trace = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            Font = CreateMonoFont(),
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.None,
            Text = "Журнал появится после обновления.",
        };
        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            Padding = new Padding(12, 4, 12, 0),
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "",
        };
        var copy = new Button
        {
            Text = "Копировать журнал",
            AutoSize = true,
            MinimumSize = new Size(160, 34),
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(12, 8, 0, 8),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
        };
        copy.FlatAppearance.BorderSize = 1;
        copy.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        copy.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        copy.Click += (_, _) => Copy();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            WrapContents = false,
            BackColor = Color.FromArgb(244, 247, 251),
        };
        buttons.Controls.Add(copy);

        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 12, 12, 0),
            BackColor = Color.FromArgb(244, 247, 251),
        };
        frame.Controls.Add(_trace);

        Controls.Add(frame);
        Controls.Add(_status);
        Controls.Add(buttons);
    }

    public void Clear()
    {
        _trace.Clear();
        _status.Text = "";
    }

    public void AppendLine(string line)
    {
        if (_trace.TextLength > 0)
            _trace.AppendText(Environment.NewLine);
        _trace.AppendText(line);
        _trace.SelectionStart = _trace.TextLength;
        _trace.ScrollToCaret();
    }

    public void Present()
    {
        if (!Visible)
            Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    public void ForceClose()
    {
        if (IsDisposed)
            return;
        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    private void Copy()
    {
        if (_trace.TextLength == 0)
            return;

        try
        {
            Clipboard.SetText(_trace.Text);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _status.Text = "Не удалось скопировать журнал.";
            _status.ForeColor = Color.FromArgb(185, 28, 28);
            return;
        }

        _status.Text = "Журнал скопирован.";
        _status.ForeColor = Color.FromArgb(100, 116, 139);
    }

    private static Font CreateFont()
    {
        try
        {
            return new Font("Segoe UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        }
    }

    private static Font CreateMonoFont()
    {
        try
        {
            return new Font("Consolas", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericMonospace, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        }
    }
}
