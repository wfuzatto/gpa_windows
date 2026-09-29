using System.ComponentModel;
using GpaWindows.Models;
using GpaWindows.Services;

namespace GpaWindows;

public sealed class MainForm : Form
{
    private readonly CheckBox _enabled = NewCheckBox("Ativar fiscalização contínua");
    private readonly CheckBox _lockWallpaper = NewCheckBox("Impedir alteração do papel de parede");
    private readonly CheckBox _lockTheme = NewCheckBox("Impedir alteração do tema do Windows");
    private readonly CheckBox _blockAudio = NewCheckBox("Bloquear áudio do computador (desabilitar endpoints de áudio)");

    private readonly CheckBox _applicationControl = NewCheckBox("Ativar controle de aplicativos");
    private readonly CheckBox _blockUnknownApps = NewCheckBox("Modo estrito: bloquear executáveis desconhecidos/portáteis não autorizados");

    private readonly CheckBox _dnsAllowList = NewCheckBox("Ativar DNS Allowlist (bloquear qualquer domínio não autorizado)");
    private readonly CheckBox _disableDoH = NewCheckBox("Desativar DNS-over-HTTPS no Edge e Chrome");
    private readonly CheckBox _sanitizeHosts = NewCheckBox("Proteger/limpar o arquivo hosts durante o modo estrito");

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
        BorderStyle = BorderStyle.None,
        Height = 300,
        Width = 650,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
        ColumnHeadersHeight = 36,
        RowTemplate = { Height = 32 }
    };

    private readonly TextBox _domains = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Height = 150,
        Dock = DockStyle.Top,
        Font = new Font("Consolas", 10.5f),
        PlaceholderText = "exemplo.com\r\napi.exemplo.com\r\n..."
    };

    private readonly TextBox _upstreamDns = new()
    {
        Width = 160,
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

    private readonly RichTextBox _log = new()
    {
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = Color.FromArgb(20, 24, 31),
        ForeColor = Color.FromArgb(205, 214, 226),
        Font = new Font("Consolas", 9.5f),
        Dock = DockStyle.Fill
    };

    public MainForm()
    {
        Text = "GPA Windows — Diretivas Locais";
        Width = 1240;
        Height = 900;
        MinimumSize = new Size(1040, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(13, 17, 23);
        ForeColor = Color.FromArgb(235, 240, 246);
        Font = new Font("Segoe UI", 10f);

        ConfigureAppsGrid();
        Controls.Add(BuildLayout());

        Load += (_, _) =>
        {
            LoadConfig();
            RefreshStatus();
        };
    }

    private void ConfigureAppsGrid()
    {
        _appsGrid.EnableHeadersVisualStyles = false;
        _appsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(33, 38, 45);
        _appsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _appsGrid.DefaultCellStyle.BackColor = Color.FromArgb(20, 24, 31);
        _appsGrid.DefaultCellStyle.ForeColor = Color.FromArgb(225, 231, 239);
        _appsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 78, 120);
        _appsGrid.DefaultCellStyle.SelectionForeColor = Color.White;

        _appsGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ManagedApplication.Allowed),
            HeaderText = "Permitir",
            Width = 70
        });

        _appsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedApplication.DisplayName),
            HeaderText = "Programa",
            Width = 190,
            ReadOnly = true
        });

        _appsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedApplication.Publisher),
            HeaderText = "Fabricante",
            Width = 140,
            ReadOnly = true
        });

        _appsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ManagedApplication.ExecutablePath),
            HeaderText = "Executável",
            Width = 300,
            ReadOnly = true
        });

        _appsGrid.DataSource = _appRows;
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 2,
            RowCount = 2
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new Panel { Dock = DockStyle.Fill };
        header.Controls.Add(new Label
        {
            Text = "GPA Windows",
            Font = new Font("Segoe UI Semibold", 24f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(0, 0)
        });
        header.Controls.Add(new Label
        {
            Text = "Gerenciador de Políticas Administrativas locais",
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = Color.FromArgb(145, 156, 170),
            AutoSize = true,
            Location = new Point(3, 43)
        });

        root.SetColumnSpan(header, 2);
        root.Controls.Add(header, 0, 0);

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 0, 14, 0)
        };

        left.Controls.Add(BuildGeneralCard());
        left.Controls.Add(BuildPersonalizationCard());
        left.Controls.Add(BuildAudioCard());
        left.Controls.Add(BuildApplicationControlCard());
        left.Controls.Add(BuildDnsCard());
        left.Controls.Add(BuildActionsCard());

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Padding = new Padding(8, 0, 0, 0)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(BuildStatusCard(), 0, 0);
        right.Controls.Add(BuildLogCard(), 0, 1);

        root.Controls.Add(left, 0, 1);
        root.Controls.Add(right, 1, 1);

        return root;
    }

    private Control BuildGeneralCard()
    {
        var panel = Card("Fiscalização");
        var body = BodyFlow();

        _enabled.Checked = true;
        body.Controls.Add(_enabled);

        var intervalRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(3, 10, 3, 3)
        };
        intervalRow.Controls.Add(new Label
        {
            Text = "Reaplicar diretivas gerais a cada",
            AutoSize = true,
            Padding = new Padding(0, 5, 4, 0)
        });
        intervalRow.Controls.Add(_interval);
        intervalRow.Controls.Add(new Label
        {
            Text = "segundos",
            AutoSize = true,
            Padding = new Padding(4, 5, 0, 0)
        });

        body.Controls.Add(intervalRow);
        panel.Controls.Add(body);
        return panel;
    }

    private Control BuildPersonalizationCard()
    {
        var panel = Card("Personalização do Windows");
        var body = BodyFlow();
        body.Controls.Add(_lockWallpaper);
        body.Controls.Add(_lockTheme);
        panel.Controls.Add(body);
        return panel;
    }

    private Control BuildAudioCard()
    {
        var panel = Card("Áudio");
        var body = BodyFlow();

        body.Controls.Add(_blockAudio);
        body.Controls.Add(new Label
        {
            Text = "O bloqueio desabilita os endpoints PnP de áudio. Isso impede reprodução e também pode indisponibilizar microfones enquanto a política estiver ativa.",
            ForeColor = Color.FromArgb(160, 170, 182),
            AutoSize = true,
            MaximumSize = new Size(650, 0),
            Margin = new Padding(3, 6, 3, 3)
        });

        panel.Controls.Add(body);
        return panel;
    }

    private Control BuildApplicationControlCard()
    {
        var panel = Card("Controle de aplicativos");
        var body = BodyFlow();

        body.Controls.Add(_applicationControl);
        body.Controls.Add(_blockUnknownApps);

        body.Controls.Add(new Label
        {
            Text = "Marcado = permitido. Desmarcado = o serviço encerra o programa ao detectar sua execução. Componentes do Windows e o próprio GPA Windows são protegidos.",
            ForeColor = Color.FromArgb(160, 170, 182),
            AutoSize = true,
            MaximumSize = new Size(650, 0),
            Margin = new Padding(3, 6, 3, 8)
        });

        var toolbar = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(3, 0, 3, 8)
        };

        var refresh = NewButton("Atualizar lista");
        refresh.Click += (_, _) => RefreshInstalledApplications();

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

        body.Controls.Add(toolbar);
        body.Controls.Add(_appsGrid);

        body.Controls.Add(new Label
        {
            Text = "Entradas sem caminho de executável foram inventariadas, mas não podem ser bloqueadas até que um EXE correspondente seja informado.",
            ForeColor = Color.FromArgb(160, 170, 182),
            AutoSize = true,
            MaximumSize = new Size(650, 0),
            Margin = new Padding(3, 6, 3, 3)
        });

        panel.Controls.Add(body);
        return panel;
    }

    private Control BuildDnsCard()
    {
        var panel = Card("Rede / DNS Allowlist");
        var body = BodyFlow();

        body.Controls.Add(_dnsAllowList);
        body.Controls.Add(_disableDoH);
        body.Controls.Add(_sanitizeHosts);

        body.Controls.Add(new Label
        {
            Text = "Domínios permitidos — um por linha. Permitir exemplo.com também libera seus subdomínios.",
            ForeColor = Color.FromArgb(160, 170, 182),
            AutoSize = true,
            MaximumSize = new Size(650, 0),
            Margin = new Padding(3, 12, 3, 6)
        });

        _domains.Width = 650;
        body.Controls.Add(_domains);

        var dnsRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(3, 10, 3, 0)
        };
        dnsRow.Controls.Add(new Label
        {
            Text = "DNS upstream:",
            AutoSize = true,
            Padding = new Padding(0, 5, 6, 0)
        });
        dnsRow.Controls.Add(_upstreamDns);
        body.Controls.Add(dnsRow);

        panel.Controls.Add(body);
        return panel;
    }

    private Control BuildActionsCard()
    {
        var panel = Card("Ações");
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(14, 8, 14, 14)
        };

        var apply = NewButton("Salvar e aplicar", primary: true);
        apply.Click += async (_, _) => await RunUiActionAsync("Aplicando políticas...", ApplyPolicies);

        var revert = NewButton("Restaurar políticas");
        revert.Click += async (_, _) => await RunUiActionAsync("Restaurando políticas...", RevertPolicies);

        var install = NewButton("Instalar serviço");
        install.Click += async (_, _) => await RunUiActionAsync(
            "Instalando serviço...",
            () => WindowsServiceManager.EnsureInstalledAndRunning());

        var remove = NewButton("Remover serviço");
        remove.Click += async (_, _) => await RunUiActionAsync(
            "Removendo serviço...",
            WindowsServiceManager.Remove);

        body.Controls.Add(apply);
        body.Controls.Add(revert);
        body.Controls.Add(install);
        body.Controls.Add(remove);

        panel.Controls.Add(body);
        return panel;
    }

    private Control BuildStatusCard()
    {
        var panel = Card("Status");
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(14, 8, 14, 14)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddStatusRow(body, 0, "Serviço", _serviceStatus);
        AddStatusRow(body, 1, "Última aplicação", _lastApplied);
        AddStatusRow(body, 2, "Configuração", _configPath);

        var refresh = NewButton("Atualizar status");
        refresh.Click += (_, _) => RefreshStatus();
        body.Controls.Add(refresh, 1, 3);

        panel.Controls.Add(body);
        return panel;
    }

    private Control BuildLogCard()
    {
        var panel = Card("Atividade");
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 8, 14, 14)
        };
        host.Controls.Add(_log);
        panel.Controls.Add(host);
        return panel;
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

        if (_appRows.Count == 0)
            RefreshInstalledApplications();
    }

    private PolicyConfig ReadConfig()
    {
        _appsGrid.EndEdit();

        if (BindingContext[_appRows] is CurrencyManager manager)
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

    private void RefreshInstalledApplications()
    {
        try
        {
            Cursor = Cursors.WaitCursor;

            var current = _appRows
                .GroupBy(AppKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var scanned = AppInventoryService.Scan();

            foreach (var app in scanned)
            {
                if (current.TryGetValue(AppKey(app), out var previous))
                    app.Allowed = previous.Allowed;
            }

            foreach (var manuallyAdded in _appRows.Where(x =>
                         !string.IsNullOrWhiteSpace(x.ExecutablePath) &&
                         scanned.All(y => !string.Equals(
                             y.ExecutablePath,
                             x.ExecutablePath,
                             StringComparison.OrdinalIgnoreCase))))
            {
                scanned.Add(CloneApplication(manuallyAdded));
            }

            ReplaceAppRows(scanned);
            AppendLog($"Inventário atualizado: {_appRows.Count} aplicativos.");
        }
        catch (Exception ex)
        {
            AppendLog("ERRO ao inventariar aplicativos: " + ex.Message);
        }
        finally
        {
            Cursor = Cursors.Default;
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

    private static Panel Card(string title)
    {
        var panel = new Panel
        {
            Width = 710,
            Height = 100,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.FromArgb(22, 27, 34),
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(0)
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(14, 12, 0, 0),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11f)
        };

        panel.Controls.Add(titleLabel);
        titleLabel.BringToFront();

        return panel;
    }

    private static FlowLayoutPanel BodyFlow() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(14, 8, 14, 14)
    };

    private static CheckBox NewCheckBox(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.FromArgb(225, 231, 239),
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
        Margin = new Padding(3, 3, 8, 3),
        Cursor = Cursors.Hand
    };

    private static Label NewValueLabel() => new()
    {
        AutoSize = true,
        ForeColor = Color.FromArgb(220, 226, 234),
        MaximumSize = new Size(250, 0)
    };

    private static void AddStatusRow(TableLayoutPanel panel, int row, string caption, Label value)
    {
        panel.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            ForeColor = Color.FromArgb(140, 151, 165)
        }, 0, row);

        panel.Controls.Add(value, 1, row);
    }
}
