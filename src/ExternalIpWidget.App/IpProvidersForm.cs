using ExternalIpWidget.Core;

namespace ExternalIpWidget;

internal sealed class IpProvidersForm : Form
{
    private readonly HttpClient _http;
    private readonly OptionalEnvironmentProxy _environmentProxy;
    private readonly ListView _list;
    private readonly TextBox _name;
    private readonly TextBox _url;
    private readonly Button _up;
    private readonly Button _down;
    private readonly Button _delete;
    private readonly Label _status;
    private static readonly Color RussiaColor = Color.FromArgb(204, 32, 32);
    private static readonly Color OtherColor = Color.FromArgb(21, 128, 61);
    private static readonly Color PendingColor = Color.FromArgb(100, 116, 139);

    private readonly GeoIpLookup _geo;
    private readonly Dictionary<string, Task<GeoIpInfo>> _geoByAddress = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _closed = new();
    private CancellationTokenSource? _fullProbe;
    private int _nextId;
    private int _fullEpoch;

    public IpProvidersForm(
        HttpClient http,
        IReadOnlyList<IpProvider> providers,
        OptionalEnvironmentProxy environmentProxy)
    {
        _http = http;
        _environmentProxy = environmentProxy;
        _geo = new GeoIpLookup(http);
        Providers = providers;

        Text = "Сервисы проверки IP";
        Font = CreateFont();
        BackColor = Color.FromArgb(244, 247, 251);
        ForeColor = Color.FromArgb(15, 23, 42);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        ClientSize = new Size(760, 480);
        MinimumSize = new Size(640, 420);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            Text = "Порядок строк — порядок проверок. Сервис должен ответить коротким текстом с IPv4. Список хранится в ip-providers.json рядом с программой.",
        };

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            ShowItemToolTips = true,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.None,
        };
        _list.Columns.Add("Имя", 140);
        _list.Columns.Add("Адрес", 340);
        _list.Columns.Add("Ответ", 220);
        _list.OwnerDraw = true;
        _list.DrawColumnHeader += (_, e) => e.DrawDefault = true;
        _list.DrawItem += (_, e) => e.DrawDefault = false;
        _list.DrawSubItem += DrawSubItem;
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();

        _name = new TextBox { Width = 140, Margin = new Padding(0, 6, 8, 0) };
        _url = new TextBox { Width = 360, Margin = new Padding(0, 6, 8, 0) };
        _url.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            AddProvider();
        };

        var add = CreateButton("Добавить", primary: false);
        add.Click += (_, _) => AddProvider();

        var addRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            WrapContents = false,
            Padding = new Padding(12, 6, 12, 0),
            BackColor = Color.FromArgb(244, 247, 251),
        };
        addRow.Controls.Add(FieldLabel("Имя"));
        addRow.Controls.Add(_name);
        addRow.Controls.Add(FieldLabel("Адрес"));
        addRow.Controls.Add(_url);
        addRow.Controls.Add(add);

        _up = CreateButton("Вверх", primary: false);
        _down = CreateButton("Вниз", primary: false);
        _delete = CreateButton("Удалить", primary: false);
        var again = CreateButton("Проверить снова", primary: false);
        var reset = CreateButton("Стандартный список", primary: false);
        var ok = CreateButton("ОК", primary: true);
        var cancel = CreateButton("Отмена", primary: false);
        _up.Click += (_, _) => MoveSelected(-1);
        _down.Click += (_, _) => MoveSelected(1);
        _delete.Click += (_, _) => DeleteSelected();
        again.Click += (_, _) => _ = ProbeAllAsync();
        reset.Click += (_, _) => ResetDefaults();
        ok.Click += (_, _) => Accept();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(12, 4, 12, 0),
            BackColor = Color.FromArgb(244, 247, 251),
        };
        buttons.Controls.Add(_up);
        buttons.Controls.Add(_down);
        buttons.Controls.Add(_delete);
        buttons.Controls.Add(again);
        buttons.Controls.Add(reset);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            Padding = new Padding(12, 2, 12, 0),
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "",
        };

        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 156,
            BackColor = Color.FromArgb(244, 247, 251),
        };
        bottom.Controls.Add(_status);
        bottom.Controls.Add(buttons);
        bottom.Controls.Add(addRow);

        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 0, 12, 0),
            BackColor = Color.FromArgb(244, 247, 251),
        };
        frame.Controls.Add(_list);

        Controls.Add(frame);
        Controls.Add(hint);
        Controls.Add(bottom);

        foreach (var provider in providers)
            AddRow(provider);
        if (_list.Items.Count == 0)
        {
            foreach (var provider in PublicIpLookup.DefaultProviders)
                AddRow(provider);
        }

        UpdateButtons();
        Shown += (_, _) => _ = ProbeAllAsync();
    }

    public IReadOnlyList<IpProvider> Providers { get; private set; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closed.Cancel();
        base.OnFormClosing(e);
    }

    private void Accept()
    {
        var providers = new List<IpProvider>();
        foreach (ListViewItem item in _list.Items)
        {
            var row = (ProviderRow)item.Tag!;
            providers.Add(new IpProvider(row.Name, row.Url));
        }

        if (providers.Count == 0)
        {
            ShowStatus("Нужен хотя бы один сервис.", error: true);
            return;
        }

        Providers = providers;
        DialogResult = DialogResult.OK;
    }

    private void AddProvider()
    {
        var url = _url.Text.Trim();
        var name = _name.Text.Trim();
        if (name.Length == 0 && Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            name = uri.Host;

        IpProvider provider;
        try
        {
            provider = new IpProvider(name, url);
        }
        catch (ArgumentException ex)
        {
            ShowStatus(ex.Message, error: true);
            return;
        }

        var row = AddRow(provider);
        _name.Clear();
        _url.Clear();
        ShowStatus("", error: false);
        UpdateButtons();
        _ = ProbeRowsAsync([row], full: false);
    }

    private void DeleteSelected()
    {
        if (_list.SelectedItems.Count == 0)
            return;
        if (_list.Items.Count <= 1)
        {
            ShowStatus("Нужен хотя бы один сервис.", error: true);
            return;
        }

        var index = _list.SelectedIndices[0];
        _list.Items.RemoveAt(index);
        var next = Math.Min(index, _list.Items.Count - 1);
        if (next >= 0)
        {
            _list.Items[next].Selected = true;
            _list.Items[next].Focused = true;
        }

        ShowStatus("", error: false);
        UpdateButtons();
    }

    private void MoveSelected(int delta)
    {
        if (_list.SelectedIndices.Count == 0)
            return;

        var index = _list.SelectedIndices[0];
        var next = index + delta;
        if (next < 0 || next >= _list.Items.Count)
            return;

        var item = _list.Items[index];
        _list.BeginUpdate();
        _list.Items.RemoveAt(index);
        _list.Items.Insert(next, item);
        _list.EndUpdate();
        item.Selected = true;
        item.Focused = true;
        item.EnsureVisible();
        UpdateButtons();
    }

    private void ResetDefaults()
    {
        _list.Items.Clear();
        foreach (var provider in PublicIpLookup.DefaultProviders)
            AddRow(provider);
        ShowStatus("", error: false);
        UpdateButtons();
        _ = ProbeAllAsync();
    }

    private ProviderRow AddRow(IpProvider provider)
    {
        var row = new ProviderRow
        {
            Id = ++_nextId,
            Name = provider.Name,
            Url = provider.Url,
        };
        var item = new ListViewItem(row.Name) { Tag = row, UseItemStyleForSubItems = false };
        item.SubItems.Add(row.Url);
        item.SubItems.Add("");
        _list.Items.Add(item);
        return row;
    }

    private Task ProbeAllAsync()
    {
        var rows = new List<ProviderRow>();
        foreach (ListViewItem item in _list.Items)
            rows.Add((ProviderRow)item.Tag!);
        return ProbeRowsAsync(rows, full: true);
    }

    private async Task ProbeRowsAsync(IReadOnlyList<ProviderRow> rows, bool full)
    {
        if (rows.Count == 0 || IsDisposed)
            return;

        var epoch = full ? ++_fullEpoch : _fullEpoch;
        foreach (var row in rows)
        {
            row.Epoch++;
            SetAnswer(row, "запрос…", AnswerTone.Pending);
        }

        var epochs = rows.Select(row => row.Epoch).ToArray();
        CancellationToken token;
        if (full)
        {
            _fullProbe?.Cancel();
            _fullProbe = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token);
            token = _fullProbe.Token;
        }
        else
        {
            token = _closed.Token;
        }

        var providers = rows.Select(row => new IpProvider(row.Name, row.Url)).ToArray();
        var lookup = new PublicIpLookup(_http, providers, environmentProxy: _environmentProxy);
        var progress = new Progress<IpProviderProbe>(item =>
        {
            if (IsDisposed || (full && epoch != _fullEpoch))
                return;
            var row = rows[item.Index];
            if (row.Epoch != epochs[item.Index] || FindItem(row.Id) is null)
                return;

            if (item.Address is null)
            {
                SetAnswer(row, item.Error ?? "нет ответа", AnswerTone.Other);
                return;
            }

            var address = item.Address;
            if (TryKnownTone(address, out var known))
            {
                SetAnswer(row, address, known);
                return;
            }

            SetAnswer(row, address, AnswerTone.Pending);
            var rowEpoch = epochs[item.Index];
            _ = ApplyCountryColorAsync(row, address, rowEpoch);
        });

        try
        {
            await lookup.ProbeAllAsync(token, progress).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex) when (!IsDisposed)
        {
            ShowStatus(ex.Message, error: true);
        }
    }

    private async Task ApplyCountryColorAsync(ProviderRow row, string address, int epoch)
    {
        AnswerTone tone;
        try
        {
            var geo = await GeoForAsync(address).ConfigureAwait(true);
            tone = geo.BelongsToRussia() ? AnswerTone.Russia : AnswerTone.Other;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            tone = AnswerTone.Other;
        }

        if (IsDisposed || row.Epoch != epoch)
            return;

        var item = FindItem(row.Id);
        if (item is null || item.SubItems[2].Text != address)
            return;

        SetAnswer(row, address, tone);
    }

    private bool TryKnownTone(string address, out AnswerTone tone)
    {
        Task<GeoIpInfo>? pending;
        lock (_geoByAddress)
            _geoByAddress.TryGetValue(address, out pending);

        if (pending is { IsCompletedSuccessfully: true })
        {
            tone = pending.Result.BelongsToRussia() ? AnswerTone.Russia : AnswerTone.Other;
            return true;
        }

        tone = AnswerTone.Pending;
        return false;
    }

    private Task<GeoIpInfo> GeoForAsync(string address)
    {
        lock (_geoByAddress)
        {
            if (_geoByAddress.TryGetValue(address, out var existing))
                return existing;

            var task = LookupGeoAsync(address);
            _geoByAddress[address] = task;
            return task;
        }
    }

    private async Task<GeoIpInfo> LookupGeoAsync(string address)
    {
        try
        {
            return await _geo.LookupAsync(address, _closed.Token).ConfigureAwait(false);
        }
        catch
        {
            lock (_geoByAddress)
                _geoByAddress.Remove(address);
            throw;
        }
    }

    private void SetAnswer(ProviderRow row, string text, AnswerTone tone)
    {
        if (IsDisposed)
            return;

        var item = FindItem(row.Id);
        if (item is null)
            return;

        row.Tone = tone;
        var answer = item.SubItems[2];
        answer.Text = text;
        item.ToolTipText = text;
        answer.ForeColor = ToneColor(tone);
        _list.Invalidate(item.Bounds);
    }

    private void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.ColumnIndex != 2)
        {
            e.DrawDefault = true;
            return;
        }

        // Выбранная строка без фокуса списка всё равно рисуется системным цветом текста.
        // Колонку «Ответ» рисуем сами и оставляем красный или зелёный.
        e.DrawBackground();
        var tone = e.Item?.Tag is ProviderRow row ? row.Tone : AnswerTone.Pending;
        var bounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 6), e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            e.SubItem?.Text ?? "",
            e.Item?.Font ?? _list.Font,
            bounds,
            ToneColor(tone),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private static Color ToneColor(AnswerTone tone)
    {
        return tone switch
        {
            AnswerTone.Russia => RussiaColor,
            AnswerTone.Other => OtherColor,
            _ => PendingColor,
        };
    }

    private ListViewItem? FindItem(int id)
    {
        foreach (ListViewItem item in _list.Items)
        {
            if (item.Tag is ProviderRow row && row.Id == id)
                return item;
        }

        return null;
    }

    private void UpdateButtons()
    {
        var index = _list.SelectedIndices.Count == 0 ? -1 : _list.SelectedIndices[0];
        _up.Enabled = index > 0;
        _down.Enabled = index >= 0 && index < _list.Items.Count - 1;
        _delete.Enabled = index >= 0 && _list.Items.Count > 1;
    }

    private void ShowStatus(string text, bool error)
    {
        _status.Text = text;
        _status.ForeColor = error
            ? Color.FromArgb(185, 28, 28)
            : Color.FromArgb(100, 116, 139);
    }

    private static Label FieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 10, 6, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
        };
    }

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(88, 32),
            Padding = new Padding(10, 2, 10, 2),
            Margin = new Padding(0, 4, 8, 0),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 1;
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

    private sealed class ProviderRow
    {
        public int Id { get; init; }

        public int Epoch { get; set; }

        public string Name { get; init; } = "";

        public string Url { get; init; } = "";

        public AnswerTone Tone { get; set; }
    }

    private enum AnswerTone
    {
        Pending,
        Russia,
        Other,
    }
}
