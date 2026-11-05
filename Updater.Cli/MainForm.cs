using System.IO;
using System.Drawing;
using System.Linq;
using Updater.Core.Models;
using Updater.Core.Services;
using Updater.Cli.Services;

namespace Updater.Cli;

public partial class MainForm : Form
{
    private readonly FormsLogger _logger;
    private readonly ArchiveExtractor _extractor;
    private readonly FileLockDetector _lockDetector;
    private readonly UpdateService _updateService;
    private readonly RollbackService _rollbackService;
    private readonly VersionLister _versionLister;

    private TabControl _tabControl = null!;
    private TextBox _logTextBox = null!;

    // Update Tab Controls
    private TextBox _updateSourceTextBox = null!;
    private TextBox _updateDestTextBox = null!;
    private TextBox _updateBackupRootTextBox = null!;
    private TextBox _updateConfigFileTextBox = null!;
    private TextBox _updateInnerFolderTextBox = null!;
    private CheckBox _updatePreserveConfigCheckBox = null!;
    private CheckBox _updateRequireConfigCheckBox = null!;
    private CheckBox _updateDryRunCheckBox = null!;
    private CheckBox _updateDeleteSourceArchiveCheckBox = null!;
    private TextBox _updateWinRarPathTextBox = null!;
    private Button _updateBrowseSourceButton = null!;
    private Button _updateBrowseDestButton = null!;
    private Button _updateBrowseBackupRootButton = null!;
    private Button _updateBrowseWinRarButton = null!;
    private Button _updateButton = null!;
    private Label _updateStatusLabel = null!;

    // Rollback Tab Controls
    private TextBox _rollbackDestTextBox = null!;
    private TextBox _rollbackBackupRootTextBox = null!;
    private RadioButton _rollbackLastRadioButton = null!;
    private RadioButton _rollbackToVersionRadioButton = null!;
    private ComboBox _rollbackVersionComboBox = null!;
    private CheckBox _rollbackKeepAppSettingsCheckBox = null!;
    private Button _rollbackBrowseDestButton = null!;
    private Button _rollbackBrowseBackupRootButton = null!;
    private Button _rollbackRefreshButton = null!;
    private Button _rollbackButton = null!;

    // List Versions Tab Controls
    private TextBox _listVersionsBackupRootTextBox = null!;
    private ListView _versionsListView = null!;
    private Button _listVersionsBrowseBackupRootButton = null!;
    private Button _listVersionsRefreshButton = null!;

    public MainForm()
    {
        InitializeComponent();
        
        _logTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9)
        };

        _logger = new FormsLogger(_logTextBox);
        _extractor = new ArchiveExtractor(_logger);
        _lockDetector = new FileLockDetector(_logger);
        _updateService = new UpdateService(_logger, _extractor, _lockDetector);
        _rollbackService = new RollbackService(_logger, _lockDetector);
        _versionLister = new VersionLister(_logger);

        InitializeUI();
        LoadDefaults();
        
        // Refresh lists after form is shown
        this.Load += (s, e) =>
        {
            RefreshVersionsList();
            RefreshRollbackVersions();
        };
    }

    private void InitializeComponent()
    {
        this.Text = "Updater - Installation Manager";
        this.Size = new Size(900, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(800, 600);
    }

    private void InitializeUI()
    {
        // Main layout: Split container with tabs on top and log on bottom
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 450,
            FixedPanel = FixedPanel.Panel2
        };

        // Tab Control
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        // Update Tab
        var updateTab = CreateUpdateTab();
        _tabControl.TabPages.Add(updateTab);

        // Rollback Tab
        var rollbackTab = CreateRollbackTab();
        _tabControl.TabPages.Add(rollbackTab);

        // List Versions Tab
        var listVersionsTab = CreateListVersionsTab();
        _tabControl.TabPages.Add(listVersionsTab);

        splitContainer.Panel1.Controls.Add(_tabControl);

        // Log Panel
        var logPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        var logLabel = new Label
        {
            Text = "Activity Log:",
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(5, 5, 0, 0)
        };
        logPanel.Controls.Add(_logTextBox);
        logPanel.Controls.Add(logLabel);
        logPanel.Controls.SetChildIndex(logLabel, 0);

        splitContainer.Panel2.Controls.Add(logPanel);

        this.Controls.Add(splitContainer);
    }

    private TabPage CreateUpdateTab()
    {
        var tab = new TabPage("Update");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 11,
            Padding = new Padding(10)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        int row = 0;

        // Information Panel for first-time users
        var infoPanel = new Panel
        {
            Height = 140,
            BackColor = Color.FromArgb(240, 248, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(15, 12, 15, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        var titleLabel = new Label
        {
            Text = "📋 How to Use This Application",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 60, 90),
            Location = new Point(0, 0),
            AutoSize = true
        };
        
        var infoLabel = new Label
        {
            Text = "This tool updates your application by deploying a new version from a package (ZIP, RAR, or folder).\n" +
                   "• Source: Package file or folder with the new version  •  Destination: Current installation folder\n" +
                   "• Backup Root: Storage location for backups  •  Automatic backup created before each update\n" +
                   "• Use 'Dry Run' to preview changes without applying them",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(60, 60, 60),
            Location = new Point(0, 25),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        infoPanel.Controls.Add(titleLabel);
        infoPanel.Controls.Add(infoLabel);
        
        panel.Controls.Add(infoPanel, 0, row);
        panel.SetColumnSpan(infoPanel, 3);
        row++;

        // Source
        panel.Controls.Add(new Label { Text = "Source:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateSourceTextBox = new TextBox { Dock = DockStyle.Fill, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        panel.Controls.Add(_updateSourceTextBox, 1, row);
        _updateBrowseSourceButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _updateBrowseSourceButton.Click += (s, e) => BrowseFileOrFolder(_updateSourceTextBox, "Select Source (ZIP, RAR, or Folder)");
        panel.Controls.Add(_updateBrowseSourceButton, 2, row++);

        // Destination
        panel.Controls.Add(new Label { Text = "Destination:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateDestTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_updateDestTextBox, 1, row);
        _updateBrowseDestButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _updateBrowseDestButton.Click += (s, e) => BrowseFolder(_updateDestTextBox, "Select Destination Folder");
        panel.Controls.Add(_updateBrowseDestButton, 2, row++);

        // Backup Root
        panel.Controls.Add(new Label { Text = "Backup Root:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateBackupRootTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_updateBackupRootTextBox, 1, row);
        _updateBrowseBackupRootButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _updateBrowseBackupRootButton.Click += (s, e) => BrowseFolder(_updateBackupRootTextBox, "Select Backup Root Folder");
        panel.Controls.Add(_updateBrowseBackupRootButton, 2, row++);

        // Config File
        panel.Controls.Add(new Label { Text = "Config File:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateConfigFileTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_updateConfigFileTextBox, 1, row);
        panel.Controls.Add(new Label(), 2, row++);

        // Hidden in UI by request (keep backing field for compatibility)
        _updateInnerFolderTextBox = new TextBox { Text = string.Empty };

        // Preserve Config
        _updatePreserveConfigCheckBox = new CheckBox { Text = "Preserve", Checked = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true };
        panel.Controls.Add(_updatePreserveConfigCheckBox, 0, row);
        var preserveLabel = new Label 
        { 
            Text = "Copy config file from current installation to new build before deployment", 
            Anchor = AnchorStyles.Left | AnchorStyles.Top, 
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 100),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
        };
        panel.Controls.Add(preserveLabel, 1, row);
        panel.SetColumnSpan(preserveLabel, 2);
        row++;

        // Require Config
        _updateRequireConfigCheckBox = new CheckBox { Text = "Require", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true };
        panel.Controls.Add(_updateRequireConfigCheckBox, 0, row);
        var requireLabel = new Label 
        { 
            Text = "Fail the update if config file is missing in destination", 
            Anchor = AnchorStyles.Left | AnchorStyles.Top, 
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 100),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
        };
        panel.Controls.Add(requireLabel, 1, row);
        panel.SetColumnSpan(requireLabel, 2);
        row++;

        // Hidden in UI by request (keep backing field for compatibility)
        _updateDryRunCheckBox = new CheckBox { Checked = false };

        // Delete source archive
        _updateDeleteSourceArchiveCheckBox = new CheckBox { Text = "Delete source archive", Checked = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true };
        panel.Controls.Add(_updateDeleteSourceArchiveCheckBox, 0, row);
        var deleteArchiveLabel = new Label
        {
            Text = "Delete the selected .rar/.zip source file after a successful update",
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 100),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
        };
        panel.Controls.Add(deleteArchiveLabel, 1, row);
        panel.SetColumnSpan(deleteArchiveLabel, 2);
        row++;

        // Hidden in UI by request (keep backing fields for compatibility)
        _updateWinRarPathTextBox = new TextBox { Text = string.Empty };
        _updateBrowseWinRarButton = new Button();

        // Update Button
        _updateButton = new Button { Text = "Start Update", Dock = DockStyle.Fill, Height = 40 };
        _updateButton.Click += UpdateButton_Click;
        panel.Controls.Add(_updateButton, 0, row);
        panel.SetColumnSpan(_updateButton, 3);
        row++;

        _updateStatusLabel = new Label
        {
            Text = string.Empty,
            ForeColor = Color.FromArgb(30, 60, 90),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Visible = false,
            Margin = new Padding(3, 10, 3, 0)
        };
        panel.Controls.Add(_updateStatusLabel, 0, row);
        panel.SetColumnSpan(_updateStatusLabel, 3);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateRollbackTab()
    {
        var tab = new TabPage("Rollback");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 6,
            Padding = new Padding(10)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        // Set row styles - make the options row taller
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Destination row
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Backup Root row
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 200)); // Rollback Options row - increased height
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Rollback Button row
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Remaining space
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0)); // Extra row

        int row = 0;

        // Destination
        panel.Controls.Add(new Label { Text = "Destination:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _rollbackDestTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_rollbackDestTextBox, 1, row);
        _rollbackBrowseDestButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _rollbackBrowseDestButton.Click += (s, e) => BrowseFolder(_rollbackDestTextBox, "Select Destination Folder");
        panel.Controls.Add(_rollbackBrowseDestButton, 2, row++);

        // Backup Root
        panel.Controls.Add(new Label { Text = "Backup Root:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _rollbackBackupRootTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_rollbackBackupRootTextBox, 1, row);
        _rollbackBrowseBackupRootButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _rollbackBrowseBackupRootButton.Click += (s, e) => BrowseFolder(_rollbackBackupRootTextBox, "Select Backup Root Folder");
        panel.Controls.Add(_rollbackBrowseBackupRootButton, 2, row++);

        // Rollback Options - Using GroupBox for better visual clarity with scrolling
        var rollbackGroupBox = new GroupBox 
        { 
            Text = "Select Rollback Option  (Secur folder is always preserved from current install)",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 20, 10, 10),
            ForeColor = Color.FromArgb(33, 33, 33), // Dark gray text
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            BackColor = Color.FromArgb(250, 250, 250) // Light gray background
        };
        
        // Scrollable panel to contain the options
        var scrollablePanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(250, 250, 250),
            Padding = new Padding(5)
        };
        
        var rollbackOptionsPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(8,0,8,0),
            BackColor = Color.FromArgb(250, 250, 250),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        rollbackOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rollbackOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rollbackOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rollbackOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rollbackOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        
        _rollbackLastRadioButton = new RadioButton 
        { 
            Text = "Rollback to Last Backup", 
            Checked = true, 
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.FromArgb(33, 33, 33), // Dark text for better readability
            BackColor = Color.FromArgb(250, 250, 250), // Light background
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Padding = new Padding(5,0,5,0),
            UseVisualStyleBackColor = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        _rollbackToVersionRadioButton = new RadioButton 
        { 
            Text = "Rollback to Specific Version:", 
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.FromArgb(33, 33, 33), // Dark text for better readability
            BackColor = Color.FromArgb(250, 250, 250), // Light background
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Padding = new Padding(5,0,5,0),
            UseVisualStyleBackColor = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        _rollbackVersionComboBox = new ComboBox 
        { 
            DropDownStyle = ComboBoxStyle.DropDownList, 
            Height = 28,
            Width = 600,  // Make ComboBox control wider to match dropdown
            Margin = new Padding(25, 8, 5, 10),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(33, 33, 33),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            DropDownWidth = 600  // Make dropdown wider to show full backup names
        };
        
        _rollbackLastRadioButton.CheckedChanged += (s, e) => _rollbackVersionComboBox.Enabled = !_rollbackLastRadioButton.Checked;
        _rollbackToVersionRadioButton.CheckedChanged += (s, e) => _rollbackVersionComboBox.Enabled = _rollbackToVersionRadioButton.Checked;
        _rollbackVersionComboBox.Enabled = false;

        _rollbackKeepAppSettingsCheckBox = new CheckBox
        {
            Text = "Keep current appsettings",
            Checked = true,
            AutoSize = true,
            ForeColor = Color.FromArgb(33, 33, 33),
            BackColor = Color.FromArgb(250, 250, 250),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Padding = new Padding(25, 6, 5, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var keepAppSettingsLabel = new Label
        {
            Text = "Preserve the current appsettings.json during rollback (do not use the one from the backup)",
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 100),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            Padding = new Padding(45, 0, 5, 8),
            BackColor = Color.FromArgb(250, 250, 250),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        rollbackOptionsPanel.Controls.Add(_rollbackLastRadioButton, 0, 0);
        rollbackOptionsPanel.Controls.Add(_rollbackToVersionRadioButton, 0, 1);
        rollbackOptionsPanel.Controls.Add(_rollbackVersionComboBox, 0, 2);
        rollbackOptionsPanel.Controls.Add(_rollbackKeepAppSettingsCheckBox, 0, 3);
        rollbackOptionsPanel.Controls.Add(keepAppSettingsLabel, 0, 4);
        
        // Add the TableLayoutPanel to the scrollable panel
        scrollablePanel.Controls.Add(rollbackOptionsPanel);
        
        // Add the scrollable panel to the GroupBox
        rollbackGroupBox.Controls.Add(scrollablePanel);
        
        panel.Controls.Add(new Label { Text = "Rollback To:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        panel.Controls.Add(rollbackGroupBox, 1, row);
        _rollbackRefreshButton = new Button { Text = "Refresh", Dock = DockStyle.Fill };
        _rollbackRefreshButton.Click += (s, e) => RefreshRollbackVersions();
        panel.Controls.Add(_rollbackRefreshButton, 2, row++);

        // Rollback Button
        _rollbackButton = new Button { Text = "Start Rollback", Dock = DockStyle.Fill, Height = 40 };
        _rollbackButton.Click += RollbackButton_Click;
        panel.Controls.Add(_rollbackButton, 0, row);
        panel.SetColumnSpan(_rollbackButton, 3);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateListVersionsTab()
    {
        var tab = new TabPage("List Versions");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(10)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        int row = 0;

        // Backup Root
        panel.Controls.Add(new Label { Text = "Backup Root:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _listVersionsBackupRootTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_listVersionsBackupRootTextBox, 1, row);
        _listVersionsBrowseBackupRootButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _listVersionsBrowseBackupRootButton.Click += (s, e) => BrowseFolder(_listVersionsBackupRootTextBox, "Select Backup Root Folder");
        panel.Controls.Add(_listVersionsBrowseBackupRootButton, 2, row++);

        // Refresh Button
        _listVersionsRefreshButton = new Button { Text = "Refresh List", Dock = DockStyle.Fill, Height = 35 };
        _listVersionsRefreshButton.Click += (s, e) => RefreshVersionsList();
        panel.Controls.Add(_listVersionsRefreshButton, 0, row);
        panel.SetColumnSpan(_listVersionsRefreshButton, 3);
        row++;

        // Versions List
        _versionsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _versionsListView.Columns.Add("Version", 400);
        _versionsListView.Columns.Add("Date", 200);
        _versionsListView.Columns.Add("Status", 100);
        panel.Controls.Add(_versionsListView, 0, row);
        panel.SetColumnSpan(_versionsListView, 3);

        tab.Controls.Add(panel);
        return tab;
    }

    private void LoadDefaults()
    {
        // Default paths (CTO / La Fabrica de Software deployment layout)
        const string defaultSourceRar = @"C:\La Fabrica de Software\net7.0-windows.rar";
        const string defaultDestination = @"C:\La Fabrica de Software\MaquinasDispensadorasNuevoSoftware";
        const string backupPath = @"C:\La Fabrica de Software\Secur";

        _updateSourceTextBox.Text = defaultSourceRar;
        _updateDestTextBox.Text = defaultDestination;
        _updateBackupRootTextBox.Text = backupPath;
        _updateConfigFileTextBox.Text = "appsettings.json";
        _updatePreserveConfigCheckBox.Checked = true;
        _updateRequireConfigCheckBox.Checked = false;
        _updateDryRunCheckBox.Checked = false;
        _updateDeleteSourceArchiveCheckBox.Checked = true;

        // Rollback targets the same installation folder as Update
        _rollbackDestTextBox.Text = defaultDestination;
        _rollbackBackupRootTextBox.Text = backupPath;
        _rollbackKeepAppSettingsCheckBox.Checked = true;

        _listVersionsBackupRootTextBox.Text = backupPath;

        // Ensure default folder structure exists on first run.
        EnsureDirectoryExists(Path.GetDirectoryName(defaultSourceRar));
        EnsureDirectoryExists(defaultDestination);
        EnsureDirectoryExists(backupPath);
    }

    private void EnsureDirectoryExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
            return;

        try
        {
            Directory.CreateDirectory(path);
            _logger.LogInfo($"Created default directory: {path}");
        }
        catch (Exception ex)
        {
            // Keep app usable even if folder creation is blocked by permissions.
            _logger.LogWarn($"Could not create default directory '{path}': {ex.Message}");
        }
    }

    private void BrowseFileOrFolder(TextBox textBox, string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "All Supported|*.zip;*.rar|ZIP Files|*.zip|RAR Files|*.rar|All Files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBox.Text = dialog.FileName;
        }
    }

    private void BrowseFolder(TextBox textBox, string description)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrEmpty(textBox.Text) && Directory.Exists(textBox.Text))
        {
            dialog.SelectedPath = Path.GetFullPath(textBox.Text);
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseFile(TextBox textBox, string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBox.Text = dialog.FileName;
        }
    }

    private void RefreshRollbackVersions()
    {
        _rollbackVersionComboBox.Items.Clear();
        
        if (!Directory.Exists(_rollbackBackupRootTextBox.Text))
        {
            return;
        }

        var backups = Directory.GetDirectories(_rollbackBackupRootTextBox.Text)
            .Select(d => new
            {
                Path = d,
                Name = Path.GetFileName(d),
                LastWriteTime = Directory.GetLastWriteTime(d)
            })
            .OrderByDescending(b => b.LastWriteTime)
            .ToList();

        int maxWidth = 400; // Minimum width
        foreach (var backup in backups)
        {
            _rollbackVersionComboBox.Items.Add(backup.Name);
            
            // Calculate width needed for this item
            using (var g = _rollbackVersionComboBox.CreateGraphics())
            {
                var textSize = g.MeasureString(backup.Name, _rollbackVersionComboBox.Font);
                int itemWidth = (int)textSize.Width + 50; // Add padding
                if (itemWidth > maxWidth)
                {
                    maxWidth = itemWidth;
                }
            }
        }
        
        // Set both ComboBox width and dropdown width to show full names (with a maximum)
        int calculatedWidth = Math.Min(maxWidth, 800);
        _rollbackVersionComboBox.Width = calculatedWidth;
        _rollbackVersionComboBox.DropDownWidth = calculatedWidth;
    }

    private void RefreshVersionsList()
    {
        _versionsListView.Items.Clear();

        if (!Directory.Exists(_listVersionsBackupRootTextBox.Text))
        {
            _logger.LogWarn($"Backup root not found: {_listVersionsBackupRootTextBox.Text}");
            return;
        }

        var backups = Directory.GetDirectories(_listVersionsBackupRootTextBox.Text)
            .Select(d => new
            {
                Path = d,
                Name = Path.GetFileName(d),
                LastWriteTime = Directory.GetLastWriteTime(d)
            })
            .OrderByDescending(b => b.LastWriteTime)
            .ToList();

        if (backups.Count == 0)
        {
            _logger.LogInfo("No backups found.");
            return;
        }

        var lastBackup = backups.First();
        foreach (var backup in backups)
        {
            var status = backup.Path == lastBackup.Path ? "(latest)" : "";
            var item = new ListViewItem(backup.Name);
            item.SubItems.Add(backup.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            item.SubItems.Add(status);
            _versionsListView.Items.Add(item);
        }

        _logger.LogInfo($"Found {backups.Count} backup(s). Latest: {lastBackup.Name}");
    }

    private async void UpdateButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _updateButton.Enabled = false;
            
            // CTO requirement defaults
            const string defaultDestination = @"C:\La Fabrica de Software\MaquinasDispensadorasNuevoSoftware";
            const string backupPath = @"C:\La Fabrica de Software\Secur";
            
            // Validate source file is selected
            if (string.IsNullOrWhiteSpace(_updateSourceTextBox.Text))
            {
                MessageBox.Show(
                    "Please select a .zip or .rar file to update from.\n\nClick 'Browse...' next to Source to select your update file.",
                    "Source Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _updateButton.Enabled = true;
                return;
            }

            // Validate source file exists
            var sourcePath = Path.GetFullPath(_updateSourceTextBox.Text);
            if (!File.Exists(sourcePath))
            {
                MessageBox.Show(
                    $"Source file does not exist:\n{sourcePath}\n\nPlease select a valid .zip or .rar file.",
                    "Source Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _updateButton.Enabled = true;
                return;
            }

            // Validate source is .zip or .rar file
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension != ".zip" && extension != ".rar")
            {
                MessageBox.Show(
                    $"Invalid file type. Please select a .zip or .rar file.\n\nSelected file: {sourcePath}",
                    "Invalid File Type",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _updateButton.Enabled = true;
                return;
            }

            // Use client default paths only when user has not set them (leave user's choices intact)
            if (string.IsNullOrWhiteSpace(_updateDestTextBox.Text))
                _updateDestTextBox.Text = defaultDestination;
            if (string.IsNullOrWhiteSpace(_updateBackupRootTextBox.Text))
                _updateBackupRootTextBox.Text = backupPath;

            var destPath = Path.GetFullPath(_updateDestTextBox.Text);
            var backupRootPath = Path.GetFullPath(_updateBackupRootTextBox.Text);
            
            _logger.LogInfo($"Source: {sourcePath}");
            _logger.LogInfo($"Destination: {destPath}");
            _logger.LogInfo($"Backup Root: {backupRootPath}");

            // Ensure destination directory exists (create if needed)
            if (!Directory.Exists(destPath))
            {
                try
                {
                    Directory.CreateDirectory(destPath);
                    _logger.LogInfo($"Created destination directory: {destPath}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Cannot create destination directory:\n{destPath}\n\nError: {ex.Message}",
                        "Directory Creation Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    _updateButton.Enabled = true;
                    return;
                }
            }

            // Ensure backup directory exists (create if needed)
            if (!Directory.Exists(backupRootPath))
            {
                try
                {
                    Directory.CreateDirectory(backupRootPath);
                    _logger.LogInfo($"Created backup directory: {backupRootPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarn($"Cannot create backup directory: {ex.Message}");
                    // Continue anyway - backup will fail later if needed
                }
            }

            _logger.LogInfo("Starting update process...");
            _updateStatusLabel.Text = "Updating...";
            _updateStatusLabel.Visible = true;
            _updateStatusLabel.Refresh();
            this.UseWaitCursor = true;

            var options = new UpdateOptions
            {
                Source = sourcePath,
                Destination = Path.GetFullPath(_updateDestTextBox.Text),
                BackupRoot = Path.GetFullPath(_updateBackupRootTextBox.Text),
                ConfigFile = _updateConfigFileTextBox.Text,
                InnerFolder = string.IsNullOrWhiteSpace(_updateInnerFolderTextBox.Text) ? null : _updateInnerFolderTextBox.Text,
                PreserveConfig = _updatePreserveConfigCheckBox.Checked,
                RequireConfig = _updateRequireConfigCheckBox.Checked,
                DryRun = _updateDryRunCheckBox.Checked,
                DeleteSourceArchive = _updateDeleteSourceArchiveCheckBox.Checked,
                WinRarPath = string.IsNullOrWhiteSpace(_updateWinRarPathTextBox.Text) ? null : _updateWinRarPathTextBox.Text
            };

            await Task.Run(() => _updateService.Update(options));

            MessageBox.Show("Update completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshVersionsList();
            RefreshRollbackVersions();
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (IOException ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}\n\nMake sure no files are locked or in use.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _updateButton.Enabled = true;
            this.UseWaitCursor = false;
            _updateStatusLabel.Visible = false;
            _updateStatusLabel.Text = string.Empty;
        }
    }

    private async void RollbackButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _rollbackButton.Enabled = false;
            
            // Validate inputs before proceeding
            if (string.IsNullOrWhiteSpace(_rollbackDestTextBox.Text))
            {
                MessageBox.Show("Please specify a destination folder.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _rollbackButton.Enabled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(_rollbackBackupRootTextBox.Text))
            {
                MessageBox.Show("Please specify a backup root folder.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _rollbackButton.Enabled = true;
                return;
            }

            // Check if backup root exists
            var backupRoot = Path.GetFullPath(_rollbackBackupRootTextBox.Text);
            if (!Directory.Exists(backupRoot))
            {
                var result = MessageBox.Show(
                    $"Backup root directory does not exist:\n{backupRoot}\n\n" +
                    "No backups are available. You need to perform at least one update before you can rollback.\n\n" +
                    "Would you like to create the backup directory?",
                    "Backup Directory Not Found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        Directory.CreateDirectory(backupRoot);
                        _logger.LogInfo($"Created backup directory: {backupRoot}");
                        MessageBox.Show("Backup directory created. However, there are no backups available yet. Please perform an update first.", 
                            "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to create backup directory: {ex.Message}");
                        MessageBox.Show($"Failed to create backup directory:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                _rollbackButton.Enabled = true;
                return;
            }

            // Check if there are any backups
            var backups = Directory.GetDirectories(backupRoot);
            if (backups.Length == 0)
            {
                MessageBox.Show(
                    $"No backups found in:\n{backupRoot}\n\n" +
                    "You need to perform at least one update before you can rollback.",
                    "No Backups Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _rollbackButton.Enabled = true;
                return;
            }

            var options = new RollbackOptions
            {
                Destination = Path.GetFullPath(_rollbackDestTextBox.Text),
                BackupRoot = backupRoot,
                UseLast = _rollbackLastRadioButton.Checked,
                KeepCurrentAppSettings = _rollbackKeepAppSettingsCheckBox.Checked,
                ToVersion = _rollbackToVersionRadioButton.Checked && _rollbackVersionComboBox.SelectedItem != null
                    ? _rollbackVersionComboBox.SelectedItem.ToString()
                    : null
            };

            if (_rollbackToVersionRadioButton.Checked && string.IsNullOrEmpty(options.ToVersion))
            {
                MessageBox.Show("Please select a version to rollback to.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _rollbackButton.Enabled = true;
                return;
            }

            _logger.LogInfo("Starting rollback process...");

            await Task.Run(() => _rollbackService.Rollback(options));

            MessageBox.Show("Rollback completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshVersionsList();
            RefreshRollbackVersions();
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (IOException ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}\n\nMake sure no files are locked or in use.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _rollbackButton.Enabled = true;
        }
    }
}

