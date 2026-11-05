using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppSettingsEditor.Models;
using AppSettingsEditor.Services;
using Newtonsoft.Json;

namespace AppSettingsEditor;

internal partial class ChecklistForm : Form
{
    private readonly ChecklistService _checklistService;
    private ChecklistSession _session;
    private DataGridView _dataGridView = null!;
    private TextBox _customerTextBox = null!;
    private TextBox _systemTextBox = null!;
    private DateTimePicker _installationDatePicker = null!;
    private TextBox _anyDeskIdTextBox = null!;
    private TextBox _rustDeskIdTextBox = null!;
    private TextBox _performedByTextBox = null!;
    private TextBox _reviewedByTextBox = null!;
    private Button _runAllChecksButton = null!;
    private Button _recheckSelectedButton = null!;
    private Button _exportReportButton = null!;
    private Button _saveSessionButton = null!;
    private Button _loadSessionButton = null!;
    private ProgressBar _progressBar = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private Dictionary<string, ChecklistItem> _itemsDict = new();
    private Dictionary<string, ChecklistResult> _resultsDict = new();

    public ChecklistForm()
    {
        _checklistService = new ChecklistService();
        _session = new ChecklistSession();
        InitializeComponent();
        LoadChecklistItems();
    }

    private void InitializeComponent()
    {
        this.Text = "PC Readiness Checklist";
        this.Size = new Size(1400, 900);
        this.MinimumSize = new Size(1000, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(245, 245, 250);
        this.WindowState = FormWindowState.Normal;
        this.FormBorderStyle = FormBorderStyle.Sizable;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(10)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));  // Header
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // DataGridView (will be constrained)
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // Button panel (increased from 50)
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // Status bar

        // Header panel: no scroll to prevent excessive scrolling when viewing checklist items (e.g. Windows UI/Debloat)
        var headerScrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = false };
        var headerPanel = CreateHeaderPanel();
        headerScrollPanel.Controls.Add(headerPanel);
        mainPanel.Controls.Add(headerScrollPanel, 0, 0);

        // DataGridView - handles its own scrolling
        _dataGridView = CreateDataGridView();
        mainPanel.Controls.Add(_dataGridView, 0, 1);

        // Button panel
        var buttonPanel = CreateButtonPanel();
        mainPanel.Controls.Add(buttonPanel, 0, 2);

        // Status bar
        var statusStrip = new StatusStrip { Dock = DockStyle.Fill };
        _statusLabel = new ToolStripStatusLabel("Ready");
        statusStrip.Items.Add(_statusLabel);
        mainPanel.Controls.Add(statusStrip, 0, 3);

        this.Controls.Add(mainPanel);
        
        // Handle form resize to adjust DataGridView columns
        this.Resize += ChecklistForm_Resize;
        this.Load += ChecklistForm_Load;
        this.Layout += ChecklistForm_Layout;
    }

    private void ChecklistForm_Layout(object? sender, LayoutEventArgs e)
    {
        // Ensure button panel is always visible by forcing layout recalculation
        if (_dataGridView != null && _dataGridView.Parent != null)
        {
            _dataGridView.Parent.Invalidate();
        }
    }

    private void ChecklistForm_Load(object? sender, EventArgs e)
    {
        AdjustDataGridViewColumns();
    }

    private void ChecklistForm_Resize(object? sender, EventArgs e)
    {
        if (_dataGridView != null && _dataGridView.Columns.Count > 0)
        {
            AdjustDataGridViewColumns();
        }
    }

    private void AdjustDataGridViewColumns()
    {
        if (_dataGridView == null || _dataGridView.Columns.Count == 0) return;

        var totalWidth = _dataGridView.ClientSize.Width;
        var scrollBarWidth = _dataGridView.Rows.Count * _dataGridView.RowTemplate.Height > _dataGridView.ClientSize.Height 
            ? SystemInformation.VerticalScrollBarWidth 
            : 0;
        
        var availableWidth = totalWidth - scrollBarWidth - 20; // 20px padding

        // Fixed widths
        var categoryWidth = 150;
        var statusWidth = 100;
        var fixWidth = 80;
        
        // Calculate fill columns width
        var fillColumnsWidth = availableWidth - categoryWidth - statusWidth - fixWidth;
        var fillColumnCount = 3; // Title, Evidence, ManualInstructions
        
        var fillColumnWidth = Math.Max(150, fillColumnsWidth / fillColumnCount);

        var categoryCol = _dataGridView.Columns["Category"];
        var titleCol = _dataGridView.Columns["Title"];
        var statusCol = _dataGridView.Columns["Status"];
        var evidenceCol = _dataGridView.Columns["Evidence"];
        var fixCol = _dataGridView.Columns["Fix"];
        var manualCol = _dataGridView.Columns["ManualInstructions"];

        if (categoryCol != null) categoryCol.Width = categoryWidth;
        if (titleCol != null) titleCol.Width = fillColumnWidth;
        if (statusCol != null) statusCol.Width = statusWidth;
        if (evidenceCol != null) evidenceCol.Width = fillColumnWidth;
        if (fixCol != null) fixCol.Width = fixWidth;
        if (manualCol != null) manualCol.Width = fillColumnWidth;
    }

    private Panel CreateHeaderPanel()
    {
        var panel = new Panel 
        { 
            Dock = DockStyle.Fill, 
            Padding = new Padding(10),
            AutoSize = false
        };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            ColumnCount = 4,
            Padding = new Padding(5)
        };

        for (int i = 0; i < 6; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        // Row 0 - Labels
        table.Controls.Add(new Label { Text = "Customer:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        table.Controls.Add(new Label { Text = "System:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
        table.Controls.Add(new Label { Text = "Installation Date:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 0);
        table.Controls.Add(new Label { Text = "MAC Address:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 3, 0);

        // Row 1 - Controls
        _customerTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 2, 5, 2) };
        table.Controls.Add(_customerTextBox, 0, 1);
        _systemTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 2, 5, 2) };
        table.Controls.Add(_systemTextBox, 1, 1);
        _installationDatePicker = new DateTimePicker { Dock = DockStyle.Fill, Margin = new Padding(5, 2, 5, 2), Format = DateTimePickerFormat.Short };
        table.Controls.Add(_installationDatePicker, 2, 1);
        var macLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(52, 152, 219), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        macLabel.Text = _checklistService.GetMacAddress() ?? "Not detected";
        table.Controls.Add(macLabel, 3, 1);

        // Row 2 - Labels
        table.Controls.Add(new Label { Text = "AnyDesk ID:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        table.Controls.Add(new Label { Text = "RustDesk ID:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 1, 2);
        table.Controls.Add(new Label { Text = "Performed By:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 2);
        table.Controls.Add(new Label { Text = "Reviewed By:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 3, 2);

        // Row 3 - Controls
        _anyDeskIdTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 2, 5, 2) };
        table.Controls.Add(_anyDeskIdTextBox, 0, 3);
        _rustDeskIdTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 2, 5, 2) };
        table.Controls.Add(_rustDeskIdTextBox, 1, 3);
        _performedByTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 2, 5, 2) };
        table.Controls.Add(_performedByTextBox, 2, 3);
        _reviewedByTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 2, 5, 2) };
        table.Controls.Add(_reviewedByTextBox, 3, 3);

        panel.Controls.Add(table);
        return panel;
    }

    private DataGridView CreateDataGridView()
    {
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            ScrollBars = ScrollBars.Both,
            AllowUserToResizeColumns = true,
            AllowUserToResizeRows = false,
        };

        dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,  // Header text alignment
            WrapMode = DataGridViewTriState.True  // Enable text wrapping in headers
        };
        
        // Increase header height to accommodate multi-line text
        dgv.ColumnHeadersHeight = 50;  // Increased from default ~23px to 50px
        
        // Category column - fixed width
        var categoryCol = new DataGridViewTextBoxColumn 
        { 
            Name = "Category", 
            HeaderText = "Category", 
            Width = 150, 
            ReadOnly = true,
            MinimumWidth = 120
        };
        dgv.Columns.Add(categoryCol);

        // Title column - fill remaining space
        var titleCol = new DataGridViewTextBoxColumn 
        { 
            Name = "Title", 
            HeaderText = "Task / Requirement", 
            Width = 300, 
            ReadOnly = true,
            MinimumWidth = 200,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        dgv.Columns.Add(titleCol);

        // Status column - fixed width
        var statusCol = new DataGridViewTextBoxColumn 
        { 
            Name = "Status", 
            HeaderText = "Status", 
            Width = 100, 
            ReadOnly = true,
            MinimumWidth = 80
        };
        dgv.Columns.Add(statusCol);

        // Evidence column - fill remaining space
        var evidenceCol = new DataGridViewTextBoxColumn 
        { 
            Name = "Evidence", 
            HeaderText = "Evidence / Details", 
            Width = 250, 
            ReadOnly = true,
            MinimumWidth = 150,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        dgv.Columns.Add(evidenceCol);

        // Fix button column - fixed width
        var fixCol = new DataGridViewButtonColumn 
        { 
            Name = "Fix", 
            HeaderText = "Action", 
            Width = 80, 
            Text = "Fix", 
            UseColumnTextForButtonValue = true,
            MinimumWidth = 70
        };
        dgv.Columns.Add(fixCol);

        // Manual Instructions column - fill remaining space with word wrap
        var manualCol = new DataGridViewTextBoxColumn 
        { 
            Name = "ManualInstructions", 
            HeaderText = "Manual Guidance", 
            Width = 300, 
            ReadOnly = true,
            MinimumWidth = 150,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        dgv.Columns.Add(manualCol);
        
        // Enable word wrap for ManualInstructions column
        manualCol.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        dgv.CellFormatting += DataGridView_CellFormatting;
        dgv.CellClick += DataGridView_CellClick;
        // dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 240, 255);
        dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        
        // Enable word wrap for better text display
        dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        
        // Set auto-resize for rows to accommodate wrapped text
        dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        dgv.DefaultCellStyle.Padding = new Padding(3);
        
        // Increase row height for better readability
        dgv.RowTemplate.Height = 40;

        return dgv;
    }

    private Panel CreateButtonPanel()
    {
        var panel = new Panel 
        { 
            Dock = DockStyle.Fill, 
            Padding = new Padding(10, 5, 10, 5),  // Reduced vertical padding
            AutoScroll = false,  // No scroll needed - buttons should fit
            MinimumSize = new Size(0, 60)  // Ensure minimum height
        };
        var flowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = false,
            Padding = new Padding(0)
        };

        _runAllChecksButton = new Button
        {
            Text = "Run All Checks",
            Size = new Size(120, 35),
            BackColor = Color.FromArgb(52, 152, 219),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 2, 3, 2)  // Reduced margin
        };
        _runAllChecksButton.FlatAppearance.BorderSize = 0;
        _runAllChecksButton.Click += RunAllChecksButton_Click;
        flowPanel.Controls.Add(_runAllChecksButton);

        _recheckSelectedButton = new Button
        {
            Text = "Re-check Selected",
            Size = new Size(120, 35),
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 2, 3, 2)  // Reduced margin
        };
        _recheckSelectedButton.FlatAppearance.BorderSize = 0;
        _recheckSelectedButton.Click += RecheckSelectedButton_Click;
        flowPanel.Controls.Add(_recheckSelectedButton);

        _exportReportButton = new Button
        {
            Text = "Export Report",
            Size = new Size(120, 35),
            BackColor = Color.FromArgb(155, 89, 182),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 2, 3, 2)  // Reduced margin
        };
        _exportReportButton.FlatAppearance.BorderSize = 0;
        _exportReportButton.Click += ExportReportButton_Click;
        flowPanel.Controls.Add(_exportReportButton);

        _saveSessionButton = new Button
        {
            Text = "Save Session",
            Size = new Size(120, 35),
            BackColor = Color.FromArgb(241, 196, 15),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 2, 3, 2)  // Reduced margin
        };
        _saveSessionButton.FlatAppearance.BorderSize = 0;
        _saveSessionButton.Click += SaveSessionButton_Click;
        flowPanel.Controls.Add(_saveSessionButton);

        _loadSessionButton = new Button
        {
            Text = "Load Session",
            Size = new Size(120, 35),
            BackColor = Color.FromArgb(230, 126, 34),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 2, 3, 2)  // Reduced margin
        };
        _loadSessionButton.FlatAppearance.BorderSize = 0;
        _loadSessionButton.Click += LoadSessionButton_Click;
        flowPanel.Controls.Add(_loadSessionButton);

        _progressBar = new ProgressBar
        {
            Size = new Size(200, 25),
            Style = ProgressBarStyle.Continuous,
            Margin = new Padding(3, 2, 3, 2),  // Reduced margin
        };
        flowPanel.Controls.Add(_progressBar);

        panel.Controls.Add(flowPanel);
        return panel;
    }

    private void LoadChecklistItems()
    {
        var items = _checklistService.GetChecklistItems();
        _itemsDict = items.ToDictionary(i => i.Id, i => i);

        _dataGridView.Rows.Clear();
        foreach (var item in items)
        {
            var row = new DataGridViewRow();
            // Show Fix button if FixAvailable is true OR if ManualInstructions exist (for manual tasks)
            var showFixButton = item.FixAvailable || !string.IsNullOrEmpty(item.ManualInstructions);
            var actionText = item.RequiresManualConfirmation ? "Done manually" : "Fix";
            row.CreateCells(_dataGridView,
                item.Category,
                item.Title,
                "Not Checked",
                "",
                showFixButton ? actionText : "",
                item.ManualInstructions ?? ""
            );
            row.Tag = item;
            _dataGridView.Rows.Add(row);
        }
    }

    private void DataGridView_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        var row = _dataGridView.Rows[e.RowIndex];
        var item = row.Tag as ChecklistItem;
        if (item == null) return;

        // Get the column index by name to avoid hardcoded index issues
        var statusColumnIndex = _dataGridView.Columns["Status"]?.Index ?? -1;
        
        if (e.ColumnIndex == statusColumnIndex) // Status column
        {
            var status = e.Value?.ToString() ?? "Not Checked";
            var result = _resultsDict.GetValueOrDefault(item.Id);
            if (result != null)
            {
                status = result.Status.ToString();
            }

            switch (status)
            {
                case "Pass":
                    e.CellStyle.BackColor = Color.FromArgb(46, 204, 113);
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                    break;
                case "Fail":
                    e.CellStyle.BackColor = Color.FromArgb(231, 76, 60);
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                    break;
                case "Unknown":
                    e.CellStyle.BackColor = Color.FromArgb(241, 196, 15);
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                    break;
                default:
                    e.CellStyle.BackColor = Color.FromArgb(236, 240, 241);
                    e.CellStyle.ForeColor = Color.Black;
                    break;
            }
            e.Value = status;
        }
    }

    private async void DataGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        var row = _dataGridView.Rows[e.RowIndex];
        var item = row.Tag as ChecklistItem;
        if (item == null) return;

        // Get the column index by name to avoid hardcoded index issues
        var fixColumnIndex = _dataGridView.Columns["Fix"]?.Index ?? -1;
        
        // Allow Fix button to work if FixAvailable is true OR if ManualInstructions exist
        var canFix = item.FixAvailable || !string.IsNullOrEmpty(item.ManualInstructions);
        
        if (e.ColumnIndex == fixColumnIndex && canFix) // Fix button
        {
            if (item.RequiresManualConfirmation)
            {
                var manualResult = MessageBox.Show(
                    $"This step cannot be verified automatically.\n\nPlease confirm you have manually reviewed/corrected:\n{item.Title}\n\nMark as done manually?",
                    "Manual Confirmation Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (manualResult == DialogResult.Yes)
                {
                    var confirmedResult = new ChecklistResult
                    {
                        ItemId = item.Id,
                        Status = ChecklistStatus.Pass,
                        Evidence = $"Confirmed manually by user at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.",
                        CheckedAt = DateTime.Now,
                        CanFix = true
                    };

                    _resultsDict[item.Id] = confirmedResult;
                    _session.Results[item.Id] = confirmedResult;

                    var statusCol = _dataGridView.Columns["Status"];
                    var evidenceCol = _dataGridView.Columns["Evidence"];
                    if (statusCol != null) row.Cells[statusCol.Index].Value = confirmedResult.Status.ToString();
                    if (evidenceCol != null) row.Cells[evidenceCol.Index].Value = confirmedResult.Evidence;

                    _statusLabel.Text = "Manual step confirmed";
                    _dataGridView.Refresh();
                }

                return;
            }

            var result = MessageBox.Show(
                $"Do you want to fix: {item.Title}?\n\n{item.Description}",
                "Confirm Fix",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                await FixItemAsync(item);
            }
        }
    }

    private async void RunAllChecksButton_Click(object? sender, EventArgs e)
    {
        _runAllChecksButton.Enabled = false;
        _progressBar.Maximum = _itemsDict.Count;
        _progressBar.Value = 0;
        _statusLabel.Text = "Running checks and auto-fixing...";

        // Run checks with auto-fix enabled
        await RunChecksAsync(_itemsDict.Values.ToList(), autoFix: true);

        _progressBar.Value = _progressBar.Maximum;
        _runAllChecksButton.Enabled = true;
        _statusLabel.Text = "Checks completed";
        _dataGridView.Refresh();
    }

    private async void RecheckSelectedButton_Click(object? sender, EventArgs e)
    {
        var selectedItems = _dataGridView.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(r => r.Tag as ChecklistItem)
            .Where(i => i != null)
            .Cast<ChecklistItem>()
            .ToList();

        if (selectedItems.Count == 0)
        {
            MessageBox.Show("Please select at least one item to re-check.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _recheckSelectedButton.Enabled = false;
        _progressBar.Maximum = selectedItems.Count;
        _progressBar.Value = 0;
        _statusLabel.Text = "Re-checking selected items...";

        await RunChecksAsync(selectedItems);

        _progressBar.Value = _progressBar.Maximum;
        _recheckSelectedButton.Enabled = true;
        _statusLabel.Text = "Re-check completed";
        _dataGridView.Refresh();
    }

    private async Task RunChecksAsync(List<ChecklistItem> items, bool autoFix = false)
    {
        // Reset progress bar for this batch of checks
        _progressBar.Maximum = items.Count;
        _progressBar.Value = 0;
        
        // Pass current ID values from textboxes to the service before running checks
        _checklistService.CurrentAnyDeskId = _anyDeskIdTextBox.Text.Trim();
        _checklistService.CurrentRustDeskId = _rustDeskIdTextBox.Text.Trim();
        
        foreach (var item in items)
        {
            // Run check with auto-fix enabled
            var result = await _checklistService.CheckItemAsync(item, autoFix: autoFix);
            _resultsDict[item.Id] = result;
            _session.Results[item.Id] = result;

            // Automatically extract and save IDs if detected
            ExtractAndSaveIdsFromResult(item, result);

            var row = _dataGridView.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(r => r.Tag == item);
            if (row != null)
            {
                var statusCol = _dataGridView.Columns["Status"];
                var evidenceCol = _dataGridView.Columns["Evidence"];
                if (statusCol != null) row.Cells[statusCol.Index].Value = result.Status.ToString();
                if (evidenceCol != null) row.Cells[evidenceCol.Index].Value = result.Evidence;
            }

            // Safely increment progress bar, ensuring we don't exceed Maximum
            if (_progressBar.Value < _progressBar.Maximum)
            {
                _progressBar.Value++;
            }
            Application.DoEvents();
        }
        
        // Ensure progress bar is at maximum when done
        if (_progressBar.Value < _progressBar.Maximum)
        {
            _progressBar.Value = _progressBar.Maximum;
        }
    }

    private async Task FixItemAsync(ChecklistItem item)
    {
        _statusLabel.Text = $"Fixing: {item.Title}...";
        
        // Pass current ID values from textboxes to the service
        _checklistService.CurrentAnyDeskId = _anyDeskIdTextBox.Text.Trim();
        _checklistService.CurrentRustDeskId = _rustDeskIdTextBox.Text.Trim();
        
        var result = await _checklistService.FixItemAsync(item);
        _resultsDict[item.Id] = result;
        _session.Results[item.Id] = result;

        // Automatically extract and save IDs if detected
        ExtractAndSaveIdsFromResult(item, result);

        var row = _dataGridView.Rows.Cast<DataGridViewRow>()
            .FirstOrDefault(r => r.Tag == item);
        if (row != null)
        {
            var statusCol = _dataGridView.Columns["Status"];
            var evidenceCol = _dataGridView.Columns["Evidence"];
            if (statusCol != null) row.Cells[statusCol.Index].Value = result.Status.ToString();
            if (evidenceCol != null) row.Cells[evidenceCol.Index].Value = result.Evidence;
        }

        _statusLabel.Text = "Fix completed";
        _dataGridView.Refresh();

        // Re-check after fix to verify it worked
        // For manual entry items (like AnyDesk ID, RustDesk ID), if fix was successful (Pass),
        // preserve the Pass status and don't overwrite with re-check results
        bool isManualEntryItem = item.CheckImplementation == "AnyDeskID" || 
                                 item.CheckImplementation == "RustDeskID" ||
                                 item.Title.Contains("AnyDesk ID", StringComparison.OrdinalIgnoreCase) ||
                                 item.Title.Contains("RustDesk ID", StringComparison.OrdinalIgnoreCase);

        // Computer rename takes effect after restart; re-check would still show the old MachineName.
        bool isComputerRenameItem = item.CheckImplementation == "ComputerName" ||
                                    item.Title.Contains("Set PC Name", StringComparison.OrdinalIgnoreCase);
        
        if ((isManualEntryItem || isComputerRenameItem) && result.Status == ChecklistStatus.Pass)
        {
            // For manual entry items with Pass status, skip re-check to preserve the result
            // The Pass status indicates the user has entered the ID manually
            _statusLabel.Text = "Fix completed - Status preserved";
        }
        else
        {
            // For other items, do re-check as normal
            // For Delivery Optimization, add longer delay to allow registry changes to take effect
            bool isDeliveryOptimization = item.CheckImplementation == "Item30" || 
                                         item.Title.Contains("Delivery Optimization", StringComparison.OrdinalIgnoreCase);
            await Task.Delay(isDeliveryOptimization ? 3000 : 500);
            
            // Refresh ID values from textboxes before re-checking
            _checklistService.CurrentAnyDeskId = _anyDeskIdTextBox.Text.Trim();
            _checklistService.CurrentRustDeskId = _rustDeskIdTextBox.Text.Trim();
            var verifyResult = await _checklistService.CheckItemAsync(item, autoFix: false);
            
            // Only update if re-check result is Pass (same or better)
            // Don't overwrite Pass with Unknown or Fail
            if (result.Status == ChecklistStatus.Pass && verifyResult.Status == ChecklistStatus.Pass)
            {
                // Both are Pass, update with verification result (may have updated evidence)
                _resultsDict[item.Id] = verifyResult;
                _session.Results[item.Id] = verifyResult;

                // Automatically extract and save IDs from verification result
                ExtractAndSaveIdsFromResult(item, verifyResult);

                var verifyRow = _dataGridView.Rows.Cast<DataGridViewRow>()
                    .FirstOrDefault(r => r.Tag == item);
                if (verifyRow != null)
                {
                    var statusCol = _dataGridView.Columns["Status"];
                    var evidenceCol = _dataGridView.Columns["Evidence"];
                    if (statusCol != null) verifyRow.Cells[statusCol.Index].Value = verifyResult.Status.ToString();
                    if (evidenceCol != null) verifyRow.Cells[evidenceCol.Index].Value = verifyResult.Evidence;
                }
                _statusLabel.Text = "Fix and verification completed";
            }
            else if (result.Status == ChecklistStatus.Pass && verifyResult.Status != ChecklistStatus.Pass)
            {
                // Preserve the Pass status from fix, don't overwrite with worse status (Unknown/Fail)
                // Keep the original result in resultsDict and session
                _statusLabel.Text = "Fix completed - Status preserved";
            }
            else
            {
                // Original result was not Pass, so update with verification result
                _resultsDict[item.Id] = verifyResult;
                _session.Results[item.Id] = verifyResult;

                // Automatically extract and save IDs from verification result
                ExtractAndSaveIdsFromResult(item, verifyResult);

                var verifyRow = _dataGridView.Rows.Cast<DataGridViewRow>()
                    .FirstOrDefault(r => r.Tag == item);
                if (verifyRow != null)
                {
                    var statusCol = _dataGridView.Columns["Status"];
                    var evidenceCol = _dataGridView.Columns["Evidence"];
                    if (statusCol != null) verifyRow.Cells[statusCol.Index].Value = verifyResult.Status.ToString();
                    if (evidenceCol != null) verifyRow.Cells[evidenceCol.Index].Value = verifyResult.Evidence;
                }
                _statusLabel.Text = "Fix and verification completed";
            }
        }
        _dataGridView.Refresh();
    }

    private void ExportReportButton_Click(object? sender, EventArgs e)
    {
        UpdateSessionFromUI();

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|HTML files (*.html)|*.html|All files (*.*)|*.*",
            FileName = $"ChecklistReport_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                if (dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    ExportJsonReport(dialog.FileName);
                }
                else if (dialog.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                {
                    ExportHtmlReport(dialog.FileName);
                }
                else
                {
                    ExportJsonReport(dialog.FileName + ".json");
                    ExportHtmlReport(dialog.FileName + ".html");
                }

                MessageBox.Show("Report exported successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting report: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ExportJsonReport(string filePath)
    {
        _session.CompletedAt = DateTime.Now;
        var json = JsonConvert.SerializeObject(_session, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    private void ExportHtmlReport(string filePath)
    {
        var html = GenerateHtmlReport();
        File.WriteAllText(filePath, html);
    }

    private string GenerateHtmlReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='utf-8'>");
        sb.AppendLine("<title>PC Readiness Checklist Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; background: #f5f5fa; }");
        sb.AppendLine(".container { max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
        sb.AppendLine("h1 { color: #1e3a5f; border-bottom: 3px solid #3498db; padding-bottom: 10px; }");
        sb.AppendLine("h2 { color: #2c3e50; margin-top: 30px; }");
        sb.AppendLine(".header-info { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin: 20px 0; }");
        sb.AppendLine(".info-item { padding: 10px; background: #ecf0f1; border-radius: 4px; }");
        sb.AppendLine(".info-label { font-weight: bold; color: #34495e; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
        sb.AppendLine("th { background: #3498db; color: white; padding: 12px; text-align: left; }");
        sb.AppendLine("td { padding: 10px; border-bottom: 1px solid #ddd; }");
        sb.AppendLine("tr:nth-child(even) { background: #f8f9fa; }");
        sb.AppendLine(".status-pass { background: #2ecc71; color: white; padding: 4px 8px; border-radius: 4px; font-weight: bold; }");
        sb.AppendLine(".status-fail { background: #e74c3c; color: white; padding: 4px 8px; border-radius: 4px; font-weight: bold; }");
        sb.AppendLine(".status-unknown { background: #f1c40f; color: white; padding: 4px 8px; border-radius: 4px; font-weight: bold; }");
        sb.AppendLine(".summary { margin: 20px 0; padding: 15px; background: #ecf0f1; border-radius: 4px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class='container'>");
        sb.AppendLine("<h1>PC Readiness Checklist Report</h1>");

        // Header info
        sb.AppendLine("<div class='header-info'>");
        sb.AppendLine($"<div class='info-item'><span class='info-label'>Customer:</span> {_session.Customer}</div>");
        sb.AppendLine($"<div class='info-item'><span class='info-label'>System:</span> {_session.System}</div>");
        sb.AppendLine($"<div class='info-item'><span class='info-label'>Installation Date:</span> {_session.InstallationDate:yyyy-MM-dd}</div>");
        sb.AppendLine($"<div class='info-item'><span class='info-label'>MAC Address:</span> {_session.MacAddress ?? "N/A"}</div>");
        sb.AppendLine($"<div class='info-item'><span class='info-label'>AnyDesk ID:</span> {_session.AnyDeskId ?? "N/A"}</div>");
        sb.AppendLine($"<div class='info-item'><span class='info-label'>RustDesk ID:</span> {_session.RustDeskId ?? "N/A"}</div>");
        sb.AppendLine($"<div class='info-item'><span class='info-label'>Performed By:</span> {_session.PerformedBy ?? "N/A"}</div>");
        sb.AppendLine($"<div class='info-item'><span class='info-label'>Reviewed By:</span> {_session.ReviewedBy ?? "N/A"}</div>");
        sb.AppendLine($"<div class='info-item'><span class='info-label'>Report Date:</span> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");
        sb.AppendLine("</div>");

        // Summary
        var passCount = _session.Results.Values.Count(r => r.Status == ChecklistStatus.Pass);
        var failCount = _session.Results.Values.Count(r => r.Status == ChecklistStatus.Fail);
        var unknownCount = _session.Results.Values.Count(r => r.Status == ChecklistStatus.Unknown);
        var total = _session.Results.Count;

        sb.AppendLine("<div class='summary'>");
        sb.AppendLine($"<h2>Summary</h2>");
        sb.AppendLine($"<p><strong>Total Items:</strong> {total}</p>");
        sb.AppendLine($"<p><strong>Pass:</strong> {passCount} | <strong>Fail:</strong> {failCount} | <strong>Unknown:</strong> {unknownCount}</p>");
        sb.AppendLine($"<p><strong>Completion:</strong> {((passCount + failCount) * 100.0 / total):F1}%</p>");
        sb.AppendLine("</div>");

        // Checklist items by category
        var categories = _itemsDict.Values.GroupBy(i => i.Category).OrderBy(g => g.Key);
        foreach (var category in categories)
        {
            sb.AppendLine($"<h2>{category.Key}</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>ID</th><th>Title</th><th>Status</th><th>Evidence</th><th>Manual Instructions</th></tr>");

            foreach (var item in category.OrderBy(i => int.TryParse(i.Id, out var id) ? id : 999))
            {
                var result = _session.Results.GetValueOrDefault(item.Id);
                var status = result?.Status ?? ChecklistStatus.NotChecked;
                var statusClass = status.ToString().ToLower();
                var evidence = result?.Evidence ?? "";
                var instructions = item.ManualInstructions ?? "";

                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{item.Id}</td>");
                sb.AppendLine($"<td><strong>{item.Title}</strong><br><small>{item.Description}</small></td>");
                sb.AppendLine($"<td><span class='status-{statusClass}'>{status}</span></td>");
                sb.AppendLine($"<td>{evidence}</td>");
                sb.AppendLine($"<td>{instructions}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");
        }

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private void SaveSessionButton_Click(object? sender, EventArgs e)
    {
        UpdateSessionFromUI();

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"ChecklistSession_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var json = JsonConvert.SerializeObject(_session, Formatting.Indented);
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show("Session saved successfully!", "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving session: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void LoadSessionButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                _session = JsonConvert.DeserializeObject<ChecklistSession>(json) ?? new ChecklistSession();
                _resultsDict = _session.Results;
                LoadSessionToUI();
                _dataGridView.Refresh();
                MessageBox.Show("Session loaded successfully!", "Load Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading session: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void UpdateSessionFromUI()
    {
        _session.Customer = _customerTextBox.Text;
        _session.System = _systemTextBox.Text;
        _session.InstallationDate = _installationDatePicker.Value;
        _session.MacAddress = _checklistService.GetMacAddress();
        _session.AnyDeskId = _anyDeskIdTextBox.Text;
        _session.RustDeskId = _rustDeskIdTextBox.Text;
        _session.PerformedBy = _performedByTextBox.Text;
        _session.ReviewedBy = _reviewedByTextBox.Text;
    }

    private void LoadSessionToUI()
    {
        _customerTextBox.Text = _session.Customer;
        _systemTextBox.Text = _session.System;
        _installationDatePicker.Value = _session.InstallationDate;
        _anyDeskIdTextBox.Text = _session.AnyDeskId ?? "";
        _rustDeskIdTextBox.Text = _session.RustDeskId ?? "";
        _performedByTextBox.Text = _session.PerformedBy ?? "";
        _reviewedByTextBox.Text = _session.ReviewedBy ?? "";
    }

    private void ExtractAndSaveIdsFromResult(ChecklistItem item, ChecklistResult result)
    {
        // Check if this is an AnyDesk or RustDesk ID check
        bool isAnyDeskCheck = item.CheckImplementation == "AnyDeskID" || 
                             item.Title.Contains("AnyDesk", StringComparison.OrdinalIgnoreCase) ||
                             item.Description.Contains("AnyDesk", StringComparison.OrdinalIgnoreCase);
        
        bool isRustDeskCheck = item.CheckImplementation == "RustDeskID" || 
                              item.Title.Contains("RustDesk", StringComparison.OrdinalIgnoreCase) ||
                              item.Description.Contains("RustDesk", StringComparison.OrdinalIgnoreCase);

        if (isAnyDeskCheck && result.Status == ChecklistStatus.Pass)
        {
            // Extract AnyDesk ID from Evidence (format: "AnyDesk ID: 1 468 621 262" or "AnyDesk ID: 1468621262")
            // Match the full ID including spaces, then remove spaces to get the clean ID
            // Use a more robust pattern that captures all digits and spaces until end of string or next non-digit/space
            var match = System.Text.RegularExpressions.Regex.Match(result.Evidence, @"AnyDesk\s+ID:\s*([\d\s]+?)(?:\s|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                // Try alternative pattern without spaces
                match = System.Text.RegularExpressions.Regex.Match(result.Evidence, @"AnyDesk\s+ID:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            if (match.Success)
            {
                var id = match.Groups[1].Value.Trim();
                // Remove all spaces to get clean ID
                var cleanId = id.Replace(" ", "").Trim();
                // Validate it's a reasonable AnyDesk ID (typically 9-10 digits)
                if (!string.IsNullOrEmpty(cleanId) && cleanId.Length >= 9 && cleanId.All(char.IsDigit))
                {
                    _session.AnyDeskId = cleanId;
                    if (_anyDeskIdTextBox != null)
                    {
                        // Only update textbox if it's empty or different (preserve user input if they already entered it)
                        if (string.IsNullOrWhiteSpace(_anyDeskIdTextBox.Text) || 
                            _anyDeskIdTextBox.Text.Replace(" ", "").Trim() != cleanId)
                        {
                            _anyDeskIdTextBox.Text = cleanId;
                        }
                    }
                }
            }
        }

        if (isRustDeskCheck && result.Status == ChecklistStatus.Pass)
        {
            // Extract RustDesk ID from Evidence (format: "RustDesk ID: 1234567890123456")
            var match = System.Text.RegularExpressions.Regex.Match(result.Evidence, @"RustDesk\s+ID:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var id = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(id))
                {
                    _session.RustDeskId = id;
                    if (_rustDeskIdTextBox != null)
                    {
                        _rustDeskIdTextBox.Text = id;
                    }
                }
            }
        }
    }
}

