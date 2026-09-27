using ExternalIpWidget.Core;

namespace ExternalIpWidget;

public sealed class MainForm : Form
{
    private readonly OptionalEnvironmentProxy _environmentProxy;
    private readonly HttpClient _http;
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
    private readonly Font _eyebrowFont;
    private readonly Font _geoFont;

    private readonly Label _ipLabel;
    private readonly Label _geoLabel;
    private readonly Label _sourceLabel;
    private readonly Label _status;
    private readonly Label _versionLabel;
    private readonly Label _proxyReference;
    private readonly Button _refreshButton;
    private readonly Button _copyButton;
    private readonly Button _logButton;
    private readonly CheckBox _alwaysOnTop;
    private readonly CheckBox _showOnTaskbar;
    private readonly CheckBox _startWithWindows;
    private readonly ComboBox _proxyMode;
    private readonly TextBox _customProxy;
    private readonly FlowLayoutPanel _customProxyRow;
    private readonly Label _customProxyHint;
    private readonly RequestLogForm _log = new();
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
    private string? _appliedCustomProxy;

    public MainForm()
    {
        _settings.Normalize();
        _environmentProxy = new OptionalEnvironmentProxy
        {
            Mode = _settings.ProxyMode,
            CustomProxy = _settings.CustomProxy,
        };
        _http = PublicIpLookup.CreateHttpClient(_environmentProxy);
        _lookup = new PublicIpLookup(_http, environmentProxy: _environmentProxy);
        _geo = new GeoIpLookup(_http);
        Font = CreateUiFont("Segoe UI", 9.75f, FontStyle.Regular);
        _ipFontLarge = CreateUiFont(Font.FontFamily.Name, 22f, FontStyle.Bold);
        _ipFontCompact = CreateUiFont(Font.FontFamily.Name, 13f, FontStyle.Bold);
        _eyebrowFont = CreateUiFont(Font.FontFamily.Name, 8.5f, FontStyle.Bold);
        _geoFont = CreateUiFont(Font.FontFamily.Name, 11f, FontStyle.Regular);

        Text = WindowTitle();
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
            MaximumSize = new Size(400, 0),
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
        _status = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(0, 10, 0, 0),
            Text = "Ожидание ответа сервиса",
        };
        _versionLabel = new Label
        {
            Text = "Версия " + AppVersion(),
            AutoSize = true,
            ForeColor = Color.FromArgb(148, 163, 184),
            Margin = new Padding(0, 8, 0, 0),
        };
        _refreshButton = CreateButton("Обновить", "Обновление…", primary: true);
        _copyButton = CreateButton("Копировать", "Скопировано", primary: false);
        _copyButton.Enabled = false;
        _logButton = CreateButton("Журнал запросов", "Журнал запросов", primary: false);
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
        _proxyMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 320,
            Margin = new Padding(0, 4, 0, 0),
        };
        _proxyMode.Items.AddRange(["Без прокси", "Переменная окружения HTTP_PROXY", "Системный прокси", "Заданный прокси"]);
        _proxyReference = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(436, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            Margin = new Padding(0, 6, 0, 0),
            Visible = false,
        };
        _customProxy = new TextBox
        {
            Width = 280,
            Margin = new Padding(0, 4, 0, 0),
        };
        _customProxyRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Visible = false,
            Margin = new Padding(0, 6, 0, 0),
        };
        _customProxyRow.Controls.Add(new Label
        {
            Text = "Адрес",
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
        });
        _customProxyRow.Controls.Add(_customProxy);
        _customProxyHint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(436, 0),
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(0, 4, 0, 0),
            Visible = false,
            Text = "Формат: хост:порт или http://хост:порт. Например, 127.0.0.1:8080.",
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

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(_refreshButton);
        buttons.Controls.Add(_copyButton);
        buttons.Controls.Add(_logButton);

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

        var proxyBlock = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            MaximumSize = new Size(436, 0),
            Margin = new Padding(0, 8, 0, 0),
        };
        var proxyRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0),
        };
        proxyRow.Controls.Add(new Label
        {
            Text = "Прокси",
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
        });
        proxyRow.Controls.Add(_proxyMode);
        proxyBlock.Controls.Add(proxyRow);
        proxyBlock.Controls.Add(_proxyReference);
        proxyBlock.Controls.Add(_customProxyRow);
        proxyBlock.Controls.Add(_customProxyHint);

        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 14, 16, 14),
        };
        root.Controls.Add(card);
        root.Controls.Add(buttons);
        root.Controls.Add(options);
        root.Controls.Add(proxyBlock);
        root.Controls.Add(_status);
        root.Controls.Add(_versionLabel);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(root);
        MinimumSize = new Size(420, 220);

        _toolTip.SetToolTip(_ipLabel, "Адрес, который видит внешний сервис.");
        _toolTip.SetToolTip(_geoLabel, "Приблизительное местоположение внешнего IP по базе GeoIP. Это не координаты компьютера.");
        _toolTip.SetToolTip(_alwaysOnTop, "Держать окно виджета поверх остальных.");
        _toolTip.SetToolTip(_showOnTaskbar, "Показывать внешний IP на панели задач, слева от часов. Плашку можно перетащить.");
        _toolTip.SetToolTip(_startWithWindows, "Добавить ярлык в папку автозагрузки Windows.");
        _toolTip.SetToolTip(_proxyMode, "Как виджет подключается к сервисам при проверке адреса.");
        _toolTip.SetToolTip(_customProxy, "Хост и порт: 127.0.0.1:8080 или http://хост:порт.");
        _toolTip.SetToolTip(_interval, "Как часто снова спрашивать внешний сервис.");
        _toolTip.SetToolTip(_logButton, "Открыть журнал последнего запроса: прокси, DNS, подключение и ответ сервиса.");

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
        _log.ForceClose();
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
            _log.ForceClose();
            _log.Dispose();
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
            _eyebrowFont.Dispose();
            _geoFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private Label Eyebrow()
    {
        return new Label
        {
            Text = "EXTERNAL IP",
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

    private Button CreateButton(string text, string widestText, bool primary)
    {
        const int height = 36;
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Font = Font,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12, 0, 12, 0),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            UseCompatibleTextRendering = false,
        };
        button.FlatAppearance.BorderSize = 1;
        if (primary)
        {
            button.BackColor = Color.FromArgb(37, 99, 235);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Color.FromArgb(37, 99, 235);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(29, 78, 216);
        }
        else
        {
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(15, 23, 42);
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 232, 240);
        }

        var textWidth = TextRenderer.MeasureText(widestText, button.Font).Width;
        var width = Math.Max(120, textWidth + button.Padding.Horizontal + 8);
        button.Size = new Size(width, height);
        button.MinimumSize = new Size(width, height);
        button.MaximumSize = new Size(width, height);
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
        _tray.Text = "External IP: определение…";
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
        _logButton.Click += (_, _) => _log.Present();
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
        _proxyMode.SelectedIndexChanged += (_, _) =>
        {
            var mode = SelectedProxyMode();
            _environmentProxy.Mode = mode;
            UpdateProxyDetails();
            if (_loading)
                return;
            _settings.ProxyMode = mode;
            _settings.ProxyChoiceSaved = true;
            ScheduleSave();
            BeginRefresh();
        };
        _customProxy.TextChanged += (_, _) =>
        {
            _environmentProxy.CustomProxy = _customProxy.Text;
            if (_loading)
                return;
            _settings.CustomProxy = _customProxy.Text;
            ScheduleSave();
        };
        _customProxy.Leave += (_, _) => ApplyCustomProxy(refresh: true);
        _customProxy.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter || SelectedProxyMode() != ProxyMode.Custom)
                return;
            e.SuppressKeyPress = true;
            ApplyCustomProxy(refresh: true);
        };
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
        _customProxy.Text = _settings.CustomProxy;
        _environmentProxy.CustomProxy = _settings.CustomProxy;
        _appliedCustomProxy = _settings.CustomProxy.Trim();
        _environmentProxy.Mode = _settings.ProxyMode;
        _proxyMode.SelectedIndex = ProxyIndex(_settings.ProxyMode);
        UpdateProxyDetails();
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
            AppendTrace(ex.GetType().Name + ": " + ex.Message);
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
            _log.Clear();
            UpdateProxyDetails();
            if (SelectedProxyMode() == ProxyMode.Custom)
                _appliedCustomProxy = _customProxy.Text.Trim();
            var progress = new Progress<string>(name =>
            {
                if (generation == _generation && !IsDisposed)
                    _sourceLabel.Text = $"Запрос к {name}…";
            });
            var trace = new Progress<string>(line =>
            {
                if (generation == _generation && !IsDisposed)
                    AppendTrace(line);
            });
            var result = await _lookup.GetAsync(token, progress, trace);
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
        _copyButton.Enabled = true;
        _geoLabel.Text = "Определение местоположения…";
        _geoLabel.ForeColor = Color.FromArgb(100, 116, 139);
        Text = WindowTitle(result.Address);
        SetTrayText("External IP: " + result.Address);
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
            SetTrayText($"External IP: {result.Address} · {geo.FormatPlace()}");
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
        _currentAddress = null;
        _copyButton.Enabled = false;
        _ipLabel.Text = "Не удалось обновить адрес";
        _ipLabel.Font = _ipFontCompact;
        ApplyAddressHighlight(false);
        _sourceLabel.Text = ex.Attempts.Count == 0
            ? "Проверка не выполнена"
            : "Внешний сервис не ответил";
        _geoLabel.Text = "Местоположение не определено";
        _geoLabel.ForeColor = Color.FromArgb(100, 116, 139);
        Text = WindowTitle("Не удалось обновить адрес");
        SetTrayText("External IP: не удалось обновить адрес");
        _taskbarBand.SetRussia(false);
        _taskbarBand.SetAddress("Error");
        _toolTip.SetToolTip(_status, details);
        _toolTip.SetToolTip(_ipLabel, details);
        ShowStatus("Не удалось обновить адрес", error: true);
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
                    "External IP",
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

    private void AppendTrace(string line)
    {
        if (IsDisposed)
            return;
        _log.AppendLine(line);
    }

    private ProxyMode SelectedProxyMode()
    {
        return ProxyIndexMode(_proxyMode.SelectedIndex);
    }

    private static int ProxyIndex(ProxyMode mode)
    {
        return mode switch
        {
            ProxyMode.Environment => 1,
            ProxyMode.System => 2,
            ProxyMode.Custom => 3,
            _ => 0,
        };
    }

    private static ProxyMode ProxyIndexMode(int index)
    {
        return index switch
        {
            1 => ProxyMode.Environment,
            2 => ProxyMode.System,
            3 => ProxyMode.Custom,
            _ => ProxyMode.None,
        };
    }

    private void ApplyCustomProxy(bool refresh)
    {
        if (_loading || IsDisposed || SelectedProxyMode() != ProxyMode.Custom)
            return;

        var text = _customProxy.Text.Trim();
        _environmentProxy.CustomProxy = text;
        _settings.CustomProxy = text;
        ScheduleSave();
        if (!refresh || string.Equals(text, _appliedCustomProxy, StringComparison.Ordinal))
            return;

        _appliedCustomProxy = text;
        BeginRefresh();
    }

    private void UpdateProxyDetails()
    {
        var mode = SelectedProxyMode();
        var showReference = mode is ProxyMode.Environment or ProxyMode.System;
        var custom = mode == ProxyMode.Custom;
        _customProxyRow.Visible = custom;
        _customProxyHint.Visible = custom;
        _proxyReference.Visible = showReference;
        if (!showReference)
            return;

        _proxyReference.Text = _environmentProxy.ReferenceText(new Uri("https://api.ipify.org/"));
    }

    private static string AppVersion()
    {
        var informational = Application.ProductVersion;
        if (string.IsNullOrWhiteSpace(informational))
            return "";
        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }

    private static string WindowTitle(string? prefix = null)
    {
        var version = AppVersion();
        var name = string.IsNullOrEmpty(version) ? "External IP" : "External IP " + version;
        return string.IsNullOrEmpty(prefix) ? name : prefix + " — " + name;
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
