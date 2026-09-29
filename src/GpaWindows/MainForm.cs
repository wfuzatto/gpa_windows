using GpaWindows.Models;
using GpaWindows.Services;

namespace GpaWindows;

public sealed class MainForm : Form
{
    private readonly CheckBox _enabled = NewCheckBox("Ativar fiscalização contínua");
    private readonly CheckBox _lockWallpaper = NewCheckBox("Impedir alteração do papel de parede");
    private readonly CheckBox _lockTheme = NewCheckBox("Impedir alteração do tema do Windows");
    private readonly CheckBox _dnsAllowList = NewCheckBox("Ativar DNS Allowlist (bloquear qualquer domínio não autorizado)");
    private readonly CheckBox _disableDoH = NewCheckBox("Desativar DNS-over-HTTPS no Edge e Chrome");
    private readonly CheckBox _sanitizeHosts = NewCheckBox("Proteger/limpar o arquivo hosts durante o modo estrito");

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
        Width = 1080;
        Height = 820;
        MinimumSize = new Size(940, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(13, 17, 23);
        ForeColor = Color.FromArgb(235, 240, 246);
        Font = new Font("Segoe UI", 10f);

        Controls.Add(BuildLayout());

        Load += (_, _) =>
        {
            LoadConfig();
            RefreshStatus();
        };
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

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
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
            Text = "Reaplicar a cada",
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
            MaximumSize = new Size(560, 0),
            Margin = new Padding(3, 12, 3, 6)
        });

        _domains.Width = 560;
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
        _dnsAllowList.Checked = config.DnsAllowListEnabled;
        _disableDoH.Checked = config.DisableBrowserDoH;
        _sanitizeHosts.Checked = config.SanitizeHostsWhenDnsAllowListEnabled;
        _upstreamDns.Text = config.UpstreamDns;
        _interval.Value = Math.Clamp(config.EnforcementIntervalSeconds, 10, 3600);
        _domains.Text = string.Join(Environment.NewLine, config.AllowedDomains);
    }

    private PolicyConfig ReadConfig()
    {
        var previous = ConfigStore.Load();

        previous.Enabled = _enabled.Checked;
        previous.LockWallpaper = _lockWallpaper.Checked;
        previous.LockTheme = _lockTheme.Checked;
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

    private static Panel Card(string title)
    {
        var panel = new Panel
        {
            Width = 610,
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
