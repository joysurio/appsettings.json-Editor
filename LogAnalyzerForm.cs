using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

namespace AppSettingsEditor;

public partial class LogAnalyzerForm : Form
{
    private Button btnSelectZip;
    private TextBox txtZipPath;
    private Button btnAnalyze;
    private RichTextBox txtStatus;
    private ProgressBar progressBar;
    private Label lblStatus;
    private string[] _selectedTextFiles = Array.Empty<string>();
    private bool _isZipSelection;

    public LogAnalyzerForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Log Analyzer - Resume Maker";
        this.Size = new System.Drawing.Size(800, 600);
        this.StartPosition = FormStartPosition.CenterScreen;

        // Source selection
        Label lblZip = new Label
        {
            Text = "Select ZIP or TXT/LOG files:",
            Location = new System.Drawing.Point(20, 20),
            Size = new System.Drawing.Size(220, 23),
            AutoSize = true
        };
        this.Controls.Add(lblZip);

        txtZipPath = new TextBox
        {
            Location = new System.Drawing.Point(20, 50),
            Size = new System.Drawing.Size(500, 23),
            ReadOnly = true
        };
        this.Controls.Add(txtZipPath);

        btnSelectZip = new Button
        {
            Text = "Browse...",
            Location = new System.Drawing.Point(530, 48),
            Size = new System.Drawing.Size(100, 30)
        };
        btnSelectZip.Click += BtnSelectZip_Click;
        this.Controls.Add(btnSelectZip);

        // Analyze button
        btnAnalyze = new Button
        {
            Text = "Analyze and Generate PDF",
            Location = new System.Drawing.Point(20, 90),
            Size = new System.Drawing.Size(200, 35),
            Enabled = false
        };
        btnAnalyze.Click += BtnAnalyze_Click;
        this.Controls.Add(btnAnalyze);

        // Progress bar
        progressBar = new ProgressBar
        {
            Location = new System.Drawing.Point(20, 135),
            Size = new System.Drawing.Size(610, 23),
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };
        this.Controls.Add(progressBar);

        // Status label
        lblStatus = new Label
        {
            Text = "Ready",
            Location = new System.Drawing.Point(20, 165),
            Size = new System.Drawing.Size(610, 23),
            AutoSize = true
        };
        this.Controls.Add(lblStatus);

        // Status text box
        txtStatus = new RichTextBox
        {
            Location = new System.Drawing.Point(20, 195),
            Size = new System.Drawing.Size(610, 350),
            ReadOnly = true,
            Font = new System.Drawing.Font("Consolas", 9)
        };
        this.Controls.Add(txtStatus);
    }

    private void BtnSelectZip_Click(object sender, EventArgs e)
    {
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "Supported files (*.zip;*.txt;*.log)|*.zip;*.txt;*.log|Zip files (*.zip)|*.zip|Text/Log files (*.txt;*.log)|*.txt;*.log|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var selected = openFileDialog.FileNames ?? Array.Empty<string>();
                if (selected.Length == 0)
                    return;

                var zipFiles = selected.Where(f => string.Equals(Path.GetExtension(f), ".zip", StringComparison.OrdinalIgnoreCase)).ToArray();
                var textFiles = selected.Where(f =>
                    string.Equals(Path.GetExtension(f), ".txt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetExtension(f), ".log", StringComparison.OrdinalIgnoreCase)).ToArray();

                if (zipFiles.Length > 1)
                {
                    MessageBox.Show("Please select only one ZIP file at a time.", "Invalid selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (zipFiles.Length == 1 && textFiles.Length > 0)
                {
                    MessageBox.Show("Select either one ZIP file OR TXT/LOG files, not both together.", "Invalid selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                txtStatus.Clear();

                if (zipFiles.Length == 1)
                {
                    _isZipSelection = true;
                    _selectedTextFiles = Array.Empty<string>();
                    txtZipPath.Text = zipFiles[0];
                    txtStatus.AppendText($"Selected ZIP: {Path.GetFileName(zipFiles[0])}\n");
                }
                else
                {
                    _isZipSelection = false;
                    _selectedTextFiles = textFiles;
                    txtZipPath.Text = $"{textFiles.Length} text/log file(s) selected";
                    txtStatus.AppendText($"Selected {textFiles.Length} text/log file(s):\n");
                    foreach (var f in textFiles.Take(10))
                        txtStatus.AppendText($" - {Path.GetFileName(f)}\n");
                    if (textFiles.Length > 10)
                        txtStatus.AppendText($" ... and {textFiles.Length - 10} more\n");
                }

                btnAnalyze.Enabled = zipFiles.Length == 1 || textFiles.Length > 0;
            }
        }
    }

    private async void BtnAnalyze_Click(object sender, EventArgs e)
    {
        if (_isZipSelection)
        {
            if (string.IsNullOrEmpty(txtZipPath.Text) || !File.Exists(txtZipPath.Text))
            {
                MessageBox.Show("Please select a valid zip file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        else
        {
            if (_selectedTextFiles.Length == 0 || _selectedTextFiles.Any(f => !File.Exists(f)))
            {
                MessageBox.Show("Please select one or more valid TXT/LOG files.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        btnAnalyze.Enabled = false;
        btnSelectZip.Enabled = false;
        progressBar.Visible = true;
        progressBar.Style = ProgressBarStyle.Marquee;
        lblStatus.Text = "Processing...";

        try
        {
            if (_isZipSelection)
                await ProcessZipFile(txtZipPath.Text);
            else
                await ProcessTextFiles(_selectedTextFiles);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            txtStatus.AppendText($"\nERROR: {ex.Message}\n");
        }
        finally
        {
            btnAnalyze.Enabled = true;
            btnSelectZip.Enabled = true;
            progressBar.Visible = false;
            lblStatus.Text = "Ready";
        }
    }

    private string GetProjectRootDirectory()
    {
        // Get the application's startup directory (where the exe is)
        string appDirectory = Application.StartupPath;
        
        // Navigate up to project root (from bin\Debug\net9.0-windows\ or bin\Release\net9.0-windows\)
        // Go up 3 levels: net9.0-windows -> Debug/Release -> bin -> project root
        DirectoryInfo dir = new DirectoryInfo(appDirectory);
        
        // Try to find project root by going up until we find the .csproj file or reach a reasonable limit
        int maxLevels = 5;
        int levels = 0;
        while (dir.Parent != null && levels < maxLevels)
        {
            // Check if this directory contains the project file
            if (Directory.GetFiles(dir.FullName, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
            levels++;
        }
        
        // Fallback: go up 3 levels from startup path
        dir = new DirectoryInfo(appDirectory);
        for (int i = 0; i < 3 && dir.Parent != null; i++)
        {
            dir = dir.Parent;
        }
        
        return dir.FullName;
    }

    private async Task ProcessZipFile(string zipPath)
    {
        txtStatus.AppendText("\n=== Starting Analysis ===\n");

        string dateStr = LogAnalyzer.ExtractDateFromFilename(Path.GetFileName(zipPath));
        if (string.IsNullOrEmpty(dateStr))
            dateStr = DateTime.Now.ToString("yyyyMMdd");

        txtStatus.AppendText($"Detected date: {dateStr}\n");

        // Extract zip to project directory
        string projectRoot = GetProjectRootDirectory();
        string extractPath = Path.Combine(projectRoot, $"extracted_logs_{dateStr}");
        txtStatus.AppendText($"Extracting zip file...\n");
        await Task.Run(() => LogAnalyzer.ExtractZip(zipPath, extractPath));
        txtStatus.AppendText($"Extracted to: {extractPath}\n");

        // Analyze logs
        txtStatus.AppendText("Analyzing log files...\n");
        var analysis = await Task.Run(() => LogAnalyzer.AnalyzeLogs(extractPath, dateStr));
        txtStatus.AppendText($"Found {analysis.Machines.Count} machines\n");
        txtStatus.AppendText($"Total log files: {analysis.TotalLogFiles}\n");

        // Generate markdown inside extracted_logs folder
        string mdFile = Path.Combine(extractPath, $"Log_Files_Summary_{dateStr}.md");
        txtStatus.AppendText("Generating markdown summary...\n");
        await Task.Run(() => MarkdownGenerator.GenerateSummary(analysis, mdFile, dateStr));
        txtStatus.AppendText($"Markdown created: {Path.GetFileName(mdFile)}\n");

        // Generate Network Connection Analysis markdown (always generated)
        string networkAnalysisMdFile = Path.Combine(extractPath, $"Network_Connection_Analysis_{dateStr}.md");
        txtStatus.AppendText("Generating Network Connection Analysis...\n");
        await Task.Run(() => NetworkAnalysisGenerator.GenerateNetworkAnalysis(analysis, networkAnalysisMdFile, dateStr));
        txtStatus.AppendText($"Network Analysis markdown created: {Path.GetFileName(networkAnalysisMdFile)}\n");
        
        // Convert to PDF inside extracted_logs folder
        string pdfFile = Path.Combine(extractPath, $"Log_Files_Summary_{dateStr}.pdf");
        txtStatus.AppendText("Converting to PDF...\n");
        await Task.Run(() => PdfConverter.ConvertToPdf(mdFile, pdfFile));
        txtStatus.AppendText($"PDF created: {Path.GetFileName(pdfFile)}\n");

        // Convert Network Connection Analysis to PDF (always convert since markdown is always generated)
        string networkAnalysisPdfFile = Path.Combine(extractPath, $"Network_Connection_Analysis_{dateStr}.pdf");
        txtStatus.AppendText("Converting Network Connection Analysis to PDF...\n");
        try
        {
            await Task.Run(() => PdfConverter.ConvertToPdf(networkAnalysisMdFile, networkAnalysisPdfFile));
            txtStatus.AppendText($"Network Analysis PDF created: {Path.GetFileName(networkAnalysisPdfFile)}\n");
        }
        catch (Exception ex)
        {
            txtStatus.AppendText($"Warning: Could not convert Network Analysis to PDF: {ex.Message}\n");
        }

        txtStatus.AppendText("\n=== Analysis Complete ===\n");
        txtStatus.AppendText($"\nSummary: {analysis.MachinesWithCriticalErrors} critical, {analysis.MachinesWithWarnings} warnings\n");

        // Open PDF
        DialogResult result = MessageBox.Show(
            "Analysis complete! Would you like to open the PDF?",
            "Success",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result == DialogResult.Yes && File.Exists(pdfFile))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = pdfFile,
                UseShellExecute = true
            });
        }
    }

    private async Task ProcessTextFiles(string[] textFiles)
    {
        txtStatus.AppendText("\n=== Starting Analysis ===\n");

        string dateStr = DateTime.Now.ToString("yyyyMMdd");
        txtStatus.AppendText($"Using date: {dateStr}\n");

        string projectRoot = GetProjectRootDirectory();
        string extractPath = Path.Combine(projectRoot, $"extracted_logs_{dateStr}");
        string selectedFilesFolder = Path.Combine(extractPath, "SelectedFiles");

        txtStatus.AppendText("Preparing selected text/log files...\n");
        await Task.Run(() =>
        {
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            Directory.CreateDirectory(selectedFilesFolder);

            foreach (var file in textFiles)
            {
                var target = Path.Combine(selectedFilesFolder, Path.GetFileName(file));
                File.Copy(file, target, true);
            }
        });
        txtStatus.AppendText($"Prepared {textFiles.Length} file(s) in: {selectedFilesFolder}\n");

        txtStatus.AppendText("Analyzing log files...\n");
        var analysis = await Task.Run(() => LogAnalyzer.AnalyzeLogs(extractPath, dateStr));
        txtStatus.AppendText($"Found {analysis.Machines.Count} machines\n");
        txtStatus.AppendText($"Total log files: {analysis.TotalLogFiles}\n");

        string mdFile = Path.Combine(extractPath, $"Log_Files_Summary_{dateStr}.md");
        txtStatus.AppendText("Generating markdown summary...\n");
        await Task.Run(() => MarkdownGenerator.GenerateSummary(analysis, mdFile, dateStr));
        txtStatus.AppendText($"Markdown created: {Path.GetFileName(mdFile)}\n");

        string networkAnalysisMdFile = Path.Combine(extractPath, $"Network_Connection_Analysis_{dateStr}.md");
        txtStatus.AppendText("Generating Network Connection Analysis...\n");
        await Task.Run(() => NetworkAnalysisGenerator.GenerateNetworkAnalysis(analysis, networkAnalysisMdFile, dateStr));
        txtStatus.AppendText($"Network Analysis markdown created: {Path.GetFileName(networkAnalysisMdFile)}\n");

        string pdfFile = Path.Combine(extractPath, $"Log_Files_Summary_{dateStr}.pdf");
        txtStatus.AppendText("Converting to PDF...\n");
        await Task.Run(() => PdfConverter.ConvertToPdf(mdFile, pdfFile));
        txtStatus.AppendText($"PDF created: {Path.GetFileName(pdfFile)}\n");

        string networkAnalysisPdfFile = Path.Combine(extractPath, $"Network_Connection_Analysis_{dateStr}.pdf");
        txtStatus.AppendText("Converting Network Connection Analysis to PDF...\n");
        try
        {
            await Task.Run(() => PdfConverter.ConvertToPdf(networkAnalysisMdFile, networkAnalysisPdfFile));
            txtStatus.AppendText($"Network Analysis PDF created: {Path.GetFileName(networkAnalysisPdfFile)}\n");
        }
        catch (Exception ex)
        {
            txtStatus.AppendText($"Warning: Could not convert Network Analysis to PDF: {ex.Message}\n");
        }

        txtStatus.AppendText("\n=== Analysis Complete ===\n");
        txtStatus.AppendText($"\nSummary: {analysis.MachinesWithCriticalErrors} critical, {analysis.MachinesWithWarnings} warnings\n");

        DialogResult result = MessageBox.Show(
            "Analysis complete! Would you like to open the PDF?",
            "Success",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result == DialogResult.Yes && File.Exists(pdfFile))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = pdfFile,
                UseShellExecute = true
            });
        }
    }
}
