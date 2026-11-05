using System.Diagnostics;
using AppSettingsEditor.Models;
using AppSettingsEditor.Services;
using System.Drawing;

namespace AppSettingsEditor.Controls;

internal class ResidentOpenExeControl : UserControl
{
    private readonly ResidentOpenExeConfigService _configService = new();
    private readonly OpenExeWatchdogService? _editorWatchdog;
    private readonly Action? _afterEditorWatchdogToggle;
    private Button _editorWatchdogToggleBtn = null!;

    private TextBox _configPathTextBox = null!;
    private TextBox _exeNameTextBox = null!;
    private TextBox _exeDirTextBox = null!;
    private TextBox _exe2NameTextBox = null!;
    private TextBox _exe2DirTextBox = null!;
    private NumericUpDown _intervalUpDown = null!;
    private TextBox _trayNameTextBox = null!;
    private CheckBox _secondExeCheckBox = null!;
    private Panel _secondExePanel = null!;
    private CheckBox _closeExeCheckBox = null!;
    private NumericUpDown _heartbeatUpDown = null!;
    private TextBox _logDirTextBox = null!;
    private Label _statusLabel = null!;

    /// <param name="editorWatchdog">When set, the screen can pause/resume the editor’s background auto-open of the monitored exe.</param>
    /// <param name="afterEditorWatchdogToggle">Called after start/stop so the host can refresh tray UI.</param>
    public ResidentOpenExeControl(OpenExeWatchdogService? editorWatchdog = null, Action? afterEditorWatchdogToggle = null)
    {
        _editorWatchdog = editorWatchdog;
        _afterEditorWatchdogToggle = afterEditorWatchdogToggle;
        Dock = DockStyle.Fill;
        BackColor = UIThemeService.LightGray;
        BuildUi();
    }

    private void BuildUi()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16)
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var title = new Label
        {
            Text = "ResidentOpenExe — keep a monitored executable running. If it closes, ResidentOpenExe restarts it automatically.",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = UIThemeService.DarkBlue,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        main.Controls.Add(title, 0, 0);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var form = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Padding = new Padding(0, 0, 0, 8),
            MaximumSize = new Size(920, 0)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        int r = 0;
        form.Controls.Add(MkLabel("Config.xml path"), 0, r);
        _configPathTextBox = new TextBox { Dock = DockStyle.Fill };
        form.Controls.Add(_configPathTextBox, 1, r);
        var browseCfg = new Button { Text = "Browse…", Dock = DockStyle.Fill };
        browseCfg.Click += (_, _) => BrowseConfigFile();
        form.Controls.Add(browseCfg, 2, r++);
        _configPathTextBox.Text = Path.Combine(@"C:\La Fabrica de Software", "ResidentOpenExe", "Config.xml");

        var loadBtn = new Button { Text = "Load configuration" };
        UIThemeService.ApplyModernButtonStyle(loadBtn);
        loadBtn.Click += (_, _) => LoadConfig();
        var saveBtn = new Button { Text = "Save configuration" };
        UIThemeService.ApplyModernButtonStyle(saveBtn, isPrimary: true);
        saveBtn.Click += (_, _) => SaveConfig();
        var verifyHelpBtn = new Button { Text = "How to verify" };
        verifyHelpBtn.Click += (_, _) => ShowVerificationGuide();
        var folderBtn = new Button { Text = "Open folder" };
        folderBtn.Click += (_, _) => OpenConfigFolder();
        _editorWatchdogToggleBtn = new Button { Text = "Stop auto-open", AutoSize = true };
        UIThemeService.ApplyModernButtonStyle(_editorWatchdogToggleBtn);
        _editorWatchdogToggleBtn.Click += (_, _) => ToggleEditorWatchdog();
        if (_editorWatchdog == null)
            _editorWatchdogToggleBtn.Visible = false;
        var loadSavePanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        loadSavePanel.Controls.AddRange(new Control[] { loadBtn, saveBtn, verifyHelpBtn, folderBtn, _editorWatchdogToggleBtn });
        form.Controls.Add(loadSavePanel, 0, r);
        form.SetColumnSpan(loadSavePanel, 3);
        r++;

        form.Controls.Add(MkLabel("Executable file name", bold: true), 0, r);
        r++;
        var exeHint = MkHint("(exact file name, e.g. LFSMachines.Desktop.exe)");
        form.Controls.Add(exeHint, 0, r);
        form.SetColumnSpan(exeHint, 3);
        r++;
        _exeNameTextBox = new TextBox { Dock = DockStyle.Fill };
        form.Controls.Add(_exeNameTextBox, 1, r);
        var browseExe = new Button { Text = "Pick .exe…", Dock = DockStyle.Fill };
        browseExe.Click += (_, _) => BrowsePrimaryExe();
        form.Controls.Add(browseExe, 2, r++);

        form.Controls.Add(MkLabel("Executable folder"), 0, r);
        _exeDirTextBox = new TextBox { Dock = DockStyle.Fill };
        form.Controls.Add(_exeDirTextBox, 1, r);
        var browseDir = new Button { Text = "Folder…", Dock = DockStyle.Fill };
        browseDir.Click += (_, _) => BrowseFolder(_exeDirTextBox);
        form.Controls.Add(browseDir, 2, r++);

        form.Controls.Add(MkLabel("Check interval (seconds)"), 0, r);
        _intervalUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 3600,
            Value = 2,
            Dock = DockStyle.Left,
            Width = 80
        };
        form.Controls.Add(_intervalUpDown, 1, r);
        form.SetColumnSpan(_intervalUpDown, 2);
        r++;

        form.Controls.Add(MkLabel("Tray / window title (optional)"), 0, r);
        _trayNameTextBox = new TextBox { Dock = DockStyle.Fill };
        form.Controls.Add(_trayNameTextBox, 1, r);
        form.SetColumnSpan(_trayNameTextBox, 2);
        r++;

        _secondExeCheckBox = new CheckBox { Text = "Configure a second executable (optional)", AutoSize = true };
        _secondExeCheckBox.CheckedChanged += (_, _) => _secondExePanel.Enabled = _secondExeCheckBox.Checked;
        form.Controls.Add(_secondExeCheckBox, 0, r);
        form.SetColumnSpan(_secondExeCheckBox, 3);
        r++;

        _secondExePanel = new Panel { AutoSize = true, Height = 72 };
        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            AutoSize = true
        };
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        inner.Controls.Add(MkLabel("Second file name"), 0, 0);
        _exe2NameTextBox = new TextBox { Dock = DockStyle.Fill };
        inner.Controls.Add(_exe2NameTextBox, 1, 0);
        var browse2 = new Button { Text = "Pick .exe…", Dock = DockStyle.Fill };
        browse2.Click += (_, _) => BrowseSecondExe();
        inner.Controls.Add(browse2, 2, 0);
        inner.Controls.Add(MkLabel("Second folder"), 0, 1);
        _exe2DirTextBox = new TextBox { Dock = DockStyle.Fill };
        inner.Controls.Add(_exe2DirTextBox, 1, 1);
        var browse2d = new Button { Text = "Folder…", Dock = DockStyle.Fill };
        browse2d.Click += (_, _) => BrowseFolder(_exe2DirTextBox);
        inner.Controls.Add(browse2d, 2, 1);
        _secondExePanel.Controls.Add(inner);
        form.Controls.Add(_secondExePanel, 0, r);
        form.SetColumnSpan(_secondExePanel, 3);
        r++;

        _closeExeCheckBox = new CheckBox
        {
            Text = "Include CloseExe section (advanced: heartbeat / max idle time — requires Funcionamiento.txt in the monitored app setup)",
            AutoSize = true
        };
        form.Controls.Add(_closeExeCheckBox, 0, r);
        form.SetColumnSpan(_closeExeCheckBox, 3);
        r++;

        form.Controls.Add(MkLabel("Heartbeat timeout (ms)"), 0, r);
        _heartbeatUpDown = new NumericUpDown { Minimum = 1000, Maximum = int.MaxValue, Value = 120000, Width = 120, Dock = DockStyle.Left };
        form.Controls.Add(_heartbeatUpDown, 1, r);
        form.SetColumnSpan(_heartbeatUpDown, 2);
        r++;

        form.Controls.Add(MkLabel("Log directory (optional)"), 0, r);
        _logDirTextBox = new TextBox { Dock = DockStyle.Fill };
        form.Controls.Add(_logDirTextBox, 1, r);
        var browseLog = new Button { Text = "Folder…", Dock = DockStyle.Fill };
        browseLog.Click += (_, _) => BrowseFolder(_logDirTextBox);
        form.Controls.Add(browseLog, 2, r++);

        scroll.Controls.Add(form);
        main.Controls.Add(scroll, 0, 1);

        _statusLabel = new Label { Dock = DockStyle.Fill, ForeColor = Color.DimGray, Text = "" };
        main.Controls.Add(_statusLabel, 0, 2);

        Controls.Add(main);
        _secondExePanel.Enabled = false;
        UpdateEditorWatchdogToggleUi();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
            UpdateEditorWatchdogToggleUi();
    }

    private void ToggleEditorWatchdog()
    {
        if (_editorWatchdog == null)
            return;

        if (_editorWatchdog.IsRunning)
            _editorWatchdog.Stop();
        else
            _editorWatchdog.Start();

        UpdateEditorWatchdogToggleUi();
        _afterEditorWatchdogToggle?.Invoke();
        _statusLabel.Text = _editorWatchdog.IsRunning
            ? "Editor auto-open is on (monitored exe will be started when missing)."
            : "Editor auto-open is off (monitored exe will not be started by AppSettingsEditor until you resume).";
    }

    private void UpdateEditorWatchdogToggleUi()
    {
        if (_editorWatchdog == null || !_editorWatchdogToggleBtn.Visible)
            return;

        _editorWatchdogToggleBtn.Text = _editorWatchdog.IsRunning ? "Stop auto-open" : "Resume auto-open";
    }

    /// <summary>Keep the toggle label in sync when the host changes watchdog state (e.g. tray menu).</summary>
    public void SyncEditorWatchdogToggleFromService() => UpdateEditorWatchdogToggleUi();

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

    private static Label MkHint(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8.5f),
            Padding = new Padding(0, 0, 0, 4)
        };

    private void BrowseConfigFile()
    {
        using var d = new OpenFileDialog
        {
            Title = "Select ResidentOpenExe Config.xml",
            Filter = "Config.xml|Config.xml|XML files|*.xml|All files|*.*"
        };
        if (File.Exists(_configPathTextBox.Text))
            d.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(_configPathTextBox.Text));
        if (d.ShowDialog(FindForm()) == DialogResult.OK)
            _configPathTextBox.Text = d.FileName;
    }

    private void BrowseFolder(TextBox target)
    {
        using var d = new FolderBrowserDialog { Description = "Select folder" };
        if (Directory.Exists(target.Text))
            d.SelectedPath = Path.GetFullPath(target.Text);
        if (d.ShowDialog(FindForm()) == DialogResult.OK)
            target.Text = d.SelectedPath;
    }

    private void BrowsePrimaryExe()
    {
        using var d = new OpenFileDialog
        {
            Title = "Select executable to keep running",
            Filter = "Executable|*.exe|All files|*.*"
        };
        if (Directory.Exists(_exeDirTextBox.Text))
            d.InitialDirectory = Path.GetFullPath(_exeDirTextBox.Text);
        if (d.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _exeDirTextBox.Text = Path.GetDirectoryName(d.FileName) ?? "";
            _exeNameTextBox.Text = Path.GetFileName(d.FileName);
        }
    }

    private void BrowseSecondExe()
    {
        using var d = new OpenFileDialog
        {
            Title = "Select second executable",
            Filter = "Executable|*.exe|All files|*.*"
        };
        if (Directory.Exists(_exe2DirTextBox.Text))
            d.InitialDirectory = Path.GetFullPath(_exe2DirTextBox.Text);
        if (d.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _exe2DirTextBox.Text = Path.GetDirectoryName(d.FileName) ?? "";
            _exe2NameTextBox.Text = Path.GetFileName(d.FileName);
            _secondExeCheckBox.Checked = true;
        }
    }

    private void OpenConfigFolder()
    {
        try
        {
            var path = _configPathTextBox.Text.Trim();
            var dir = File.Exists(path) ? Path.GetDirectoryName(Path.GetFullPath(path)) : Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            else if (!string.IsNullOrEmpty(path))
            {
                var d = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(d))
                    Process.Start(new ProcessStartInfo { FileName = d, UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ResidentOpenExe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LoadConfig()
    {
        try
        {
            var path = _configPathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Specify the path to Config.xml.", "ResidentOpenExe", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var s = _configService.Load(path);
            _exeNameTextBox.Text = s.ExecutableFileName;
            _exeDirTextBox.Text = s.ExecutableDirectory.TrimEnd('\\', '/');
            _exe2NameTextBox.Text = s.Executable2FileName ?? "";
            _exe2DirTextBox.Text = (s.Executable2Directory ?? "").TrimEnd('\\', '/');
            _secondExeCheckBox.Checked = !string.IsNullOrWhiteSpace(s.Executable2FileName) &&
                                         !string.IsNullOrWhiteSpace(s.Executable2Directory);
            _intervalUpDown.Value = Math.Clamp(s.CheckIntervalSeconds, (int)_intervalUpDown.Minimum, (int)_intervalUpDown.Maximum);
            _trayNameTextBox.Text = s.TrayDisplayName ?? "";
            _closeExeCheckBox.Checked = s.IncludeCloseExeSection;
            _heartbeatUpDown.Value = Math.Clamp(s.HeartbeatTimeoutMs, (int)_heartbeatUpDown.Minimum, (int)_heartbeatUpDown.Maximum);
            _logDirTextBox.Text = s.LogDirectory ?? "";

            _statusLabel.Text = File.Exists(path) ? $"Loaded: {path}" : $"No file yet — defaults shown. Save will create: {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load configuration: {ex.Message}", "ResidentOpenExe", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveConfig()
    {
        try
        {
            var path = _configPathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Specify the path to Config.xml.", "ResidentOpenExe", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_exeNameTextBox.Text) || string.IsNullOrWhiteSpace(_exeDirTextBox.Text))
            {
                MessageBox.Show("Executable file name and folder are required.", "ResidentOpenExe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var s = new ResidentOpenExeSettings
            {
                ExecutableFileName = _exeNameTextBox.Text.Trim(),
                ExecutableDirectory = _exeDirTextBox.Text.Trim(),
                CheckIntervalSeconds = (int)_intervalUpDown.Value,
                TrayDisplayName = string.IsNullOrWhiteSpace(_trayNameTextBox.Text) ? null : _trayNameTextBox.Text.Trim(),
                IncludeCloseExeSection = _closeExeCheckBox.Checked,
                HeartbeatTimeoutMs = (int)_heartbeatUpDown.Value,
                LogDirectory = string.IsNullOrWhiteSpace(_logDirTextBox.Text) ? null : _logDirTextBox.Text.Trim()
            };

            if (s.RunOnceThenExit)
            {
                MessageBox.Show(
                    "CTO requirement needs continuous watchdog mode.\r\n\r\nDisable 'Run monitored app once then exit ResidentOpenExe (SoloUnaVez)' before saving.",
                    "ResidentOpenExe",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var fullExePath = Path.Combine(s.ExecutableDirectory, s.ExecutableFileName);
            if (!File.Exists(fullExePath))
            {
                var keepSaving = MessageBox.Show(
                    $"Monitored executable was not found at:\r\n{fullExePath}\r\n\r\nSave anyway?",
                    "ResidentOpenExe",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (keepSaving != DialogResult.Yes)
                    return;
            }

            if (_secondExeCheckBox.Checked &&
                !string.IsNullOrWhiteSpace(_exe2NameTextBox.Text) &&
                !string.IsNullOrWhiteSpace(_exe2DirTextBox.Text))
            {
                s.Executable2FileName = _exe2NameTextBox.Text.Trim();
                s.Executable2Directory = _exe2DirTextBox.Text.Trim();
            }

            _configService.Save(path, s);
            _statusLabel.Text = $"Saved: {path}";
            MessageBox.Show(
                "Configuration saved.\r\n\r\nInstall or run ResidentOpenExe from the same folder as Config.xml so it picks up your settings. The resident process will restart the monitored executable whenever it is not running.\r\n\r\nTip: use 'How to verify' for CTO validation steps.",
                "ResidentOpenExe",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save: {ex.Message}", "ResidentOpenExe", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowVerificationGuide()
    {
        MessageBox.Show(
            "CTO verification steps (ResidentOpenExe watchdog)\r\n\r\n" +
            "1) Save config with valid NombreExe + RutaExe.\r\n" +
            "2) Keep 'SoloUnaVez' disabled (continuous mode).\r\n" +
            "3) Start ResidentOpenExe runtime using this Config.xml.\r\n" +
            "4) Confirm target app is running.\r\n" +
            "5) Close target app manually; wait TimerEspera seconds.\r\n" +
            "6) Verify app starts again automatically.\r\n" +
            "7) Repeat using Task Manager End Task (3 times).\r\n" +
            "8) Record relaunch time and evidence (screenshots/video).\r\n\r\n" +
            "Pass criteria: every close event results in automatic relaunch.",
            "ResidentOpenExe - Verification guide",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
