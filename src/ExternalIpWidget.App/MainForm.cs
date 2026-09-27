using ExternalIpWidget.Core;

namespace ExternalIpWidget;

public sealed class MainForm : Form
{
    private readonly HttpClient _http = PublicIpLookup.CreateHttpClient();
    private readonly PublicIpLookup _lookup;
    private readonly GeoIpLookup _geo;
    private readonly WidgetSettings _settings = SettingsStore.Load();
    private readonly Icon _icon = WidgetIcon.Create();
    private readonly NotifyIcon _tray = new();
    private readonly ToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 400 };
    private readonly Font _ipFontLarge;
    private readonly Font _ipFontCompact;
    private readonly Font _nicFont;
    private readonly Font _eyebrowFont;
    private readonly Font _geoFont;

    private readonly Label _ipLabel;
    private readonly Label _geoLabel;
    private readonly Label _sourceLabel;
    private readonly Label _nicValue;
    private readonly Label _explanation;
    private readonly Label _status;
    private readonly Button _refreshButton;
    private readonly Button _copyButton;
    private readonly CheckBox _alwaysOnTop;
    private readonly CheckBox _showOnTaskbar;
    private readonly CheckBox _startWithWindows;
    private readonly TaskbarIpBand _taskbarBand;
    private readonly NumericUpDown _interval;

    private CancellationTokenSource? _lookupCts;
    private int _generation;
    private bool _busy;
    private bool _loading = true;
    private bool _ready;
    private bool _hiding;
    private bool _exitRequested;
    private uint _showMessage;
    private string? _currentAddress;
    private string? _nicAddress;

    public MainForm()
    {
        _lookup = new PublicIpLookup(_http);
        _geo = new GeoIpLookup(_http);
        Font = CreateUiFont("Segoe UI", 9.75f, FontStyle.Regular);
        _ipFontLarge = CreateUiFont(Font.FontFamily.Name, 22f, FontStyle.Bold);
        _ipFontCompact = CreateUiFont(Font.FontFamily.Name, 13f, FontStyle.Bold);
        _nicFont = new Font(Font, FontStyle.Bold);
        _eyebrowFont = CreateUiFont(Font.FontFamily.Name, 8.5f, FontStyle.Bold);
        _geoFont = CreateUiFont(Font.FontFamily.Name, 11f, FontStyle.Regular);

        Text = "Внешний IP";
        BackColor = Color.FromArgb(244, 247, 251);
        ForeColor = Color.FromArgb(15, 23, 42);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = _icon;
        KeyPreview = true;

        _ipLabel = new Label
        {
            Text = "…",
            Font = _ipFontLarge,
            AutoSize = true,
            ForeColor = Color.FromArgb(15, 23, 42),
            BackColor = Color.White,
            Margin = new Padding(0, 2, 0, 0),
            Padding = new Padding(6, 2, 6, 2),
        };
        _geoLabel = new Label
        {
            Text = "Определение местоположения…",
            Font = _geoFont,
            AutoSize = true,
            MaximumSize = new Size(400, 0),
            ForeColor = Color.FromArgb(15, 23, 42),
            Margin = new Padding(0, 6, 0, 0),
        };
        _sourceLabel = new Label
        {
            Text = "Запрос к внешнему сервису…",
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(0, 4, 0, 0),
        };
        _nicValue = new Label
        {
            AutoSize = true,
            Font = _nicFont,
            ForeColor = Color.FromArgb(15, 23, 42),
            Margin = new Padding(0, 2, 0, 0),
        };
        _explanation = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(436, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            Margin = new Padding(0, 8, 0, 0),
            Text = "Внешний адрес запрашивается у стороннего сервиса и появится здесь.",
        };
        _status = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(0, 10, 0, 0),
            Text = "Ожидание ответа сервиса",
        };
        _refreshButton = CreateButton("Обновить", primary: true);
        _copyButton = CreateButton("Копировать", primary: false);
        _copyButton.Enabled = false;
        _alwaysOnTop = new CheckBox
        {
            Text = "Поверх всех окон",
            AutoSize = true,
            Margin = new Padding(0, 6, 16, 0),
        };
        _showOnTaskbar = new CheckBox
        {
            Text = "На панели задач",
            AutoSize = true,
            Margin = new Padding(0, 6, 16, 0),
        };
        _startWithWindows = new CheckBox
        {
            Text = "Запускать с Windows",
            AutoSize = true,
            Margin = new Padding(0, 6, 16, 0),
        };
        _interval = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 120,
            Width = 64,
            Margin = new Padding(0, 4, 6, 0),
            TextAlign = HorizontalAlignment.Center,
        };

        var card = new FlowLayoutPanel
        {
            BackColor = Color.White,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 14, 16, 14),
            Margin = new Padding(0, 0, 0, 4),
            MinimumSize = new Size(436, 0),
            MaximumSize = new Size(436, 0),
        };
        card.Controls.Add(Eyebrow());
        card.Controls.Add(_ipLabel);
        card.Controls.Add(_geoLabel);
        card.Controls.Add(_sourceLabel);
        card.Controls.Add(Caption("не с сетевого адаптера, а от внешнего сервиса"));

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            MaximumSize = new Size(436, 0),
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(_refreshButton);
        buttons.Controls.Add(_copyButton);
        var why = new LinkLabel
        {
            Text = "Почему адреса разные?",
            AutoSize = true,
            Margin = new Padding(8, 8, 0, 0),
            LinkColor = Color.FromArgb(37, 99, 235),
            ActiveLinkColor = Color.FromArgb(29, 78, 216),
        };
        why.LinkClicked += (_, _) => ShowExplanation();
        buttons.Controls.Add(why);

        var options = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            MaximumSize = new Size(436, 0),
            Margin = new Padding(0, 8, 0, 0),
        };
        options.Controls.Add(_alwaysOnTop);
        options.Controls.Add(_showOnTaskbar);
        options.Controls.Add(_startWithWindows);
        options.Controls.Add(new Label
        {
            Text = "Интервал, мин",
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
        });
        options.Controls.Add(_interval);

        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 14, 16, 14),
        };
        root.Controls.Add(card);
        root.Controls.Add(NicBlock());
        root.Controls.Add(_explanation);
        root.Controls.Add(buttons);
        root.Controls.Add(options);
        root.Controls.Add(_status);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(root);
        MinimumSize = new Size(420, 280);

        _toolTip.SetToolTip(_ipLabel, "Адрес, который видит внешний сервис. Это не адрес сетевой карты.");
        _toolTip.SetToolTip(_geoLabel, "Приблизительное местоположение внешнего IP по базе GeoIP. Это не координаты компьютера.");
        _toolTip.SetToolTip(_nicValue, "Локальный адрес интерфейса. За роутером сайты его не видят.");
        _toolTip.SetToolTip(_alwaysOnTop, "Держать окно виджета поверх остальных.");
        _toolTip.SetToolTip(_showOnTaskbar, "Показывать внешний IP на панели задач, слева от часов. Плашку можно перетащить.");
        _toolTip.SetToolTip(_startWithWindows, "Добавить ярлык в папку автозагрузки Windows.");
        _toolTip.SetToolTip(_interval, "Как часто снова спрашивать внешний сервис.");

        _taskbarBand = new TaskbarIpBand(
            ShowFromTray,
            BeginRefresh,
            BeginCopy,
            () => _showOnTaskbar.Checked = false,
            nudge =>
            {
                _settings.TaskbarNudge = nudge;
                ScheduleSave();
            },
            _settings.TaskbarNudge);
        ConfigureTray();
        BindActions();
        ApplySettings();
        UpdateNic();
        _loading = false;
        _ready = true;

        _refreshTimer.Tick += (_, _) =>
        {
            if (!_busy)
                BeginRefresh();
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            TrySave();
        };
        ApplyInterval();

        Shown += (_, _) =>
        {
            EnsureOnScreen();
            BeginRefresh();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SingleInstance.Mark(Handle);
        _showMessage = SingleInstance.RegisterShowMessage();
    }

    protected override void WndProc(ref Message m)
    {
        if (_showMessage != 0 && m.Msg == (int)_showMessage)
        {
            ShowFromTray();
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        RememberLocation();
        TrySave();
        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
            HideToTray();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5)
        {
            BeginRefresh();
            return true;
        }

        if (keyData == (Keys.Control | Keys.C) && ActiveControl is not TextBoxBase and not NumericUpDown)
        {
            BeginCopy();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lookupCts?.Cancel();
            _lookupCts?.Dispose();
            _refreshTimer.Dispose();
            _saveTimer.Dispose();
            _taskbarBand.Dispose();
            _tray.Visible = false;
            _tray.Icon = null;
            _tray.Dispose();
            Icon = null;
            _icon.Dispose();
            _http.Dispose();
            _toolTip.Dispose();
            _ipFontLarge.Dispose();
            _ipFontCompact.Dispose();
            _nicFont.Dispose();
            _eyebrowFont.Dispose();
            _geoFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private Control NicBlock()
    {
        var caption = Caption("Адрес сетевой карты");
        caption.Margin = new Padding(0, 14, 0, 0);
        var block = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
        };
        block.Controls.Add(caption);
        block.Controls.Add(_nicValue);
        return block;
    }

    private Label Eyebrow()
    {
        return new Label
        {
            Text = "ВНЕШНИЙ IP",
            AutoSize = true,
            Font = _eyebrowFont,
            ForeColor = Color.FromArgb(37, 99, 235),
        };
    }

    private static Font CreateUiFont(string family, float size, FontStyle style)
    {
        try
        {
            return new Font(family, size, style, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
        }
    }

    private static Label Caption(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 116, 139),
        };
    }

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(120, 34),
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        if (primary)
        {
            button.BackColor = Color.FromArgb(37, 99, 235);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Color.FromArgb(37, 99, 235);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
        }
        else
        {
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(15, 23, 42);
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        }

        return button;
    }

    private void ConfigureTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Показать", null, (_, _) => ShowFromTray());
        menu.Items.Add("Обновить", null, (_, _) => BeginRefresh());
        menu.Items.Add("Копировать IP", null, (_, _) => BeginCopy());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) =>
        {
            _exitRequested = true;
            Close();
        });

        _tray.Icon = _icon;
        _tray.Text = "Внешний IP: определение…";
        _tray.Visible = true;
        _tray.ContextMenuStrip = menu;
        _tray.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowFromTray();
        };
    }

    private void BindActions()
    {
        _refreshButton.Click += (_, _) => BeginRefresh();
        _copyButton.Click += (_, _) => BeginCopy();
        _alwaysOnTop.CheckedChanged += (_, _) =>
        {
            TopMost = _alwaysOnTop.Checked;
            if (_loading)
                return;
            _settings.AlwaysOnTop = _alwaysOnTop.Checked;
            ScheduleSave();
        };
        _showOnTaskbar.CheckedChanged += (_, _) =>
        {
            _taskbarBand.SetEnabled(_showOnTaskbar.Checked);
            if (_loading)
                return;
            _settings.ShowOnTaskbar = _showOnTaskbar.Checked;
            ScheduleSave();
        };
        _startWithWindows.CheckedChanged += (_, _) => ToggleStartup();
        _interval.ValueChanged += (_, _) =>
        {
            if (_loading)
                return;
            _settings.RefreshMinutes = (int)_interval.Value;
            ApplyInterval();
            ScheduleSave();
        };
        LocationChanged += (_, _) =>
        {
            if (!_ready || _hiding || WindowState != FormWindowState.Normal)
                return;
            RememberLocation();
            ScheduleSave();
        };
    }

    private void ApplySettings()
    {
        _alwaysOnTop.Checked = _settings.AlwaysOnTop;
        TopMost = _settings.AlwaysOnTop;
        _showOnTaskbar.Checked = _settings.ShowOnTaskbar;
        _interval.Value = Math.Clamp(_settings.RefreshMinutes, (int)_interval.Minimum, (int)_interval.Maximum);
        _startWithWindows.Checked = StartupShortcut.Exists();
        _settings.StartWithWindows = _startWithWindows.Checked;

        if (_settings.WindowX is int x && _settings.WindowY is int y)
        {
            StartPosition = FormStartPosition.Manual;
            var window = new PixelRect(x, y, Math.Max(Width, MinimumSize.Width), Math.Max(Height, MinimumSize.Height));
            var placed = WindowPlacement.MoveIntoView(window, WorkAreas());
            Location = new Point(placed.X, placed.Y);
        }
    }

    private void ApplyInterval()
    {
        _refreshTimer.Stop();
        _refreshTimer.Interval = _settings.RefreshMinutes * 60 * 1000;
        _refreshTimer.Start();
    }

    private async void BeginRefresh()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            if (IsDisposed || ex is OperationCanceledException)
                return;
            ShowStatus("Ошибка обновления: " + ex.Message, error: true);
        }
    }

    private async Task RefreshAsync()
    {
        _lookupCts?.Cancel();
        _lookupCts?.Dispose();
        _lookupCts = new CancellationTokenSource();
        var token = _lookupCts.Token;
        var generation = ++_generation;

        _busy = true;
        _refreshButton.Enabled = false;
        _refreshButton.Text = "Обновление…";
        if (_currentAddress is null)
            _sourceLabel.Text = "Запрос к внешнему сервису…";

        try
        {
            UpdateNic();
            var progress = new Progress<string>(name =>
            {
                if (generation == _generation && !IsDisposed)
                    _sourceLabel.Text = $"Запрос к {name}…";
            });
            var result = await _lookup.GetAsync(token, progress);
            if (generation != _generation || IsDisposed)
                return;
            ShowResult(result);
            await ShowGeoAsync(result, generation, token);
        }
        catch (OperationCanceledException) when (generation != _generation || token.IsCancellationRequested)
        {
        }
        catch (PublicIpLookupException ex) when (generation == _generation && !IsDisposed)
        {
            ShowLookupError(ex);
        }
        finally
        {
            if (generation == _generation && !IsDisposed)
            {
                _busy = false;
                _refreshButton.Enabled = true;
                _refreshButton.Text = "Обновить";
            }
        }
    }

    private void ShowResult(PublicIpResult result)
    {
        var addressChanged = !string.Equals(_currentAddress, result.Address, StringComparison.OrdinalIgnoreCase);
        _currentAddress = result.Address;
        _ipLabel.Text = result.Address;
        if (addressChanged)
            ApplyAddressHighlight(false);
        _ipLabel.Font = result.Address.Length > 15 ? _ipFontCompact : _ipFontLarge;
        _sourceLabel.Text = $"Источник: {result.ProviderName}";
        _toolTip.SetToolTip(_sourceLabel, result.ProviderUrl);
        _explanation.Text = AddressComparison.Describe(result.Address, _nicAddress);
        _explanation.ForeColor = Color.FromArgb(71, 85, 105);
        _copyButton.Enabled = true;
        _geoLabel.Text = "Определение местоположения…";
        _geoLabel.ForeColor = Color.FromArgb(100, 116, 139);
        Text = $"{result.Address} — Внешний IP";
        SetTrayText("Внешний IP: " + result.Address);
        _taskbarBand.SetAddress(result.Address);
        ShowStatus($"Обновлено в {result.RetrievedAt.LocalDateTime:HH:mm:ss}", error: false);
    }

    private async Task ShowGeoAsync(PublicIpResult result, int generation, CancellationToken token)
    {
        var progress = new Progress<string>(name =>
        {
            if (generation == _generation && !IsDisposed)
                _geoLabel.Text = $"Запрос к {name}…";
        });

        try
        {
            var geo = await _geo.LookupAsync(result.Address, token, progress);
            if (generation != _generation || IsDisposed)
                return;

            _geoLabel.Text = geo.FormatPlace();
            _geoLabel.ForeColor = Color.FromArgb(15, 23, 42);
            ApplyAddressHighlight(geo.BelongsToRussia());
            _taskbarBand.SetRussia(geo.BelongsToRussia());
            _toolTip.SetToolTip(_geoLabel, geo.FormatDetails());
            _sourceLabel.Text = $"Источник: {result.ProviderName} · GeoIP: {geo.ProviderName}";
            _toolTip.SetToolTip(_sourceLabel, result.ProviderUrl + Environment.NewLine + geo.ProviderUrl);
            SetTrayText($"Внешний IP: {result.Address} · {geo.FormatPlace()}");
        }
        catch (OperationCanceledException) when (generation != _generation || token.IsCancellationRequested)
        {
        }
        catch (GeoIpLookupException ex) when (generation == _generation && !IsDisposed)
        {
            _geoLabel.Text = "Местоположение не определено";
            _geoLabel.ForeColor = Color.FromArgb(100, 116, 139);
            var details = ex.Attempts.Count == 0 ? ex.Message : string.Join(Environment.NewLine, ex.Attempts);
            _toolTip.SetToolTip(_geoLabel, details);
        }
    }

    private void ShowLookupError(PublicIpLookupException ex)
    {
        var details = ex.Attempts.Count == 0 ? ex.Message : string.Join(Environment.NewLine, ex.Attempts);
        _toolTip.SetToolTip(_status, details);
        if (_currentAddress is null)
        {
            _ipLabel.Text = "нет данных";
            _ipLabel.Font = _ipFontCompact;
            _sourceLabel.Text = "Внешний сервис не ответил";
            _geoLabel.Text = "Местоположение не определено";
            _geoLabel.ForeColor = Color.FromArgb(100, 116, 139);
            _explanation.Text = ex.Message;
            SetTrayText("Внешний IP: нет данных");
            ApplyAddressHighlight(false);
            _taskbarBand.SetAddress(null);
            ShowStatus("Не удалось определить внешний IP. Проверьте интернет и нажмите «Обновить».", error: true);
            return;
        }

        ShowStatus("Не удалось обновить адрес. На экране последний успешный результат.", error: true);
    }

    private async void BeginCopy()
    {
        if (string.IsNullOrEmpty(_currentAddress))
            return;

        try
        {
            Clipboard.SetText(_currentAddress);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            ShowStatus("Не удалось скопировать адрес в буфер обмена.", error: true);
            return;
        }

        var caption = _copyButton.Text;
        _copyButton.Text = "Скопировано";
        try
        {
            await Task.Delay(1200);
        }
        finally
        {
            if (!IsDisposed)
                _copyButton.Text = caption;
        }
    }

    private void UpdateNic()
    {
        _nicAddress = LocalNicAddress.TryGetOutbound();
        _nicValue.Text = _nicAddress ?? "не определён";
    }

    private void ToggleStartup()
    {
        if (_loading)
            return;

        try
        {
            StartupShortcut.SetEnabled(_startWithWindows.Checked);
            _settings.StartWithWindows = _startWithWindows.Checked;
            ScheduleSave();
        }
        catch (Exception ex)
        {
            _loading = true;
            _startWithWindows.Checked = StartupShortcut.Exists();
            _loading = false;
            ShowStatus("Не удалось изменить автозапуск: " + ex.Message, error: true);
        }
    }

    private void ShowExplanation()
    {
        MessageBox.Show(this, NatExplanation.Body, NatExplanation.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void HideToTray()
    {
        if (_hiding || _exitRequested)
            return;

        _hiding = true;
        try
        {
            RememberLocation();
            Hide();
            ShowInTaskbar = false;
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            RestoreRememberedLocation();
            if (!_settings.TrayHintShown)
            {
                _tray.ShowBalloonTip(
                    4000,
                    "Внешний IP",
                    "Виджет свёрнут в трей. Чтобы закрыть его, выберите «Выход» в меню значка.",
                    ToolTipIcon.Info);
                _settings.TrayHintShown = true;
                TrySave();
            }
        }
        finally
        {
            _hiding = false;
        }
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        if (WindowState != FormWindowState.Normal)
            WindowState = FormWindowState.Normal;

        EnsureOnScreen();
        Show();
        EnsureOnScreen();

        var stayOnTop = _settings.AlwaysOnTop;
        TopMost = true;
        Activate();
        BringToFront();
        TopMost = stayOnTop;
    }

    private void ApplyAddressHighlight(bool russia)
    {
        if (russia)
        {
            _ipLabel.BackColor = Color.FromArgb(204, 32, 32);
            _ipLabel.ForeColor = Color.White;
            return;
        }

        _ipLabel.BackColor = Color.White;
        _ipLabel.ForeColor = Color.FromArgb(15, 23, 42);
    }

    private void ShowStatus(string text, bool error)
    {
        _status.Text = text;
        _status.ForeColor = error ? Color.FromArgb(185, 28, 28) : Color.FromArgb(100, 116, 139);
    }

    private void SetTrayText(string text)
    {
        _tray.Text = text.Length <= 63 ? text : text[..63];
    }

    private void RememberLocation()
    {
        if (!Visible || WindowState != FormWindowState.Normal)
            return;

        var window = CurrentRect();
        if (!WorkAreas().Any(area => WindowPlacement.HasUsefulOverlap(window, area)))
            return;

        _settings.WindowX = Location.X;
        _settings.WindowY = Location.Y;
    }

    private void RestoreRememberedLocation()
    {
        if (_settings.WindowX is not int x || _settings.WindowY is not int y)
            return;

        var window = new PixelRect(x, y, CurrentRect().Width, CurrentRect().Height);
        if (!WorkAreas().Any(area => WindowPlacement.HasUsefulOverlap(window, area)))
            return;

        Location = new Point(x, y);
    }

    private void EnsureOnScreen()
    {
        var placed = WindowPlacement.MoveIntoView(CurrentRect(), WorkAreas());
        if (placed.X == Location.X && placed.Y == Location.Y)
            return;

        StartPosition = FormStartPosition.Manual;
        Location = new Point(placed.X, placed.Y);
        if (!_ready || _hiding || !Visible || WindowState != FormWindowState.Normal)
            return;

        RememberLocation();
        ScheduleSave();
    }

    private PixelRect CurrentRect()
    {
        var width = Width > 0 ? Width : MinimumSize.Width;
        var height = Height > 0 ? Height : MinimumSize.Height;
        return new PixelRect(Location.X, Location.Y, width, height);
    }

    private static IReadOnlyList<PixelRect> WorkAreas()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
            return [];

        var current = Screen.FromPoint(Cursor.Position);
        return screens
            .OrderByDescending(screen => ReferenceEquals(screen, current))
            .Select(screen =>
            {
                var area = screen.WorkingArea;
                return new PixelRect(area.X, area.Y, area.Width, area.Height);
            })
            .ToArray();
    }

    private void ScheduleSave()
    {
        if (!_ready)
            return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void TrySave()
    {
        try
        {
            SettingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus("Не удалось сохранить настройки.", error: true);
        }
    }

}
