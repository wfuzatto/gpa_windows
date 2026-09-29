using System.ComponentModel;
using GpaWindows.Models;
using GpaWindows.Services;

namespace GpaWindows;

public sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(13, 17, 23);
    private static readonly Color Surface = Color.FromArgb(22, 27, 34);
    private static readonly Color Surface2 = Color.FromArgb(33, 38, 45);
    private static readonly Color TextPrimary = Color.FromArgb(235, 240, 246);
    private static readonly Color TextMuted = Color.FromArgb(145, 156, 170);

    private readonly CheckBox _enabled = NewCheckBox("Ativar fiscalização contínua");
    private readonly CheckBox _lockWallpaper = NewCheckBox("Impedir alteração do papel de parede");
    private readonly CheckBox _lockTheme = NewCheckBox("Impedir alteração do tema do Windows");
    private readonly CheckBox _blockAudio = NewCheckBox("Bloquear áudio do computador");

    private readonly CheckBox _applicationControl = NewCheckBox("Ativar controle de aplicativos");
    private readonly CheckBox _blockUnknownApps = NewCheckBox(
        "Modo estrito: bloquear executáveis desconhecidos/portáteis não autorizados");

    private readonly CheckBox _dnsAllowList = NewCheckBox(
        "Ativar DNS Allowlist (bloquear qualquer domínio não autorizado)");
    private readonly CheckBox _disableDoH = NewCheckBox(
        "Desativar DNS-over-HTTPS no Edge e Chrome");
    private readonly CheckBox _sanitizeHosts = NewCheckBox(
        "Proteger/limpar o arquivo hosts durante o modo estrito");

    private readonly BindingList<ManagedApplication> _appRows = [];

    private readonly DataGridView _appsGrid = new()
    {
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToOrderColumns = true,
        RowHeadersVisible = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        BackgroundColor = Color.FromArgb(20, 24, 31),
        GridColor = Color.FromArgb(48, 54, 61),
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
        ColumnHeadersHeight = 36,
        RowTemplate = { Height = 32 }
    };

    private readonly TextBox _domains = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10.5f),
        PlaceholderText = "exemplo.com\r\napi.exemplo.com\r\n..."
    };

    private readonly TextBox _upstreamDns = new()
    {
        Width = 180,
        Text = "1.1.1.1"
    };

    private readonly NumericUpDown _interval = new()
    {
        Minimum = 10,
        Maximum = 3600,
        Value = 30,
        Width = 100
    };

    private readonly Label _serviceStatus = NewValueLabel();
    private readonly Label _lastApplied = NewValueLabel();
    private readonly Label _configPath = NewValueLabel();
    private readonly Label _appCount = NewValueLabel();

    private readonly RichTextBox _log = new()
    {
        ReadOnly = true,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.FromArgb(20, 24, 31),
        ForeColor = Color.FromArgb(205, 214, 226),
        Font = new Font("Consolas", 9.5f),
        Dock = DockStyle.Fill
    };

    public MainForm()
    {
        Text = "GPA Windows — Diretivas Locais";
        Width = 1180;
        Height = 820;
        MinimumSize = new Size(920, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 10f);

        ConfigureAppsGrid();
        Controls.Add(BuildRootLayout());

        Load += (_, _) =>
        {
            LoadConfig();
            RefreshStatus();
        };

        Shown += async (_, _) =>
        {
            if (_appRows.Count == 0)
                await RefreshInstalledApplicationsAsync();
        };
    }

    private Control BuildRootLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Bg
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildTabs(), 0, 1);
        root.Controls.Add(BuildActionBar(), 0, 2);

        return root;
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg
        };

        panel.Controls.Add(new Label
        {
            Text = "GPA Windows",
            Font = new Font("Segoe UI Semibold", 24f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(0, 0)
        });

        panel.Controls.Add(new Label
        {
            Text = "Gerenciador de Políticas Administrativas locais",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(3, 42)
        });

        return panel;
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10f),
            Padding = new Point(16, 7)
        };

        tabs.TabPages.Add(BuildPoliciesTab());
        tabs.TabPages.Add(BuildApplicationsTab());
        tabs.TabPages.Add(BuildNetworkTab());
        tabs.TabPages.Add(BuildStatusTab());

        return tabs;
    }

    private TabPage BuildPoliciesTab()
    {
        var page = NewTabPage("Políticas");

        var scroll = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Bg,
            Padding = new Padding(18)
        };

        scroll.Controls.Add(SectionTitle("Fiscalização"));
        scroll.Controls.Add(_enabled);
        scroll.Controls.Add(BuildIntervalRow());

        scroll.Controls.Add(SectionTitle("Personalização"));
        scroll.Controls.Add(_lockWallpaper);
        scroll.Controls.Add(_lockTheme);

        scroll.Controls.Add(SectionTitle("Áudio"));
        scroll.Controls.Add(_blockAudio);
        scroll.Controls.Add(DescriptionLabel(
            "Quando ativado, o GPA desabilita os endpoints PnP de áudio. " +
            "A política é reaplicada periodicamente e os dispositivos desligados pelo GPA " +
            "são lembrados para restauração posterior. O bloqueio também pode afetar microfones."));

        return page.With(scroll);
    }

    private Control BuildIntervalRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(3, 4, 3, 18)
        };

        row.Controls.Add(new Label
        {
            Text = "Reaplicar diretivas gerais a cada",
            AutoSize = true,
            ForeColor = TextPrimary,
            Padding = new Padding(0, 5, 6, 0)
        });

        row.Controls.Add(_interval);

        row.Controls.Add(new Label
        {
            Text = "segundos",
            AutoSize = true,
            ForeColor = TextPrimary,
            Padding = new Padding(6, 5, 0, 0)
        });

        return row;
    }

    private TabPage BuildApplicationsTab()
    {
        var page = NewTabPage("Aplicativos");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16),
            BackColor = Bg
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Bg
        };

        options.Controls.Add(_applicationControl);
        options.Controls.Add(_blockUnknownApps);
        options.Controls.Add(DescriptionLabel(
            "Marcado na coluna Permitir = autorizado. Desmarcado = o serviço encerra o programa " +
            "quando detectar sua execução. Componentes do Windows e o próprio GPA são protegidos."));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Bg
        };

        var refresh = NewButton("Atualizar lista");
        refresh.Click += async (_, _) => await RefreshInstalledApplicationsAsync();

        var addExe = NewButton("Adicionar EXE...");
        addExe.Click += (_, _) => AddExecutableManually();

        var allowAll = NewButton("Permitir todos");
        allowAll.Click += (_, _) => SetAllManageableApplications(true);

        var blockAll = NewButton("Bloquear todos");
        blockAll.Click += (_, _) => SetAllManageableApplications(false);

        toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(addExe);
        toolbar.Controls.Add(allowAll);
        toolbar.Controls.Add(blockAll);

        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg
        };

        _appCount.Location = new Point(0, 5);
        footer.Controls.Add(_appCount);

        var hint = DescriptionLabel(
            "Programas portáteis podem ser incluídos com “Adicionar EXE...”. " +
            "Entradas sem caminho de executável não podem ser bloqueadas até o EXE ser informado.");
        hint.Location = new Point(0, 27);
        footer.Controls.Add(hint);

        layout.Controls.Add(options, 0, 0);
        layout.Controls.Add(toolbar, 0, 1);
        layout.Controls.Add(_appsGrid, 0, 2);
        layout.Controls.Add(footer, 0, 3);

        return page.With(layout);
    }

    private TabPage BuildNetworkTab()
    {
        var page = NewTabPage("Rede / DNS");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16),
            BackColor = Bg
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Bg
        };

        options.Controls.Add(_dnsAllowList);
        options.Controls.Add(_disableDoH);
        options.Controls.Add(_sanitizeHosts);

        var domainsLabel = new Label
        {
            Text = "Domínios permitidos — um por linha. Permitir exemplo.com também libera seus subdomínios.",
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var dnsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Bg,
            Padding = new Padding(0, 8, 0, 0)
        };

        dnsRow.Controls.Add(new Label
        {
            Text = "DNS upstream:",
            AutoSize = true,
            ForeColor = TextPrimary,
            Padding = new Padding(0, 5, 6, 0)
        });
        dnsRow.Controls.Add(_upstreamDns);

        layout.Controls.Add(options, 0, 0);
        layout.Controls.Add(domainsLabel, 0, 1);
        layout.Controls.Add(_domains, 0, 2);
        layout.Controls.Add(dnsRow, 0, 3);

        return page.With(layout);
    }

    private TabPage BuildStatusTab()
    {
        var page = NewTabPage("Status");

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor = Bg,
            Panel1MinSize = 0,
            Panel2MinSize = 0
        };

        // During construction a docked SplitContainer can still have Width == 0.
        // Setting SplitterDistance in the initializer then throws before the form
        // is shown. Apply a safe distance only after the control has a real size.
        split.HandleCreated += (_, _) => SetSafeStatusSplitterDistance(split);
        split.Resize += (_, _) => SetSafeStatusSplitterDistance(split);

        var status = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(18),
            BackColor = Bg
        };

        status.Controls.Add(SectionTitle("Estado do GPA"));
        status.Controls.Add(StatusPair("Serviço", _serviceStatus));
        status.Controls.Add(StatusPair("Última aplicação", _lastApplied));
        status.Controls.Add(StatusPair("Configuração", _configPath));

        var refresh = NewButton("Atualizar status");
        refresh.Margin = new Padding(3, 18, 3, 3);
        refresh.Click += (_, _) => RefreshStatus();
        status.Controls.Add(refresh);

        var logHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18),
            BackColor = Bg
        };
        logHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        logHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logHost.Controls.Add(SectionTitle("Atividade"), 0, 0);
        logHost.Controls.Add(_log, 0, 1);

        split.Panel1.Controls.Add(status);
        split.Panel2.Controls.Add(logHost);

        return page.With(split);
    }

    private static void SetSafeStatusSplitterDistance(SplitContainer split)
    {
        if (split.Width <= 0)
            return;

        var minimum = 280;
        var maximum = Math.Max(minimum, split.Width - 300);
        split.SplitterDistance = Math.Clamp(350, minimum, maximum);
    }

    private Control BuildActionBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Bg,
            Padding = new Padding(0, 10, 0, 0)
        };

        var apply = NewButton("Salvar e aplicar", primary: true);
        apply.Click += async (_, _) =>
            await RunUiActionAsync("Aplicando políticas...", ApplyPolicies);

        var revert = NewButton("Restaurar políticas");
        revert.Click += async (_, _) =>
            await RunUiActionAsync("Restaurando políticas...", RevertPolicies);

        var install = NewButton("Instalar serviço");
        install.Click += async (_, _) =>
            await RunUiActionAsync(
                "Instalando serviço...",
                () => WindowsServiceManager.EnsureInstalledAndRunning());

        var remove = NewButton("Remover serviço");
        remove.Click += async (_, _) =>
            await RunUiActionAsync(
                "Removendo serviço...",
                WindowsServiceManager.Remove);

        bar.Controls.Add(apply);
        bar.Controls.Add(revert);
        bar.Controls.Add(install);
        bar.Controls.Add(remove);

        return bar;
    }

    private void ConfigureAppsGrid()
    {
        _appsGrid.EnableHeadersVisualStyles = false;
        _appsGrid.ColumnHeadersDefaultCellStyle.BackColor = Surface2;
        _appsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _appsGrid.DefaultCellStyle.BackColor = Color.FromArgb(20, 24, 31);
        _appsGrid.DefaultCellStyle.ForeColor = Color.FromArgb(225, 231, 239);
        _appsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 78, 120);
        _appsGrid.DefaultCellStyle.SelectionForeColor = Color.White;

        _appsGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ManagedApplication.Allowed),
            HeaderText = "Permitir",
            Width = 72,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        });

        _appsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedApplication.DisplayName),
            HeaderText = "Programa",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 25
        });

        _appsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedApplication.Publisher),
            HeaderText = "Fabricante",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 20
        });

        _appsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedApplication.ExecutablePath),
            HeaderText = "Executável",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 55
        });

        _appsGrid.DataSource = _appRows;
    }

    private void LoadConfig()
    {
        var config = ConfigStore.Load();

        _enabled.Checked = config.Enabled;
        _lockWallpaper.Checked = config.LockWallpaper;
        _lockTheme.Checked = config.LockTheme;
        _blockAudio.Checked = config.BlockAudio;
        _applicationControl.Checked = config.ApplicationControlEnabled;
        _blockUnknownApps.Checked = config.BlockUnknownApplications;

        _dnsAllowList.Checked = config.DnsAllowListEnabled;
        _disableDoH.Checked = config.DisableBrowserDoH;
        _sanitizeHosts.Checked = config.SanitizeHostsWhenDnsAllowListEnabled;
        _upstreamDns.Text = config.UpstreamDns;
        _interval.Value = Math.Clamp(config.EnforcementIntervalSeconds, 10, 3600);
        _domains.Text = string.Join(Environment.NewLine, config.AllowedDomains);

        ReplaceAppRows(config.ManagedApplications);
        UpdateAppCount();
    }

    private PolicyConfig ReadConfig()
    {
        _appsGrid.EndEdit();

        if (BindingContext is not null && BindingContext[_appRows] is CurrencyManager manager)
            manager.EndCurrentEdit();

        var previous = ConfigStore.Load();

        previous.Enabled = _enabled.Checked;
        previous.LockWallpaper = _lockWallpaper.Checked;
        previous.LockTheme = _lockTheme.Checked;
        previous.BlockAudio = _blockAudio.Checked;
        previous.ApplicationControlEnabled = _applicationControl.Checked;
        previous.BlockUnknownApplications = _blockUnknownApps.Checked;

        previous.ManagedApplications = _appRows
            .Select(CloneApplication)
            .ToList();

        previous.DnsAllowListEnabled = _dnsAllowList.Checked;
        previous.DisableBrowserDoH = _disableDoH.Checked;
        previous.SanitizeHostsWhenDnsAllowListEnabled = _sanitizeHosts.Checked;
        previous.UpstreamDns = _upstreamDns.Text.Trim();
        previous.EnforcementIntervalSeconds = (int)_interval.Value;
        previous.AllowedDomains = _domains.Lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return previous;
    }

    private async Task RefreshInstalledApplicationsAsync()
    {
        try
        {
            UseWaitCursor = true;
            AppendLog("Lendo programas instalados...");

            var current = _appRows
                .GroupBy(AppKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => CloneApplication(g.First()), StringComparer.OrdinalIgnoreCase);

            var manuallyAdded = _appRows
                .Where(x => string.Equals(
                    x.Publisher,
                    "Adicionado manualmente",
                    StringComparison.OrdinalIgnoreCase))
                .Select(CloneApplication)
                .ToList();

            var scanned = await Task.Run(AppInventoryService.Scan);

            foreach (var app in scanned)
            {
                if (current.TryGetValue(AppKey(app), out var previous))
                    app.Allowed = previous.Allowed;
            }

            foreach (var manual in manuallyAdded)
            {
                if (scanned.All(y => !string.Equals(
                        y.ExecutablePath,
                        manual.ExecutablePath,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    scanned.Add(manual);
                }
            }

            ReplaceAppRows(scanned);
            UpdateAppCount();
            AppendLog($"Inventário atualizado: {_appRows.Count} aplicativos.");
        }
        catch (Exception ex)
        {
            AppendLog("ERRO ao inventariar aplicativos: " + ex.Message);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AddExecutableManually()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Adicionar executável ao controle de aplicativos",
            Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var fullPath = Path.GetFullPath(dialog.FileName);

        if (_appRows.Any(x => string.Equals(
                x.ExecutablePath,
                fullPath,
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _appRows.Add(new ManagedApplication
        {
            DisplayName = Path.GetFileNameWithoutExtension(fullPath),
            ExecutablePath = fullPath,
            ProcessName = Path.GetFileName(fullPath),
            Publisher = "Adicionado manualmente",
            Allowed = true
        });

        UpdateAppCount();
    }

    private void SetAllManageableApplications(bool allowed)
    {
        _appsGrid.EndEdit();

        foreach (var app in _appRows.Where(x => !string.IsNullOrWhiteSpace(x.ExecutablePath)))
            app.Allowed = allowed;

        _appsGrid.Refresh();
    }

    private void ReplaceAppRows(IEnumerable<ManagedApplication> applications)
    {
        _appRows.RaiseListChangedEvents = false;

        try
        {
            _appRows.Clear();

            foreach (var app in applications
                         .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                _appRows.Add(CloneApplication(app));
            }
        }
        finally
        {
            _appRows.RaiseListChangedEvents = true;
            _appRows.ResetBindings();
        }
    }

    private void UpdateAppCount()
    {
        var manageable = _appRows.Count(x => !string.IsNullOrWhiteSpace(x.ExecutablePath));
        var blocked = _appRows.Count(x => !x.Allowed && !string.IsNullOrWhiteSpace(x.ExecutablePath));
        _appCount.Text = $"{_appRows.Count} encontrados • {manageable} com EXE identificado • {blocked} bloqueados";
    }

    private void ApplyPolicies()
    {
        var config = ReadConfig();

        PolicyEngine.Validate(config);
        ConfigStore.Save(config);

        if (config.Enabled)
            WindowsServiceManager.EnsureInstalledAndRunning();

        PolicyEngine.Apply(config);
        AppendLog("Políticas salvas e aplicadas com sucesso.");
    }

    private void RevertPolicies()
    {
        var config = ConfigStore.Load();

        WindowsServiceManager.Stop();
        PolicyEngine.RevertAll(config);

        LoadConfig();
        AppendLog("Políticas gerenciadas pelo GPA Windows foram restauradas.");
    }

    private async Task RunUiActionAsync(string startMessage, Action action)
    {
        try
        {
            AppendLog(startMessage);
            UseWaitCursor = true;
            Enabled = false;

            await Task.Run(action);

            RefreshStatus();
        }
        catch (Exception ex)
        {
            AppendLog("ERRO: " + ex.Message);
            MessageBox.Show(
                this,
                ex.Message,
                "GPA Windows",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void RefreshStatus()
    {
        var config = ConfigStore.Load();

        _serviceStatus.Text = WindowsServiceManager.GetStatus();
        _lastApplied.Text = config.LastAppliedUtc is null
            ? "Nunca"
            : config.LastAppliedUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        _configPath.Text = ConfigStore.ConfigPath;
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _log.ScrollToCaret();
    }

    private static TabPage NewTabPage(string text) => new(text)
    {
        BackColor = Bg,
        ForeColor = TextPrimary,
        Padding = new Padding(0)
    };

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 13f),
        ForeColor = Color.White,
        Margin = new Padding(3, 5, 3, 10)
    };

    private static Label DescriptionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = TextMuted,
        MaximumSize = new Size(820, 0),
        Margin = new Padding(3, 5, 3, 12)
    };

    private static Control StatusPair(string caption, Label value)
    {
        var panel = new TableLayoutPanel
        {
            Width = 300,
            Height = 62,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(3, 4, 3, 4),
            BackColor = Surface
        };

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            ForeColor = TextMuted,
            Margin = new Padding(10, 7, 0, 0)
        }, 0, 0);

        value.Margin = new Padding(10, 2, 6, 0);
        panel.Controls.Add(value, 0, 1);

        return panel;
    }

    private static CheckBox NewCheckBox(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = TextPrimary,
        Margin = new Padding(3, 5, 3, 5)
    };

    private static Button NewButton(string text, bool primary = false) => new()
    {
        Text = text,
        AutoSize = true,
        Height = 36,
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Color.FromArgb(31, 111, 235) : Color.FromArgb(48, 54, 61),
        ForeColor = Color.White,
        FlatAppearance = { BorderSize = 0 },
        Padding = new Padding(10, 0, 10, 0),
        Margin = new Padding(4, 2, 4, 2),
        Cursor = Cursors.Hand
    };

    private static Label NewValueLabel() => new()
    {
        AutoSize = true,
        ForeColor = Color.FromArgb(220, 226, 234),
        MaximumSize = new Size(650, 0)
    };

    private static ManagedApplication CloneApplication(ManagedApplication source) => new()
    {
        DisplayName = source.DisplayName,
        Publisher = source.Publisher,
        ExecutablePath = source.ExecutablePath,
        ProcessName = source.ProcessName,
        Allowed = source.Allowed
    };

    private static string AppKey(ManagedApplication app) =>
        !string.IsNullOrWhiteSpace(app.ExecutablePath)
            ? app.ExecutablePath
            : $"{app.DisplayName}|{app.Publisher}";
}

internal static class TabPageExtensions
{
    public static TabPage With(this TabPage page, Control control)
    {
        page.Controls.Add(control);
        return page;
    }
}
