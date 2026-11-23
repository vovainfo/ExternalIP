namespace ExternalIpWidget;

/// <summary>
/// Порядок обхода сервисов внешнего IP и GeoIP. Изменения применяются сразу, без кнопки «Сохранить».
/// </summary>
internal sealed class ServiceOrderForm : Form
{
    private readonly OrderList _publicIp;
    private readonly OrderList _geoIp;
    private readonly Action<IReadOnlyList<string>, IReadOnlyList<string>> _changed;
    private readonly ToolTip _toolTip = new();
    private readonly Font _titleFont;
    private bool _allowClose;

    public ServiceOrderForm(
        IReadOnlyList<string> publicIpDefaults,
        IReadOnlyList<string> geoIpDefaults,
        Action<IReadOnlyList<string>, IReadOnlyList<string>> changed)
    {
        _changed = changed;
        Text = "Порядок опроса";
        Font = CreateFont(9.75f, FontStyle.Regular);
        _titleFont = CreateFont(9.75f, FontStyle.Bold);
        BackColor = Color.FromArgb(244, 247, 251);
        ForeColor = Color.FromArgb(15, 23, 42);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        KeyPreview = true;

        _publicIp = new OrderList(this, publicIpDefaults);
        _geoIp = new OrderList(this, geoIpDefaults);

        var hint = new Label
        {
            Text = "Сервисы спрашиваются сверху вниз. Следующий спрашивается, только если предыдущий не ответил. Новый порядок действует сразу.",
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            Margin = new Padding(0, 0, 0, 4),
        };

        var reset = CreateButton("По умолчанию", new Size(140, 32));
        reset.Margin = new Padding(0, 0, 8, 0);
        reset.Click += (_, _) => ResetToDefaults();
        var close = CreateButton("Закрыть", new Size(100, 32));
        close.Margin = new Padding(0);
        close.Click += (_, _) => Hide();
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 14, 0, 0),
        };
        buttons.Controls.Add(reset);
        buttons.Controls.Add(close);

        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 14, 16, 14),
        };
        root.Controls.Add(hint);
        root.Controls.Add(_publicIp.Build("Внешний IP", "Сервисы, которые сообщают внешний адрес компьютера."));
        root.Controls.Add(_geoIp.Build("Местоположение (GeoIP)", "Справочники страны, города и провайдера внешнего адреса."));
        root.Controls.Add(buttons);

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(root);

        _toolTip.SetToolTip(reset, "Вернуть исходный порядок обоих списков.");
    }

    public void SetOrders(IReadOnlyList<string> publicIp, IReadOnlyList<string> geoIp)
    {
        _publicIp.SetItems(publicIp, keepSelection: false);
        _geoIp.SetItems(geoIp, keepSelection: false);
    }

    public void Present(bool topMost)
    {
        TopMost = topMost;
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

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitLists();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        FitLists();
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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Hide();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _titleFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void FitLists()
    {
        _publicIp.Fit();
        _geoIp.Fit();
    }

    private void ResetToDefaults()
    {
        var changed = _publicIp.Reset() | _geoIp.Reset();
        if (changed)
            Notify();
    }

    private void Notify()
    {
        _changed(_publicIp.Names(), _geoIp.Names());
    }

    private Button CreateButton(string text, Size size)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Font = Font,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = size,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 232, 240);
        return button;
    }

    private static Font CreateFont(float size, FontStyle style)
    {
        try
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
        }
    }

    /// <summary>Список одного вида сервисов с кнопками «выше» и «ниже».</summary>
    private sealed class OrderList
    {
        private const int MinimumListWidth = 180;

        private readonly ServiceOrderForm _owner;
        private readonly IReadOnlyList<string> _defaults;
        private readonly ListBox _list;
        private readonly Button _up;
        private readonly Button _down;

        public OrderList(ServiceOrderForm owner, IReadOnlyList<string> defaults)
        {
            _owner = owner;
            _defaults = defaults;
            // Высота задаётся точно по строкам в Fit: подрезка до целых строк здесь не нужна.
            _list = new ListBox
            {
                IntegralHeight = false,
                Margin = new Padding(0, 0, 8, 0),
            };
            _up = owner.CreateButton("▲", new Size(36, 30));
            _up.Margin = new Padding(0, 0, 0, 6);
            _down = owner.CreateButton("▼", new Size(36, 30));
            _down.Margin = new Padding(0);

            _list.SelectedIndexChanged += (_, _) => UpdateButtons();
            _list.KeyDown += (_, e) =>
            {
                if (!e.Control || e.KeyCode is not (Keys.Up or Keys.Down))
                    return;
                e.SuppressKeyPress = true;
                Move(e.KeyCode == Keys.Up ? -1 : 1);
            };
            _up.Click += (_, _) => Move(-1);
            _down.Click += (_, _) => Move(1);
            SetItems(defaults, keepSelection: false);
        }

        public Control Build(string title, string description)
        {
            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0),
            };
            buttons.Controls.Add(_up);
            buttons.Controls.Add(_down);

            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 6, 0, 0),
            };
            row.Controls.Add(_list);
            row.Controls.Add(buttons);

            var section = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 0),
            };
            section.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = _owner._titleFont,
                Margin = new Padding(0),
            });
            section.Controls.Add(new Label
            {
                Text = description,
                AutoSize = true,
                MaximumSize = new Size(360, 0),
                ForeColor = Color.FromArgb(100, 116, 139),
                Margin = new Padding(0, 2, 0, 0),
            });
            section.Controls.Add(row);

            _owner._toolTip.SetToolTip(_list, "Ctrl+↑ и Ctrl+↓ тоже переставляют выбранный сервис.");
            _owner._toolTip.SetToolTip(_up, "Спрашивать выбранный сервис раньше.");
            _owner._toolTip.SetToolTip(_down, "Спрашивать выбранный сервис позже.");
            return section;
        }

        public void SetItems(IEnumerable<string> names, bool keepSelection)
        {
            var selected = keepSelection ? _list.SelectedItem as string : null;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var name in names)
                _list.Items.Add(name);
            _list.EndUpdate();

            var index = selected is null ? -1 : _list.Items.IndexOf(selected);
            _list.SelectedIndex = index >= 0 ? index : Math.Min(0, _list.Items.Count - 1);
            UpdateButtons();
            if (_list.IsHandleCreated)
                Fit();
        }

        public List<string> Names()
        {
            return _list.Items.Cast<string>().ToList();
        }

        /// <summary>Подгоняет список под все строки, чтобы прокрутка не понадобилась.</summary>
        public void Fit()
        {
            var border = _list.Height - _list.ClientSize.Height;
            var rows = Math.Max(1, _list.Items.Count);
            var widest = _list.Items.Cast<string>()
                .Select(name => TextRenderer.MeasureText(name, _list.Font).Width)
                .DefaultIfEmpty(0)
                .Max();
            _list.Size = new Size(
                Math.Max(MinimumListWidth, widest + border + 16),
                _list.ItemHeight * rows + border);
        }

        public bool Reset()
        {
            if (Names().SequenceEqual(_defaults))
                return false;
            SetItems(_defaults, keepSelection: true);
            return true;
        }

        private void UpdateButtons()
        {
            var index = _list.SelectedIndex;
            _up.Enabled = index > 0;
            _down.Enabled = index >= 0 && index < _list.Items.Count - 1;
        }

        private void Move(int step)
        {
            var index = _list.SelectedIndex;
            var target = index + step;
            if (index < 0 || target < 0 || target >= _list.Items.Count)
                return;

            var item = _list.Items[index];
            _list.BeginUpdate();
            _list.Items.RemoveAt(index);
            _list.Items.Insert(target, item);
            _list.SelectedIndex = target;
            _list.EndUpdate();
            _list.Focus();
            _owner.Notify();
        }
    }
}
