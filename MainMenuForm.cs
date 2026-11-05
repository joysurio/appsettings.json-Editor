using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AppSettingsEditor.Controls;
using AppSettingsEditor.Forms;
using AppSettingsEditor.Services;
using Updater.Cli;

namespace AppSettingsEditor;

internal class MainMenuForm : Form
{
    private TabControl _tabControl = null!;
    private ToolStrip _iconToolStrip = null!;
    private ToolStripButton _appSettingTabButton = null!;
    private ToolStripButton _updaterTabButton = null!;
    private ToolStripButton _checklistTabButton = null!;
    private ToolStripButton _analyzeLogsTabButton = null!;
    private ToolStripButton _xmlToJsonTabButton = null!;
    private ToolStripButton _remoteMachineAgentTabButton = null!;

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _trayMenu;
    private bool _exitRequested;
    private readonly OpenExeWatchdogService _openExeWatchdog = new();

    public MainMenuForm()
    {
        InitializeComponent();
        InitializeTray();

        // Start hidden: watchdog runs without blocking on authentication.
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        Load += OnFirstLoadBackgroundStart;
    }

    private void OnFirstLoadBackgroundStart(object? sender, EventArgs e)
    {
        Load -= OnFirstLoadBackgroundStart;
        Hide();
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "AppSettingsEditor — background. Open to change settings.";
        }
    }

    private void InitializeComponent()
    {
        this.Text = "Main Menu";
        this.Size = new Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(245, 245, 250);
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.MinimumSize = new Size(800, 600);

        // Main layout: ToolStrip at top, TabControl below, StatusStrip at bottom
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); // Icon toolbar
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Tab content
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Status bar

        // Icon toolbar at the top
        _iconToolStrip = new ToolStrip
        {
            Dock = DockStyle.Fill,
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };

        // AppSetting icon button
        _appSettingTabButton = new ToolStripButton
        {
            Text = "AppSetting",
            Image = GetSettingsIcon(),
            ImageAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = false,
            Size = new Size(100, 60),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 152, 219),
            BackColor = Color.White
        };
        _appSettingTabButton.Click += (s, e) => _tabControl.SelectedIndex = 0;
        _iconToolStrip.Items.Add(_appSettingTabButton);

        _iconToolStrip.Items.Add(new ToolStripSeparator());

        // Updater icon button
        _updaterTabButton = new ToolStripButton
        {
            Text = "Updater",
            Image = GetZipIcon(),
            ImageAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = false,
            Size = new Size(100, 60),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(46, 204, 113),
            BackColor = Color.White
        };
        _updaterTabButton.Click += (s, e) => _tabControl.SelectedIndex = 1;
        _iconToolStrip.Items.Add(_updaterTabButton);

        _iconToolStrip.Items.Add(new ToolStripSeparator());

        // Checklist icon button
        _checklistTabButton = new ToolStripButton
        {
            Text = "PC Checklist",
            Image = GetChecklistIcon(),
            ImageAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = false,
            Size = new Size(100, 60),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(155, 89, 182),
            BackColor = Color.White
        };
        _checklistTabButton.Click += (s, e) => _tabControl.SelectedIndex = 2;
        _iconToolStrip.Items.Add(_checklistTabButton);

        _iconToolStrip.Items.Add(new ToolStripSeparator());

        // Analyze_logs icon button
        _analyzeLogsTabButton = new ToolStripButton
        {
            Text = "Analyze_logs",
            Image = GetAnalyzeLogsIcon(),
            ImageAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = false,
            Size = new Size(100, 60),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 152, 0),
            BackColor = Color.White
        };
        _analyzeLogsTabButton.Click += (s, e) => _tabControl.SelectedIndex = 3;
        _iconToolStrip.Items.Add(_analyzeLogsTabButton);

        _iconToolStrip.Items.Add(new ToolStripSeparator());

        // XML to JSON Converter icon button
        _xmlToJsonTabButton = new ToolStripButton
        {
            Text = "XML Converter",
            Image = GetXmlToJsonIcon(),
            ImageAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = false,
            Size = new Size(100, 60),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 73, 94),
            BackColor = Color.White
        };
        _xmlToJsonTabButton.Click += (s, e) => _tabControl.SelectedIndex = 4;
        _iconToolStrip.Items.Add(_xmlToJsonTabButton);

        _iconToolStrip.Items.Add(new ToolStripSeparator());

        _remoteMachineAgentTabButton = new ToolStripButton
        {
            Text = "Remote Agent",
            Image = GetRemoteMachineAgentIcon(),
            ImageAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            AutoSize = false,
            Size = new Size(100, 60),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(41, 128, 185),
            BackColor = Color.White
        };
        _remoteMachineAgentTabButton.Click += (s, e) => _tabControl.SelectedIndex = 5;
        _iconToolStrip.Items.Add(_remoteMachineAgentTabButton);

        // TabControl
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.FlatButtons,
            SizeMode = TabSizeMode.FillToRight,
            ItemSize = new Size(0, 1),
            Padding = new Point(0, 0)
        };

        // AppSetting Tab
        var appSettingTab = new TabPage("AppSetting");
        var appSettingControl = new AppSettingsEditorControl();
        appSettingControl.Dock = DockStyle.Fill;
        appSettingTab.Controls.Add(appSettingControl);
        _tabControl.TabPages.Add(appSettingTab);

        // Updater Tab
        var updaterTab = new TabPage("Updater");
        var updaterForm = new MainForm();
        updaterForm.TopLevel = false;
        updaterForm.FormBorderStyle = FormBorderStyle.None;
        updaterForm.Dock = DockStyle.Fill;
        updaterTab.Controls.Add(updaterForm);
        updaterForm.Show();
        _tabControl.TabPages.Add(updaterTab);

        // Checklist Tab
        var checklistTab = new TabPage("PC Checklist");
        var checklistForm = new ChecklistForm();
        checklistForm.TopLevel = false;
        checklistForm.FormBorderStyle = FormBorderStyle.None;
        checklistForm.Dock = DockStyle.Fill;
        checklistTab.Controls.Add(checklistForm);
        checklistForm.Show();
        _tabControl.TabPages.Add(checklistTab);

        // Analyze_logs Tab
        var analyzeLogsTab = new TabPage("Analyze_logs");
        var logAnalyzerForm = new LogAnalyzerForm();
        logAnalyzerForm.TopLevel = false;
        logAnalyzerForm.FormBorderStyle = FormBorderStyle.None;
        logAnalyzerForm.Dock = DockStyle.Fill;
        analyzeLogsTab.Controls.Add(logAnalyzerForm);
        logAnalyzerForm.Show();
        _tabControl.TabPages.Add(analyzeLogsTab);

        // XML to JSON Converter Tab
        var xmlToJsonTab = new TabPage("XML Converter");
        var xmlToJsonForm = new Forms.XmlToJsonConverterForm();
        xmlToJsonForm.TopLevel = false;
        xmlToJsonForm.FormBorderStyle = FormBorderStyle.None;
        xmlToJsonForm.Dock = DockStyle.Fill;
        xmlToJsonTab.Controls.Add(xmlToJsonForm);
        xmlToJsonForm.Show();
        _tabControl.TabPages.Add(xmlToJsonTab);

        var remoteAgentTab = new TabPage("RemoteMachineAgent");
        var remoteAgentControl = new RemoteMachineAgentControl();
        remoteAgentControl.Dock = DockStyle.Fill;
        remoteAgentTab.Controls.Add(remoteAgentControl);
        _tabControl.TabPages.Add(remoteAgentTab);

        // Update icon button appearance when tab changes
        _tabControl.SelectedIndexChanged += (s, e) => UpdateIconButtonStates();

        // Status bar
        var statusStrip = new StatusStrip();
        var statusLabel = new ToolStripStatusLabel("Ready");
        statusStrip.Items.Add(statusLabel);

        mainPanel.Controls.Add(_iconToolStrip, 0, 0);
        mainPanel.Controls.Add(_tabControl, 0, 1);
        mainPanel.Controls.Add(statusStrip, 0, 2);

        this.Controls.Add(mainPanel);

        // Set initial tab
        _tabControl.SelectedIndex = 0;
        UpdateIconButtonStates();
    }

    private void InitializeTray()
    {
        _exitRequested = false;

        _trayMenu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("Show");
        var exitItem = new ToolStripMenuItem("Exit");

        showItem.Text = "Open…";
        showItem.Click += (_, _) => ShowFromTray();
        exitItem.Click += (_, _) =>
        {
            using (var gate = new PasswordGateForm(PasswordGatePurpose.ConfirmExit))
            {
                if (gate.ShowDialog() != DialogResult.OK)
                    return;
            }

            _exitRequested = true;
            if (_notifyIcon != null)
                _notifyIcon.Visible = false;
            Close();
        };

        _trayMenu.Items.Add(showItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AppSettingsEditor",
            Visible = false,
            ContextMenuStrip = _trayMenu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();

        this.Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                if (_notifyIcon != null)
                    _notifyIcon.Visible = true;
            }
        };
    }

    private void ShowFromTray()
    {
        using (var gate = new PasswordGateForm(PasswordGatePurpose.UnlockSettings))
        {
            if (gate.ShowDialog() != DialogResult.OK)
                return;
        }

        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        if (_notifyIcon != null)
            _notifyIcon.Visible = false;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exitRequested)
        {
            // Do not terminate when the user closes/minimizes; keep running in tray.
            e.Cancel = true;
            Hide();
            if (_notifyIcon != null)
                _notifyIcon.Visible = true;
            return;
        }

        _openExeWatchdog.Stop();
        _openExeWatchdog.Dispose();
        _notifyIcon?.Dispose();
        _trayMenu?.Dispose();
        base.OnFormClosing(e);
    }

    private void UpdateIconButtonStates()
    {
        // Update button appearance based on selected tab
        _appSettingTabButton.BackColor = _tabControl.SelectedIndex == 0 
            ? Color.FromArgb(230, 240, 250) 
            : Color.White;
        _updaterTabButton.BackColor = _tabControl.SelectedIndex == 1 
            ? Color.FromArgb(230, 250, 240) 
            : Color.White;
        _checklistTabButton.BackColor = _tabControl.SelectedIndex == 2 
            ? Color.FromArgb(250, 240, 250) 
            : Color.White;
        _analyzeLogsTabButton.BackColor = _tabControl.SelectedIndex == 3 
            ? Color.FromArgb(255, 245, 230) 
            : Color.White;
        _xmlToJsonTabButton.BackColor = _tabControl.SelectedIndex == 4 
            ? Color.FromArgb(240, 240, 245) 
            : Color.White;
        _remoteMachineAgentTabButton.BackColor = _tabControl.SelectedIndex == 5
            ? Color.FromArgb(232, 240, 250)
            : Color.White;
    }


    private Bitmap? GetSettingsIcon()
    {
        try
        {
            // Create a settings/gear icon with blue color
            var bitmap = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Draw gear icon in blue
                var color = Color.FromArgb(52, 152, 219);
                var pen = new Pen(color, 3);
                var brush = new SolidBrush(color);
                
                // Simple gear representation
                var rect = new Rectangle(6, 6, 36, 36);
                g.DrawEllipse(pen, rect);
                g.FillEllipse(brush, new Rectangle(16, 16, 16, 16));
                
                // Draw gear teeth
                for (int i = 0; i < 8; i++)
                {
                    var angle = i * 45;
                    var rad = angle * Math.PI / 180;
                    var x1 = 24 + (float)(Math.Cos(rad) * 15);
                    var y1 = 24 + (float)(Math.Sin(rad) * 15);
                    var x2 = 24 + (float)(Math.Cos(rad) * 20);
                    var y2 = 24 + (float)(Math.Sin(rad) * 20);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private Bitmap? GetZipIcon()
    {
        try
        {
            // Create a zip/archive icon with green color
            var bitmap = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Draw folder/zip icon in green
                var color = Color.FromArgb(46, 204, 113);
                var pen = new Pen(color, 2);
                var brush = new SolidBrush(color);
                
                // Draw folder shape
                var points = new PointF[]
                {
                    new PointF(8, 14),
                    new PointF(14, 8),
                    new PointF(40, 8),
                    new PointF(40, 40),
                    new PointF(8, 40)
                };
                g.FillPolygon(brush, points);
                g.DrawPolygon(pen, points);
                
                // Draw zip lines
                var zipPen = new Pen(color, 2.5f);
                for (int i = 0; i < 4; i++)
                {
                    g.DrawLine(zipPen, 14 + i * 3, 16, 34 + i * 3, 16);
                }
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }


    private Bitmap? GetChecklistIcon()
    {
        try
        {
            // Create a checklist/checkmark icon with purple color
            var bitmap = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                var color = Color.FromArgb(155, 89, 182);
                var pen = new Pen(color, 3);
                var brush = new SolidBrush(color);
                
                // Draw checklist box
                var rect = new Rectangle(8, 8, 32, 32);
                g.DrawRectangle(pen, rect);
                
                // Draw checkmarks
                var checkPen = new Pen(color, 3);
                checkPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                checkPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                
                // First checkmark
                g.DrawLine(checkPen, 14, 20, 18, 24);
                g.DrawLine(checkPen, 18, 24, 26, 14);
                
                // Second checkmark (slightly offset)
                g.DrawLine(checkPen, 14, 28, 18, 32);
                g.DrawLine(checkPen, 18, 32, 26, 22);
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private Bitmap? GetAnalyzeLogsIcon()
    {
        try
        {
            // Create a log/document analysis icon with red color
            var bitmap = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                var color = Color.FromArgb(255, 152, 0);
                var pen = new Pen(color, 2.5f);
                var brush = new SolidBrush(color);
                
                // Draw document/page shape
                var docRect = new Rectangle(10, 8, 28, 36);
                g.FillRectangle(brush, docRect);
                g.DrawRectangle(pen, docRect);
                
                // Draw lines representing text/logs
                var linePen = new Pen(Color.White, 2f);
                for (int i = 0; i < 5; i++)
                {
                    int y = 14 + i * 5;
                    int width = 20 - (i % 2) * 3; // Vary line length
                    g.DrawLine(linePen, 14, y, 14 + width, y);
                }
                
                // Draw magnifying glass overlay (analysis symbol)
                var magnifyPen = new Pen(color, 2.5f);
                var magnifyBrush = new SolidBrush(Color.FromArgb(240, 240, 240));
                // Circle
                g.DrawEllipse(magnifyPen, 24, 20, 16, 16);
                // Handle
                g.DrawLine(magnifyPen, 36, 28, 42, 34);
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private Bitmap? GetXmlToJsonIcon()
    {
        try
        {
            // Create an XML to JSON conversion icon - two documents with arrow
            var bitmap = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                var color = Color.FromArgb(52, 73, 94); // Dark blue-gray
                var pen = new Pen(color, 2);
                var brush = new SolidBrush(color);
                
                // Left document (XML) - angled
                var xmlDoc = new RectangleF(4, 8, 18, 24);
                g.FillRectangle(brush, xmlDoc);
                g.DrawRectangle(pen, xmlDoc.X, xmlDoc.Y, xmlDoc.Width, xmlDoc.Height);
                
                // Draw XML brackets on left doc
                var xmlPen = new Pen(Color.White, 2);
                g.DrawLine(xmlPen, 8, 14, 6, 16);
                g.DrawLine(xmlPen, 6, 16, 8, 18);
                g.DrawLine(xmlPen, 10, 14, 12, 16);
                g.DrawLine(xmlPen, 12, 16, 10, 18);
                
                // Arrow pointing right
                var arrowPen = new Pen(color, 3);
                arrowPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                arrowPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawLine(arrowPen, 24, 20, 32, 20);
                // Arrow head
                var arrowPoints = new PointF[]
                {
                    new PointF(32, 20),
                    new PointF(28, 16),
                    new PointF(28, 24)
                };
                g.FillPolygon(brush, arrowPoints);
                
                // Right document (JSON) - angled
                var jsonDoc = new RectangleF(26, 8, 18, 24);
                g.FillRectangle(brush, jsonDoc);
                g.DrawRectangle(pen, jsonDoc.X, jsonDoc.Y, jsonDoc.Width, jsonDoc.Height);
                
                // Draw JSON braces on right doc
                var jsonPen = new Pen(Color.White, 2);
                // Left brace
                g.DrawArc(jsonPen, 28, 14, 4, 4, 90, 180);
                g.DrawLine(jsonPen, 30, 14, 30, 18);
                g.DrawArc(jsonPen, 28, 18, 4, 4, 270, 180);
                // Right brace
                g.DrawArc(jsonPen, 38, 14, 4, 4, 90, -180);
                g.DrawLine(jsonPen, 40, 14, 40, 18);
                g.DrawArc(jsonPen, 38, 18, 4, 4, 270, -180);
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private Bitmap? GetRemoteMachineAgentIcon()
    {
        try
        {
            var bitmap = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var pc = Color.FromArgb(41, 128, 185);
                using var pen = new Pen(pc, 2f);
                using var brush = new SolidBrush(pc);

                g.FillRectangle(brush, 10, 14, 28, 22);
                g.DrawRectangle(pen, 10, 14, 28, 22);
                g.FillRectangle(new SolidBrush(Color.White), 14, 18, 20, 12);
                g.DrawLine(pen, 24, 36, 24, 42);
                g.DrawLine(pen, 18, 42, 30, 42);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private Bitmap? GetResidentOpenExeIcon()
    {
        try
        {
            var bitmap = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var color = Color.FromArgb(22, 160, 133);
                using var pen = new Pen(color, 2.5f);
                using var brush = new SolidBrush(color);

                g.DrawEllipse(pen, 8, 8, 32, 32);
                g.DrawArc(pen, 14, 14, 20, 20, 45, 270);
                g.FillPolygon(brush, new[]
                {
                    new PointF(32, 24),
                    new PointF(40, 20),
                    new PointF(40, 28)
                });
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

