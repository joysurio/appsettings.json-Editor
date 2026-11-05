using System.Diagnostics;
using AppSettingsEditor.Models;
using AppSettingsEditor.Services;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Mail;
using System.Net;
using System.Net.Http;
using System.Text;

namespace AppSettingsEditor.Controls;

/// <summary>
/// Full RemoteMachineAgent companion: config.json, PC/network diagnostics, DB test, machine list, send commands, and command reference.
/// </summary>
internal class RemoteMachineAgentControl : UserControl
{
    private readonly RemoteMachineAgentConfigService _configService = new();
    private readonly RemoteMachineAgentOperationsService _operations = new();

    private TabControl _tabs = null!;
    private Label _statusLabel = null!;

    private TextBox _configPathTextBox = null!;
    private TextBox _connectionStringTextBox = null!;
    private ComboBox _machineIdCombo = null!;
    private NumericUpDown _heartbeatUpDown = null!;
    private NumericUpDown _commandCheckUpDown = null!;
    private CheckBox _minimizeTrayCheckBox = null!;
    private CheckBox _startWithWindowsCheckBox = null!;

    private Label _diagMachineName = null!;
    private Label _diagConfiguredId = null!;
    private Label _diagIp = null!;
    private Label _diagNetwork = null!;
    private Label _diagNic = null!;
    private ListBox _machinesListBox = null!;

    private ComboBox _sendTargetCombo = null!;
    private ComboBox _sendCommandTypeCombo = null!;
    private TextBox _sendCommandDataTextBox = null!;
    private TextBox _activityLogTextBox = null!;
    private List<RemoteMachineAgentOperationsService.MachineCatalogItem> _machineCatalog = new();
    private System.Windows.Forms.Timer? _heartbeatTimer;
    private System.Windows.Forms.Timer? _commandTimer;
    private bool _commandCheckInProgress;
    private bool _initialConfigLoaded;

    public RemoteMachineAgentControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UIThemeService.LightGray;
        BuildUi();
        Load += async (_, _) =>
        {
            if (_initialConfigLoaded)
                return;

            _initialConfigLoaded = true;
            await LoadConfigAsync();
        };
        RefreshPcDiagnostics();
        InitializeBackgroundAgentTimers();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        _tabs = new TabControl { Dock = DockStyle.Fill };
        _tabs.TabPages.Add(BuildConfigurationTab());
        _tabs.TabPages.Add(BuildDiagnosticsTab());
        _tabs.TabPages.Add(BuildSendCommandTab());
        _tabs.TabPages.Add(BuildReferenceTab());
        root.Controls.Add(_tabs, 0, 0);

        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (_tabs.SelectedTab?.Text == "Diagnostics")
                RefreshPcDiagnostics();
        };

        _statusLabel = new Label { Dock = DockStyle.Fill, ForeColor = Color.DimGray, Text = "" };
        root.Controls.Add(_statusLabel, 0, 1);
        Controls.Add(root);
    }

    private void InitializeBackgroundAgentTimers()
    {
        _heartbeatTimer = new System.Windows.Forms.Timer();
        _heartbeatTimer.Tick += async (_, _) => await RunHeartbeatTickAsync();

        _commandTimer = new System.Windows.Forms.Timer();
        _commandTimer.Tick += async (_, _) => await RunCommandTickAsync();

        ApplyTimerIntervalsFromConfig();
        _heartbeatTimer.Start();
        _commandTimer.Start();
    }

    private void ApplyTimerIntervalsFromConfig()
    {
        if (_heartbeatTimer != null)
        {
            var heartbeatSec = Math.Clamp((int)_heartbeatUpDown.Value, 10, 3600);
            _heartbeatTimer.Interval = heartbeatSec * 1000;
        }

        if (_commandTimer != null)
        {
            var commandSec = Math.Clamp((int)_commandCheckUpDown.Value, 5, 3600);
            _commandTimer.Interval = commandSec * 1000;
        }
    }

    private async Task RunHeartbeatTickAsync()
    {
        var cs = GetConnectionStringOrEmpty();
        if (string.IsNullOrWhiteSpace(cs))
            return;

        var machineId = GetConfiguredMachineId();
        if (string.IsNullOrWhiteSpace(machineId))
            return;

        var result = await _operations.UpsertMachineHeartbeatDetailedAsync(
            cs,
            machineId,
            Environment.MachineName,
            RemoteMachineAgentNetworkHelper.GetLocalIPAddress(),
            isOnline: true);
        if (result.Success)
        {
            _statusLabel.Text = $"Last heartbeat: {DateTime.Now:HH:mm:ss}";
        }
        else
        {
            AppendLog($"Heartbeat timer failed ({result.ErrorMessage})");
        }
    }

    private async Task RunCommandTickAsync()
    {
        if (_commandCheckInProgress)
            return;

        var cs = GetConnectionStringOrEmpty();
        if (string.IsNullOrWhiteSpace(cs))
            return;

        _commandCheckInProgress = true;
        try
        {
            var processed = await ProcessPendingCommandsInternalAsync(cs, showDialogs: false);
            if (processed > 0)
            {
                _statusLabel.Text = $"Auto processed {processed} command(s) at {DateTime.Now:HH:mm:ss}";
            }
        }
        finally
        {
            _commandCheckInProgress = false;
        }
    }

    private TabPage BuildConfigurationTab()
    {
        var tab = new TabPage("Configuration");
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var form = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Padding = new Padding(8),
            MaximumSize = new Size(980, 0)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        int r = 0;
        form.Controls.Add(MkLabel("config.json path"), 0, r);
        _configPathTextBox = new TextBox { Dock = DockStyle.Fill };
        form.Controls.Add(_configPathTextBox, 1, r);
        var browseCfg = new Button { Text = "Browse…", Dock = DockStyle.Fill };
        browseCfg.Click += (_, _) => BrowseConfigFile();
        form.Controls.Add(browseCfg, 2, r++);
        _configPathTextBox.Text = RemoteMachineAgentConfigService.GetDefaultConfigPath();

        var loadBtn = new Button { Text = "Load" };
        UIThemeService.ApplyModernButtonStyle(loadBtn);
        loadBtn.Click += async (_, _) => await LoadConfigAsync();
        var saveBtn = new Button { Text = "Save" };
        UIThemeService.ApplyModernButtonStyle(saveBtn, isPrimary: true);
        saveBtn.Click += (_, _) => SaveConfig();
        var folderBtn = new Button { Text = "Open folder" };
        folderBtn.Click += (_, _) => OpenConfigFolder();
        var resetPathBtn = new Button { Text = "Default path" };
        resetPathBtn.Click += (_, _) => _configPathTextBox.Text = RemoteMachineAgentConfigService.GetDefaultConfigPath();
        var bar = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        bar.Controls.AddRange(new Control[] { loadBtn, saveBtn, folderBtn, resetPathBtn });
        form.Controls.Add(bar, 0, r);
        form.SetColumnSpan(bar, 3);
        r++;

        form.Controls.Add(MkLabel("Database connection string", true), 0, r);
        _connectionStringTextBox = new TextBox
        {
            Multiline = true,
            MinimumSize = new Size(400, 72),
            Height = 72,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false
        };
        _connectionStringTextBox.Leave += async (_, _) =>
        {
            if (_machineIdCombo.Items.Count == 0)
                await LoadMachineCatalogAsync(showErrors: false);
        };
        form.Controls.Add(_connectionStringTextBox, 1, r);
        form.SetColumnSpan(_connectionStringTextBox, 2);
        r++;

        form.Controls.Add(MkLabel("Machine ID (heartbeat / Machines table)"), 0, r);
        _machineIdCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _machineIdCombo.SelectedIndexChanged += (_, _) => RefreshPcDiagnostics();
        form.Controls.Add(_machineIdCombo, 1, r);
        var refreshMachineCatalogBtn = new Button { Text = "Refresh machines", Dock = DockStyle.Fill };
        refreshMachineCatalogBtn.Click += async (_, _) => await LoadMachineCatalogAsync(showErrors: true);
        form.Controls.Add(refreshMachineCatalogBtn, 2, r);
        r++;

        form.Controls.Add(MkLabel("Heartbeat interval (seconds)"), 0, r);
        _heartbeatUpDown = new NumericUpDown { Minimum = 10, Maximum = 3600, Value = 60, Width = 100, Dock = DockStyle.Left };
        form.Controls.Add(_heartbeatUpDown, 1, r);
        form.SetColumnSpan(_heartbeatUpDown, 2);
        r++;

        form.Controls.Add(MkLabel("Command check interval (seconds)"), 0, r);
        _commandCheckUpDown = new NumericUpDown { Minimum = 5, Maximum = 3600, Value = 30, Width = 100, Dock = DockStyle.Left };
        form.Controls.Add(_commandCheckUpDown, 1, r);
        form.SetColumnSpan(_commandCheckUpDown, 2);
        r++;

        _minimizeTrayCheckBox = new CheckBox { Text = "Minimize to system tray", AutoSize = true, Checked = true };
        form.Controls.Add(_minimizeTrayCheckBox, 0, r);
        form.SetColumnSpan(_minimizeTrayCheckBox, 3);
        r++;

        _startWithWindowsCheckBox = new CheckBox { Text = "Start with Windows", AutoSize = true };
        form.Controls.Add(_startWithWindowsCheckBox, 0, r);
        form.SetColumnSpan(_startWithWindowsCheckBox, 3);
        r++;

        var note = new Label
        {
            Text = "Agent note: choose a machine from MasterMaquinaDispensacion (Descripcion shown). The selected id is saved and used for heartbeats/commands.",
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8.5f),
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Padding = new Padding(0, 12, 0, 0)
        };
        form.Controls.Add(note, 0, r);
        form.SetColumnSpan(note, 3);

        scroll.Controls.Add(form);
        tab.Controls.Add(scroll);
        return tab;
    }

    private TabPage BuildDiagnosticsTab()
    {
        var tab = new TabPage("Diagnostics");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(12),
            AutoScroll = true
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var pcBox = new GroupBox { Text = "This PC", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var pcLayout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top };
        pcLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        pcLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int pr = 0;
        pcLayout.Controls.Add(MkLabel("Windows machine name"), 0, pr);
        pcLayout.Controls.Add(_diagMachineName = new Label { Text = "—", AutoSize = true }, 1, pr++);
        pcLayout.Controls.Add(MkLabel("Configured Machine ID (from Configuration tab)"), 0, pr);
        pcLayout.Controls.Add(_diagConfiguredId = new Label { Text = "—", AutoSize = true }, 1, pr++);
        pcLayout.Controls.Add(MkLabel("Local IPv4"), 0, pr);
        pcLayout.Controls.Add(_diagIp = new Label { Text = "—", AutoSize = true }, 1, pr++);
        pcLayout.Controls.Add(MkLabel("Network available"), 0, pr);
        pcLayout.Controls.Add(_diagNetwork = new Label { Text = "—", AutoSize = true }, 1, pr++);
        pcLayout.Controls.Add(MkLabel("Primary adapter"), 0, pr);
        pcLayout.Controls.Add(_diagNic = new Label { Text = "—", AutoSize = true }, 1, pr++);
        var btnRefreshPc = new Button { Text = "Refresh PC info" };
        UIThemeService.ApplyModernButtonStyle(btnRefreshPc);
        btnRefreshPc.Click += (_, _) => RefreshPcDiagnostics();
        pcLayout.Controls.Add(btnRefreshPc, 0, pr);
        pcLayout.SetColumnSpan(btnRefreshPc, 2);
        pcBox.Controls.Add(pcLayout);

        var dbBox = new GroupBox { Text = "Database (uses connection string from Configuration tab)", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var dbInner = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        dbInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dbInner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var dbFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        var btnTest = new Button { Text = "Test connection" };
        UIThemeService.ApplyModernButtonStyle(btnTest, true);
        btnTest.Click += async (_, _) => await RunTestConnectionAsync(btnTest);
        var btnCreateTables = new Button { Text = "Create agent tables" };
        UIThemeService.ApplyModernButtonStyle(btnCreateTables);
        btnCreateTables.Click += async (_, _) => await EnsureAgentTablesAsync(btnCreateTables);
        var btnList = new Button { Text = "Refresh machine list" };
        btnList.Click += async (_, _) => await RefreshMachineListAsync(btnList);
        dbFlow.Controls.AddRange(new Control[] { btnTest, btnCreateTables, btnList });
        _machinesListBox = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
        dbInner.Controls.Add(dbFlow, 0, 0);
        dbInner.Controls.Add(_machinesListBox, 0, 1);
        dbBox.Controls.Add(dbInner);

        panel.Controls.Add(pcBox, 0, 0);
        panel.Controls.Add(dbBox, 0, 1);
        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage BuildSendCommandTab()
    {
        var tab = new TabPage("Send command");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(12),
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));

        var sendBox = new GroupBox { Text = "Queue command in SQL (Status = Pending)", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        int r = 0;
        form.Controls.Add(MkLabel("Target MachineId"), 0, r);
        _sendTargetCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
        form.Controls.Add(_sendTargetCombo, 1, r);
        var btnRef = new Button { Text = "Refresh list", Dock = DockStyle.Fill };
        btnRef.Click += async (_, _) => await RefreshMachineListAsync(btnRef);
        form.Controls.Add(btnRef, 2, r++);

        form.Controls.Add(MkLabel("Command type"), 0, r);
        _sendCommandTypeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _sendCommandTypeCombo.Items.AddRange(new object[]
        {
            "RestartWindows", "RestartApp", "KillResidentOpenExe", "KillIfsdesktop",
            "SendLogFiles", "SendErrorFiles", "DownloadAndExecute"
        });
        if (_sendCommandTypeCombo.Items.Count > 0)
            _sendCommandTypeCombo.SelectedIndex = 0;
        form.Controls.Add(_sendCommandTypeCombo, 1, r);
        form.SetColumnSpan(_sendCommandTypeCombo, 2);
        r++;

        form.Controls.Add(MkLabel("Command data"), 0, r);
        _sendCommandDataTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Optional: delay seconds, Path|Date|Email, or URL|ExtractPath|Exe|true…"
        };
        form.Controls.Add(_sendCommandDataTextBox, 1, r);
        form.SetColumnSpan(_sendCommandDataTextBox, 2);
        r++;

        var btnSend = new Button { Text = "Send command" };
        UIThemeService.ApplyModernButtonStyle(btnSend, true);
        btnSend.Click += async (_, _) => await SendCommandAsync(btnSend);
        var btnProcessNow = new Button { Text = "Process pending now" };
        UIThemeService.ApplyModernButtonStyle(btnProcessNow);
        btnProcessNow.Click += async (_, _) => await ProcessPendingCommandsNowAsync(btnProcessNow);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Dock = DockStyle.Fill
        };
        actions.Controls.Add(btnSend);
        actions.Controls.Add(btnProcessNow);
        form.Controls.Add(actions, 0, r);
        form.SetColumnSpan(actions, 3);
        sendBox.Controls.Add(form);

        var logBox = new GroupBox { Text = "Activity log", Dock = DockStyle.Fill, Padding = new Padding(8) };
        _activityLogTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(250, 250, 252)
        };

        layout.Controls.Add(sendBox, 0, 0);
        logBox.Controls.Add(_activityLogTextBox);
        layout.Controls.Add(logBox, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildReferenceTab()
    {
        var tab = new TabPage("Command reference");
        var tb = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            Text = BuildCommandReferenceText(),
            WordWrap = false
        };
        tab.Controls.Add(tb);
        return tab;
    }

    private static string BuildCommandReferenceText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("RemoteMachineAgent — supported remote commands (CommandType values, case-insensitive when executed)");
        sb.AppendLine("");
        sb.AppendLine("1. RestartWindows — Restarts Windows. Command data: empty (30s delay), 0 (immediate), or seconds.");
        sb.AppendLine("2. RestartApp — Kills Ifsdesktop.exe; ResidentOpenExe can restart it. Data: usually empty.");
        sb.AppendLine("3. KillResidentOpenExe / KillResidentOpen — Kills residentopenexe.");
        sb.AppendLine("4. KillIfsdesktop / KillIfsdesktop.exe — Kills Ifsdesktop.exe.");
        sb.AppendLine(@"5. SendLogFiles — Compresses monthly logs and emails a ZIP. Data format: Path|Date|Email (date optional YYYY-MM-DD; default path C:\La Fabrica de Software\log). Requires SMTP in dbo.mastercontrol.");
        sb.AppendLine("6. SendErrorFiles — Lists/collects error files from agent folders. Data: usually empty.");
        sb.AppendLine("7. DownloadAndExecute / DownloadAndInstallUpdate — Data: URL|ExtractPath|ExecutableName|StartMainApp");
        sb.AppendLine("");
        sb.AppendLine("Email: the agent loads EmailServidor, EmailPuerto, EmailUseAutentication, EmailSSL, EmailUser, EmailPasswd from dbo.mastercontrol (TOP 1 row).");
        sb.AppendLine("");
        sb.AppendLine("Example INSERT:");
        sb.AppendLine("INSERT INTO Commands (MachineId, CommandType, CommandData, Status)");
        sb.AppendLine("VALUES ('TARGET_PC_NAME', 'RestartApp', '', 'Pending');");
        sb.AppendLine("");
        sb.AppendLine("Tables expected: Machines, Commands (as in RemoteMachineAgent DatabaseService).");
        return sb.ToString();
    }

    private void RefreshPcDiagnostics()
    {
        _diagMachineName.Text = Environment.MachineName;
        _diagConfiguredId.Text = string.IsNullOrWhiteSpace(GetConfiguredMachineId()) ? "(not set)" : GetConfiguredMachineId();
        _diagIp.Text = RemoteMachineAgentNetworkHelper.GetLocalIPAddress();
        _diagNetwork.Text = RemoteMachineAgentNetworkHelper.IsNetworkAvailable() ? "Yes" : "No";
        _diagNic.Text = RemoteMachineAgentNetworkHelper.GetPrimaryNetworkInterface();
    }

    private string GetConnectionStringOrEmpty()
    {
        return _connectionStringTextBox.Text?.Trim() ?? string.Empty;
    }

    private string GetConfiguredMachineId()
    {
        if (_machineIdCombo.SelectedItem is RemoteMachineAgentOperationsService.MachineCatalogItem selected)
            return selected.Id;

        return string.Empty;
    }

    private async Task LoadMachineCatalogAsync(bool showErrors, string? preferredMachineId = null)
    {
        var cs = GetConnectionStringOrEmpty();
        if (string.IsNullOrWhiteSpace(cs))
            return;

        var (items, error) = await _operations.GetMachineCatalogAsync(cs);
        if (!string.IsNullOrWhiteSpace(error))
        {
            if (showErrors)
            {
                MessageBox.Show(
                    $"Could not load machine catalog from MasterMaquinaDispensacion.\r\n\r\nDetails:\r\n{error}",
                    "RemoteMachineAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            return;
        }

        _machineCatalog = items;
        _machineIdCombo.BeginUpdate();
        try
        {
            _machineIdCombo.DataSource = null;
            _machineIdCombo.DisplayMember = nameof(RemoteMachineAgentOperationsService.MachineCatalogItem.Description);
            _machineIdCombo.ValueMember = nameof(RemoteMachineAgentOperationsService.MachineCatalogItem.Id);
            _machineIdCombo.DataSource = _machineCatalog;

            var desired = string.IsNullOrWhiteSpace(preferredMachineId) ? Environment.MachineName : preferredMachineId.Trim();
            var match = _machineCatalog.FirstOrDefault(x => string.Equals(x.Id, desired, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                _machineIdCombo.SelectedItem = match;
            else if (_machineCatalog.Count > 0)
                _machineIdCombo.SelectedIndex = 0;
        }
        finally
        {
            _machineIdCombo.EndUpdate();
        }

        RefreshPcDiagnostics();
    }

    private async Task RunTestConnectionAsync(Button? btn)
    {
        var cs = GetConnectionStringOrEmpty();
        if (string.IsNullOrEmpty(cs))
        {
            MessageBox.Show("Enter a database connection string on the Configuration tab.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (btn != null)
        {
            btn.Enabled = false;
            btn.Text = "Testing…";
        }
        try
        {
            var result = await _operations.TestConnectionDetailedAsync(cs);
            if (result.Success)
            {
                if (_machineIdCombo.Items.Count == 0)
                    await LoadMachineCatalogAsync(showErrors: true);

                var configuredMachineId = GetConfiguredMachineId();
                if (string.IsNullOrWhiteSpace(configuredMachineId))
                {
                    MessageBox.Show("Select a valid Machine ID from the list before testing.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _statusLabel.Text = "Select Machine ID first";
                    return;
                }
                var upsertResult = await _operations.UpsertMachineHeartbeatDetailedAsync(
                    cs,
                    configuredMachineId,
                    Environment.MachineName,
                    RemoteMachineAgentNetworkHelper.GetLocalIPAddress(),
                    isOnline: true);
                if (!upsertResult.Success)
                {
                    var details = string.IsNullOrWhiteSpace(upsertResult.ErrorMessage) ? "(no details)" : upsertResult.ErrorMessage;
                    MessageBox.Show(
                        $"Database connection is OK, but saving Machines heartbeat failed.\r\n\r\nDetails:\r\n{details}",
                        "RemoteMachineAgent",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    AppendLog($"Heartbeat upsert FAILED ({details})");
                    _statusLabel.Text = "DB connected but Machines sync failed";
                    return;
                }
                MessageBox.Show("Database connection successful. Required agent tables are ready.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AppendLog($"DB test: SUCCESS (schema ensured, machine upserted: {configuredMachineId})");
                _statusLabel.Text = "Database connection OK (tables ready, machine synced)";
            }
            else
            {
                var details = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "(no details)" : result.ErrorMessage;
                MessageBox.Show(
                    $"Could not open SQL connection.\r\n\r\nDetails:\r\n{details}",
                    "RemoteMachineAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                AppendLog($"DB test: FAILED ({details})");
                _statusLabel.Text = "Database connection failed";
            }
        }
        finally
        {
            if (btn != null)
            {
                btn.Enabled = true;
                btn.Text = "Test connection";
            }
        }
    }

    private async Task EnsureAgentTablesAsync(Button? btn)
    {
        var cs = GetConnectionStringOrEmpty();
        if (string.IsNullOrEmpty(cs))
        {
            MessageBox.Show("Enter a database connection string on the Configuration tab.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var prevText = btn?.Text;
        if (btn != null)
        {
            btn.Enabled = false;
            btn.Text = "Creating…";
        }

        try
        {
            var result = await _operations.EnsureAgentSchemaDetailedAsync(cs);
            if (result.Success)
            {
                AppendLog("Schema ensured: Machines and Commands are ready.");
                _statusLabel.Text = "Agent tables ensured";
                MessageBox.Show(
                    "Tables verified/created successfully:\r\n- Machines\r\n- Commands",
                    "RemoteMachineAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                var details = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "(no details)" : result.ErrorMessage;
                AppendLog("Schema ensure failed.");
                _statusLabel.Text = "Could not ensure tables";
                MessageBox.Show(
                    $"Could not create/verify tables.\r\n\r\nDetails:\r\n{details}\r\n\r\nCheck SQL permissions (CREATE TABLE / CREATE INDEX).",
                    "RemoteMachineAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (btn != null)
            {
                btn.Enabled = true;
                btn.Text = prevText ?? "Create agent tables";
            }
        }
    }

    private async Task RefreshMachineListAsync(Button? btn)
    {
        var cs = GetConnectionStringOrEmpty();
        if (string.IsNullOrEmpty(cs))
        {
            MessageBox.Show("Enter a database connection string on the Configuration tab.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var prevText = btn?.Text;
        if (btn != null)
        {
            btn.Enabled = false;
            btn.Text = "Loading…";
        }

        try
        {
            if (_machineIdCombo.Items.Count == 0)
                await LoadMachineCatalogAsync(showErrors: true);

            var configuredMachineId = GetConfiguredMachineId();
            if (string.IsNullOrWhiteSpace(configuredMachineId))
            {
                MessageBox.Show("Select a valid Machine ID from the list before refreshing.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _statusLabel.Text = "Select Machine ID first";
                return;
            }
            var upsertResult = await _operations.UpsertMachineHeartbeatDetailedAsync(
                cs,
                configuredMachineId,
                Environment.MachineName,
                RemoteMachineAgentNetworkHelper.GetLocalIPAddress(),
                isOnline: true);
            if (!upsertResult.Success)
            {
                var details = string.IsNullOrWhiteSpace(upsertResult.ErrorMessage) ? "(no details)" : upsertResult.ErrorMessage;
                AppendLog($"Heartbeat upsert FAILED ({details})");
            }

            var ids = await _operations.GetAllMachineIdsAsync(cs);
            _machinesListBox.Items.Clear();
            foreach (var id in ids)
                _machinesListBox.Items.Add(id);
            _sendTargetCombo.Items.Clear();
            foreach (var id in ids)
                _sendTargetCombo.Items.Add(id);
            var def = GetConfiguredMachineId();
            if (ids.Any(id => string.Equals(id, def, StringComparison.OrdinalIgnoreCase)))
                _sendTargetCombo.Text = def;
            else if (ids.Count > 0)
                _sendTargetCombo.Text = ids[0];
            else
                _sendTargetCombo.Text = string.Empty;
            AppendLog($"Machine list refreshed: {ids.Count} row(s). Local machine synced: {configuredMachineId}");
            _statusLabel.Text = $"Machines in DB: {ids.Count}";
        }
        finally
        {
            if (btn != null)
            {
                btn.Enabled = true;
                btn.Text = prevText ?? "Refresh";
            }
        }
    }

    private async Task SendCommandAsync(Button? btn)
    {
        var cs = GetConnectionStringOrEmpty();
        if (string.IsNullOrEmpty(cs))
        {
            MessageBox.Show("Enter a database connection string on the Configuration tab.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_sendTargetCombo.Text))
        {
            MessageBox.Show("Enter or select a target MachineId.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_sendCommandTypeCombo.SelectedItem == null)
        {
            MessageBox.Show("Select a command type.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var target = _sendTargetCombo.Text.Trim();
        var commandType = _sendCommandTypeCombo.SelectedItem.ToString() ?? string.Empty;
        var commandData = _sendCommandDataTextBox.Text.Trim();

        if (commandType is "RestartWindows" or "KillResidentOpenExe" or "KillIfsdesktop")
        {
            var confirm = MessageBox.Show(
                $"Send '{commandType}' to machine '{target}'? This may affect the target PC immediately.",
                "Confirm command",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;
        }

        if (btn != null)
        {
            btn.Enabled = false;
            btn.Text = "Sending…";
        }

        try
        {
            var id = await _operations.InsertCommandAsync(cs, target, commandType, commandData);
            if (id.HasValue)
            {
                AppendLog($"Command queued: Id={id.Value}, Type={commandType}, Target={target}");
                MessageBox.Show(
                    $"Command queued successfully.{Environment.NewLine}Id: {id.Value}{Environment.NewLine}Target: {target}{Environment.NewLine}Type: {commandType}",
                    "RemoteMachineAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _sendCommandDataTextBox.Clear();
                _statusLabel.Text = $"Command {id.Value} pending for {target}";
            }
            else
            {
                AppendLog($"Failed to insert command for {target}.");
                MessageBox.Show(
                    "Failed to insert command. Check SQL connectivity, table Commands, and permissions.",
                    "RemoteMachineAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}");
            MessageBox.Show(ex.Message, "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (btn != null)
            {
                btn.Enabled = true;
                btn.Text = "Send command";
            }
        }
    }

    private async Task ProcessPendingCommandsNowAsync(Button? btn)
    {
        var cs = GetConnectionStringOrEmpty();
        if (string.IsNullOrEmpty(cs))
        {
            MessageBox.Show("Enter a database connection string on the Configuration tab.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var previousText = btn?.Text;
        if (btn != null)
        {
            btn.Enabled = false;
            btn.Text = "Processing…";
        }

        try
        {
            var machineIdForCommands = GetConfiguredMachineId();
            if (string.IsNullOrWhiteSpace(machineIdForCommands))
            {
                MessageBox.Show("Select a Machine ID from the dropdown before processing commands.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int processed = await ProcessPendingCommandsInternalAsync(cs, showDialogs: false);
            if (processed == 0)
            {
                _statusLabel.Text = $"No pending commands for {machineIdForCommands}";
                MessageBox.Show(
                    $"No pending commands found for this PC ({machineIdForCommands}).",
                    "RemoteMachineAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                _statusLabel.Text = $"Processed {processed} command(s) for {machineIdForCommands}";
                MessageBox.Show(
                    $"Processed {processed} command(s) for this PC ({machineIdForCommands}).",
                    "RemoteMachineAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        finally
        {
            if (btn != null)
            {
                btn.Enabled = true;
                btn.Text = previousText ?? "Process pending now";
            }
        }
    }

    private async Task<int> ProcessPendingCommandsInternalAsync(string connectionString, bool showDialogs)
    {
        var machineIdForCommands = GetConfiguredMachineId();
        if (string.IsNullOrWhiteSpace(machineIdForCommands))
            return 0;

        var upsertResult = await _operations.UpsertMachineHeartbeatDetailedAsync(
            connectionString,
            machineIdForCommands,
            Environment.MachineName,
            RemoteMachineAgentNetworkHelper.GetLocalIPAddress(),
            isOnline: true);
        if (!upsertResult.Success)
            AppendLog($"Heartbeat upsert FAILED before command processing ({upsertResult.ErrorMessage})");

        int processed = 0;
        while (true)
        {
            var pending = await _operations.ClaimNextPendingCommandAsync(connectionString, machineIdForCommands);
            if (pending == null)
                break;

            try
            {
                var result = await ExecuteIncomingCommandAsync(pending.CommandType, pending.CommandData);
                await _operations.CompleteCommandAsync(connectionString, pending.Id, result);
                AppendLog($"Command executed: Id={pending.Id}, Type={pending.CommandType}, Result={result}");
            }
            catch (Exception ex)
            {
                await _operations.FailCommandAsync(connectionString, pending.Id, ex.Message);
                AppendLog($"Command failed: Id={pending.Id}, Type={pending.CommandType}, Error={ex.Message}");
            }

            processed++;
        }

        if (processed == 0 && showDialogs)
        {
            _statusLabel.Text = $"No pending commands for {machineIdForCommands}";
        }

        return processed;
    }

    private async Task<string> ExecuteIncomingCommandAsync(string commandType, string commandData)
    {
        var normalized = (commandType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "restartwindows" => ExecuteRestartWindows(commandData),
            "restartapp" => $"Killed Ifsdesktop processes: {KillProcessesByName("ifsdesktop")}",
            "killresidentopenexe" or "killresidentopen" => $"Killed ResidentOpenExe processes: {KillProcessesByName("residentopenexe")}",
            "killifsdesktop" or "killifsdesktop.exe" => $"Killed Ifsdesktop processes: {KillProcessesByName("ifsdesktop")}",
            "senderrorfiles" => ExecuteSendErrorFiles(commandData),
            "sendlogfiles" => await ExecuteSendLogFilesAsync(commandData),
            "downloadandexecute" or "downloadandinstallupdate" => await ExecuteDownloadAndExecuteAsync(commandData),
            _ => throw new NotSupportedException($"Unsupported command type: {commandType}")
        };
    }

    private static string ExecuteRestartWindows(string commandData)
    {
        var delaySeconds = 30;
        if (int.TryParse((commandData ?? string.Empty).Trim(), out var parsed))
            delaySeconds = Math.Max(0, parsed);

        var args = $"/r /t {delaySeconds} /f";
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = args,
            UseShellExecute = true,
            CreateNoWindow = true
        });
        return $"Scheduled Windows restart with delay {delaySeconds}s.";
    }

    private static int KillProcessesByName(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        var killed = 0;
        foreach (var process in processes)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
                killed++;
            }
            catch
            {
                // Ignore individual process failures.
            }
            finally
            {
                process.Dispose();
            }
        }

        return killed;
    }

    private static string ExecuteSendErrorFiles(string _)
    {
        var roots = new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Ifsdesktop"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ifsdesktop")
        };
        var matches = new List<string>();
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
                continue;
            try
            {
                matches.AddRange(Directory.GetFiles(root, "*error*", SearchOption.AllDirectories).Take(30));
            }
            catch
            {
                // Skip inaccessible folders.
            }
        }

        return matches.Count == 0
            ? "No error-like files found."
            : $"Found {matches.Count} error-like file(s).";
    }

    private async Task<string> ExecuteSendLogFilesAsync(string commandData)
    {
        var parts = (commandData ?? string.Empty).Split('|');
        var targetPath = parts.Length > 0 ? parts[0].Trim() : string.Empty;
        var datePart = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        var emailTo = parts.Length > 2 ? parts[2].Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(targetPath))
            targetPath = @"C:\La Fabrica de Software\log";
        if (!Directory.Exists(targetPath))
            throw new DirectoryNotFoundException($"Log path not found: {targetPath}");

        DateTime? requestedDate = ParseOptionalDate(datePart);
        var referenceDate = requestedDate ?? DateTime.Now;
        var monthFolder = ResolveMonthlyLogFolder(targetPath, referenceDate);
        if (string.IsNullOrWhiteSpace(monthFolder))
            return $"No month folder found in '{targetPath}' for {referenceDate:yyyy-MM}.";

        var zipPath = Path.Combine(
            Path.GetTempPath(),
            $"logs_{Environment.MachineName}_{referenceDate:yyyyMM}_{DateTime.Now:yyyyMMddHHmmss}.zip");
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        ZipFile.CreateFromDirectory(monthFolder, zipPath, CompressionLevel.Optimal, includeBaseDirectory: true);
        if (!File.Exists(zipPath))
            throw new InvalidOperationException($"Could not create zip from '{monthFolder}'.");

        if (string.IsNullOrWhiteSpace(emailTo))
            return $"Compressed current month log folder '{monthFolder}' to '{zipPath}'. Email not requested.";

        var connectionString = GetConnectionStringOrEmpty();
        if (string.IsNullOrWhiteSpace(connectionString))
            return $"Compressed '{monthFolder}' to '{zipPath}', but email was not sent because Database connection string is empty.";

        var (smtp, smtpError) = await _operations.GetMasterControlEmailSettingsAsync(connectionString);
        if (smtp == null)
            return $"Compressed '{monthFolder}' to '{zipPath}', but email settings from dbo.mastercontrol are not available. Details: {smtpError ?? "(no details)"}";

        using var message = new MailMessage(smtp.From, emailTo)
        {
            Subject = $"Monthly logs ZIP from {Environment.MachineName}",
            Body = $"Attached monthly logs folder: {Path.GetFileName(monthFolder)}"
        };
        message.Attachments.Add(new Attachment(zipPath));

        using var client = new SmtpClient(smtp.Server, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl
        };
        if (smtp.UseAuthentication)
        {
            client.Credentials = new NetworkCredential(smtp.User, smtp.Password);
            client.UseDefaultCredentials = false;
        }
        else
        {
            client.UseDefaultCredentials = true;
        }
        await client.SendMailAsync(message);
        try
        {
            File.Delete(zipPath);
        }
        catch
        {
            // Best-effort cleanup.
        }

        return $"Compressed and emailed '{Path.GetFileName(monthFolder)}' to {emailTo}.";
    }

    private static async Task<string> ExecuteDownloadAndExecuteAsync(string commandData)
    {
        var parts = (commandData ?? string.Empty).Split('|');
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            throw new InvalidOperationException("DownloadAndExecute requires URL in command data.");

        var url = parts[0].Trim();
        var extractPath = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1].Trim()
            : Path.Combine(AppContext.BaseDirectory, "Downloads");
        var executableName = parts.Length > 2 ? parts[2].Trim() : string.Empty;
        var startMain = parts.Length > 3 && bool.TryParse(parts[3], out var startFlag) && startFlag;

        Directory.CreateDirectory(extractPath);
        var rarPath = Path.Combine(extractPath, Path.GetFileName(new Uri(url).LocalPath));
        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
        {
            var bytes = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(rarPath, bytes);
        }

        var extractor = FindArchiveExtractorPath();
        if (extractor == null)
            throw new InvalidOperationException("7-Zip/WinRAR not found for extraction.");
        RunProcess(extractor, extractor.EndsWith("7z.exe", StringComparison.OrdinalIgnoreCase)
            ? $"x \"{rarPath}\" -o\"{extractPath}\" -y"
            : $"x \"{rarPath}\" \"{extractPath}\"");

        if (!string.IsNullOrWhiteSpace(executableName))
        {
            var exePath = Directory.GetFiles(extractPath, executableName, SearchOption.AllDirectories).FirstOrDefault();
            if (exePath == null)
                throw new FileNotFoundException($"Executable '{executableName}' not found after extraction.");
            RunProcess(exePath, string.Empty, waitForExit: false);
        }

        if (startMain)
        {
            var mainExe = FindIfsdesktopPath();
            if (mainExe != null)
                RunProcess(mainExe, string.Empty, waitForExit: false);
        }

        return $"Downloaded and extracted update from {url}.";
    }

    private static DateTime? ParseOptionalDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(value, new[] { "yyyy-MM-dd", "yyyyMMdd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        return null;
    }

    private static string? ResolveMonthlyLogFolder(string rootLogPath, DateTime referenceDate)
    {
        var directories = Directory.GetDirectories(rootLogPath, "*", SearchOption.TopDirectoryOnly);
        if (directories.Length == 0)
            return null;

        var yearPrefix = $"{referenceDate:yyyy}-";
        var sameYear = directories
            .Where(d => Path.GetFileName(d).StartsWith(yearPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var candidates = sameYear.Count > 0 ? sameYear : directories.ToList();

        var monthTokens = BuildMonthTokens(referenceDate.Month);
        foreach (var directory in candidates)
        {
            var normalizedName = NormalizeToken(Path.GetFileName(directory));
            if (monthTokens.Any(token => normalizedName.Contains(token)))
                return directory;
        }

        return candidates
            .OrderByDescending(d => Directory.GetLastWriteTime(d))
            .FirstOrDefault();
    }

    private static List<string> BuildMonthTokens(int month)
    {
        var cultures = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture, new CultureInfo("es-ES"), new CultureInfo("en-US") };
        return cultures
            .SelectMany(c => new[]
            {
                c.DateTimeFormat.GetMonthName(month),
                c.DateTimeFormat.GetAbbreviatedMonthName(month)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeToken)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(ch) || ch == '-'))
                sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string? FindArchiveExtractorPath()
    {
        var candidates = new[]
        {
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe",
            @"C:\Program Files\WinRAR\WinRAR.exe"
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindIfsdesktopPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Ifsdesktop.exe"),
            @"C:\Program Files\Ifsdesktop\Ifsdesktop.exe",
            @"C:\Program Files (x86)\Ifsdesktop\Ifsdesktop.exe"
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static void RunProcess(string fileName, string arguments, bool waitForExit = true)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException($"Could not start process: {fileName}");

        if (waitForExit)
            process.WaitForExit(5 * 60 * 1000);
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        if (_activityLogTextBox.IsHandleCreated && _activityLogTextBox.InvokeRequired)
        {
            _activityLogTextBox.Invoke(() =>
            {
                _activityLogTextBox.AppendText(line);
                _activityLogTextBox.SelectionStart = _activityLogTextBox.Text.Length;
                _activityLogTextBox.ScrollToCaret();
            });
        }
        else
        {
            _activityLogTextBox.AppendText(line);
            _activityLogTextBox.SelectionStart = _activityLogTextBox.Text.Length;
            _activityLogTextBox.ScrollToCaret();
        }
    }

    private static Label MkLabel(string text, bool bold = false) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = bold ? new Font("Segoe UI", 9f, FontStyle.Bold) : new Font("Segoe UI", 9f),
            ForeColor = UIThemeService.DarkBlue,
            Padding = new Padding(0, 8, 0, 0)
        };

    private void BrowseConfigFile()
    {
        using var d = new SaveFileDialog
        {
            Title = "Select or create RemoteMachineAgent config.json",
            Filter = "JSON|*.json|All files|*.*",
            FileName = "config.json"
        };
        if (File.Exists(_configPathTextBox.Text))
        {
            var full = Path.GetFullPath(_configPathTextBox.Text);
            d.InitialDirectory = Path.GetDirectoryName(full);
            d.FileName = Path.GetFileName(full);
        }
        if (d.ShowDialog(FindForm()) == DialogResult.OK)
            _configPathTextBox.Text = d.FileName;
    }

    private void OpenConfigFolder()
    {
        try
        {
            var path = _configPathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path)) return;

            string? dir;
            if (File.Exists(path))
                dir = Path.GetDirectoryName(Path.GetFullPath(path));
            else if (Directory.Exists(path))
                dir = Path.GetFullPath(path);
            else
                dir = Path.GetDirectoryName(Path.GetFullPath(path));

            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task LoadConfigAsync()
    {
        try
        {
            var path = _configPathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Specify the path to config.json.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var s = _configService.Load(path);
            _connectionStringTextBox.Text = s.DatabaseConnectionString;
            await LoadMachineCatalogAsync(showErrors: false, preferredMachineId: s.MachineId);
            _heartbeatUpDown.Value = Math.Clamp(s.HeartbeatIntervalSeconds, (int)_heartbeatUpDown.Minimum, (int)_heartbeatUpDown.Maximum);
            _commandCheckUpDown.Value = Math.Clamp(s.CommandCheckIntervalSeconds, (int)_commandCheckUpDown.Minimum, (int)_commandCheckUpDown.Maximum);
            _minimizeTrayCheckBox.Checked = s.MinimizeToTray;
            _startWithWindowsCheckBox.Checked = s.StartWithWindows;
            ApplyTimerIntervalsFromConfig();

            RefreshPcDiagnostics();
            _statusLabel.Text = File.Exists(path) ? $"Loaded: {path}" : $"No file yet — defaults shown. Save will create: {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load: {ex.Message}", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveConfig()
    {
        try
        {
            var path = _configPathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Specify the path to config.json.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var s = new RemoteMachineAgentSettings
            {
                DatabaseConnectionString = _connectionStringTextBox.Text,
                MachineId = GetConfiguredMachineId(),
                HeartbeatIntervalSeconds = (int)_heartbeatUpDown.Value,
                CommandCheckIntervalSeconds = (int)_commandCheckUpDown.Value,
                MinimizeToTray = _minimizeTrayCheckBox.Checked,
                StartWithWindows = _startWithWindowsCheckBox.Checked
            };

            if (string.IsNullOrWhiteSpace(s.MachineId))
            {
                MessageBox.Show("Select a Machine ID from the dropdown before saving.", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _configService.Save(path, s);
            ApplyTimerIntervalsFromConfig();
            RefreshPcDiagnostics();
            _statusLabel.Text = $"Saved: {path}";
            MessageBox.Show(
                "Configuration saved.\r\n\r\nRemoteMachineAgent loads %AppData%\\RemoteMachineAgent\\config.json by default.",
                "RemoteMachineAgent",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save: {ex.Message}", "RemoteMachineAgent", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _heartbeatTimer?.Stop();
            _heartbeatTimer?.Dispose();
            _commandTimer?.Stop();
            _commandTimer?.Dispose();
        }

        base.Dispose(disposing);
    }
}
