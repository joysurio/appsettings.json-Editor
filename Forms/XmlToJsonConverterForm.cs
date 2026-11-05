using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AppSettingsEditor.Services;

namespace AppSettingsEditor.Forms;

internal partial class XmlToJsonConverterForm : Form
{
    private Button _loadXmlButton = null!;
    private TextBox _xmlContentTextBox = null!;
    private TextBox _jsonPreviewTextBox = null!;
    private Button _saveJsonButton = null!;
    private Label _statusLabel = null!;
    private Panel _conversionReportPanel = null!;
    private TextBox _reportTextBox = null!;
    private SplitContainer _mainSplitContainer = null!;
    private XmlToJsonConverterService _converterService = null!;
    private string? _currentXmlFilePath = null;

    public XmlToJsonConverterForm()
    {
        _converterService = new XmlToJsonConverterService();
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "XML to appsettings.json Converter";
        this.Size = new Size(1000, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(800, 600);
        this.BackColor = Color.FromArgb(245, 245, 250);

        // Main layout: Toolbar at top, SplitContainer in middle, Status at bottom
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Toolbar
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Status

        // Toolbar
        var toolbar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(10)
        };

        _loadXmlButton = new Button
        {
            Text = "Load XML",
            Size = new Size(120, 30),
            Location = new Point(10, 10),
            BackColor = Color.FromArgb(52, 152, 219),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        _loadXmlButton.FlatAppearance.BorderSize = 0;
        _loadXmlButton.Click += LoadXmlButton_Click;

        _saveJsonButton = new Button
        {
            Text = "Save JSON",
            Size = new Size(120, 30),
            Location = new Point(140, 10),
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Enabled = false
        };
        _saveJsonButton.FlatAppearance.BorderSize = 0;
        _saveJsonButton.Click += SaveJsonButton_Click;

        toolbar.Controls.Add(_loadXmlButton);
        toolbar.Controls.Add(_saveJsonButton);

        // Top: Side-by-side XML (left) and JSON (right, editable). Bottom: Conversion report.
        var topBottomSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 420,
            FixedPanel = FixedPanel.Panel2
        };

        // Left-right split: XML | JSON
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 480
        };

        // XML panel (left)
        var xmlPanel = new Panel { Dock = DockStyle.Fill };
        var xmlLabel = new Label
        {
            Text = "XML (source):",
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(5, 5, 0, 0),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        _xmlContentTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(248, 248, 248),
            BorderStyle = BorderStyle.FixedSingle
        };
        xmlPanel.Controls.Add(_xmlContentTextBox);
        xmlPanel.Controls.Add(xmlLabel);

        // JSON panel (right) – editable so user can change parameters after conversion
        var jsonPanel = new Panel { Dock = DockStyle.Fill };
        var jsonLabel = new Label
        {
            Text = "JSON (editable):",
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(5, 5, 0, 0),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        _jsonPreviewTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = false,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        jsonPanel.Controls.Add(_jsonPreviewTextBox);
        jsonPanel.Controls.Add(jsonLabel);

        _mainSplitContainer.Panel1.Controls.Add(xmlPanel);
        _mainSplitContainer.Panel2.Controls.Add(jsonPanel);

        // Conversion report panel (bottom)
        _conversionReportPanel = new Panel { Dock = DockStyle.Fill };
        var reportLabel = new Label
        {
            Text = "Conversion Report:",
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(5, 5, 0, 0),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        _reportTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 8),
            BackColor = Color.FromArgb(250, 250, 250),
            BorderStyle = BorderStyle.FixedSingle
        };
        _conversionReportPanel.Controls.Add(_reportTextBox);
        _conversionReportPanel.Controls.Add(reportLabel);

        topBottomSplit.Panel1.Controls.Add(_mainSplitContainer);
        topBottomSplit.Panel2.Controls.Add(_conversionReportPanel);

        // Status bar
        _statusLabel = new Label
        {
            Text = "Ready",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 5, 0, 0),
            ForeColor = Color.FromArgb(100, 100, 100)
        };

        mainPanel.Controls.Add(toolbar, 0, 0);
        mainPanel.Controls.Add(topBottomSplit, 0, 1);
        mainPanel.Controls.Add(_statusLabel, 0, 2);

        this.Controls.Add(mainPanel);

        // Enable drag & drop for XML files
        this.AllowDrop = true;
        this.DragEnter += Form_DragEnter;
        this.DragDrop += Form_DragDrop;
    }

    private void LoadXmlButton_Click(object? sender, EventArgs e)
    {
        using var openDialog = new OpenFileDialog
        {
            Filter = "XML Files|*.xml|All Files|*.*",
            Title = "Select XML Configuration File"
        };

        if (openDialog.ShowDialog() == DialogResult.OK)
        {
            LoadAndConvertXml(openDialog.FileName);
        }
    }

    private void SaveJsonButton_Click(object? sender, EventArgs e)
    {
        var jsonToSave = _jsonPreviewTextBox.Text.Trim();
        if (string.IsNullOrEmpty(jsonToSave))
        {
            MessageBox.Show("No JSON to save. Please load and convert an XML file first.", "No Data", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var saveDialog = new SaveFileDialog
        {
            Filter = "JSON Files|*.json|All Files|*.*",
            Title = "Save appsettings.json",
            FileName = "appsettings.json"
        };

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                File.WriteAllText(saveDialog.FileName, jsonToSave);
                _statusLabel.Text = $"JSON saved successfully to: {saveDialog.FileName}";
                _statusLabel.ForeColor = Color.FromArgb(46, 204, 113);
                MessageBox.Show("JSON file saved successfully!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error saving file: {ex.Message}";
                _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
                MessageBox.Show($"Error saving file:\n{ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void LoadAndConvertXml(string xmlFilePath)
    {
        try
        {
            _statusLabel.Text = "Converting XML...";
            _statusLabel.ForeColor = Color.FromArgb(52, 152, 219);
            Application.DoEvents();

            var result = _converterService.ConvertXmlToJson(xmlFilePath);

            if (result.Success)
            {
                _currentXmlFilePath = xmlFilePath;
                var xmlContent = File.Exists(xmlFilePath) ? File.ReadAllText(xmlFilePath) : "";
                _xmlContentTextBox.Text = xmlContent;
                _jsonPreviewTextBox.Text = result.JsonOutput;
                _saveJsonButton.Enabled = true;

                // Build conversion report
                var report = new System.Text.StringBuilder();
                report.AppendLine($"Conversion completed successfully!");
                report.AppendLine($"Source: {Path.GetFileName(xmlFilePath)}");
                report.AppendLine();
                report.AppendLine($"Mapped Keys: {result.MappedKeys.Count}");
                if (result.MappedKeys.Count > 0)
                {
                    report.AppendLine("---");
                    foreach (var mapped in result.MappedKeys.Take(20)) // Show first 20
                    {
                        report.AppendLine($"✓ {mapped}");
                    }
                    if (result.MappedKeys.Count > 20)
                        report.AppendLine($"... and {result.MappedKeys.Count - 20} more");
                }
                report.AppendLine();

                if (result.UnmappedKeys.Count > 0)
                {
                    report.AppendLine($"Unmapped Keys: {result.UnmappedKeys.Count}");
                    report.AppendLine("---");
                    foreach (var unmapped in result.UnmappedKeys)
                    {
                        report.AppendLine($"⚠ {unmapped}");
                    }
                    report.AppendLine();
                }

                if (result.MissingExpectedKeys.Count > 0)
                {
                    report.AppendLine($"Missing Expected Keys: {result.MissingExpectedKeys.Count}");
                    report.AppendLine("---");
                    foreach (var missing in result.MissingExpectedKeys)
                    {
                        report.AppendLine($"○ {missing}");
                    }
                }

                _reportTextBox.Text = report.ToString();
                _statusLabel.Text = $"Conversion successful! Mapped: {result.MappedKeys.Count}, Unmapped: {result.UnmappedKeys.Count}";
                _statusLabel.ForeColor = Color.FromArgb(46, 204, 113);
            }
            else
            {
                _xmlContentTextBox.Text = File.Exists(xmlFilePath) ? File.ReadAllText(xmlFilePath) : "";
                _jsonPreviewTextBox.Text = string.Empty;
                _reportTextBox.Text = $"Error: {result.ErrorMessage}";
                _saveJsonButton.Enabled = false;
                _statusLabel.Text = $"Error: {result.ErrorMessage}";
                _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
                MessageBox.Show($"Conversion failed:\n{result.ErrorMessage}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Unexpected error: {ex.Message}";
            _statusLabel.ForeColor = Color.FromArgb(231, 76, 60);
            MessageBox.Show($"Unexpected error:\n{ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Form_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void Form_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var filePath = files[0];
                if (Path.GetExtension(filePath).Equals(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    LoadAndConvertXml(filePath);
                }
                else
                {
                    MessageBox.Show("Please drop an XML file.", "Invalid File Type", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
