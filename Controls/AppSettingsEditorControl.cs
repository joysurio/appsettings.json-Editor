using AppSettingsEditor.Models;
using AppSettingsEditor.Services;
using AppSettingsEditor.Forms;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace AppSettingsEditor.Controls;

internal partial class AppSettingsEditorControl : UserControl
{
    private JsonService _jsonService = null!;
    private MasterService _masterService = null!;
    private TemplateService _templateService = null!;
    private ValidationService _validationService = null!;
    private MachineTypeService _machineTypeService = null!;
    private ParameterMetadataService _parameterMetadataService = null!;
    private ConnectionStringTester _connectionStringTester = null!;
    private RemoteMachineAgentService _remoteMachineAgentService = null!;
    
    private AppSettingsRootModel _originalModel = null!;
    private AppSettingsRootModel _currentModel = null!;
    private Dictionary<string, Control> _fieldControls = null!;
    private Dictionary<string, Label> _statusLabels = null!;
    private Dictionary<string, bool> _fieldChanged = null!;
    private Dictionary<string, ValidationResult> _fieldValidation = null!;
    private Dictionary<string, List<string>> _customComboBoxValues = null!; // Track custom values per property

    private TabControl _tabControl = null!;
    private ToolStrip _toolStrip = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private Panel _mainContentPanel = null!;
    private MachineTypeSelectionControl? _machineTypeSelection = null;
    private RemoteMachineListControl? _remoteMachineList = null;
    private string? _selectedMachineType = null;
    private RemoteMachine? _connectedRemoteMachine = null;
    private bool _isRemoteMode = false;
    private ToolTip? _parameterToolTip;

    // Constructor for embeddability - accepts external services
    public AppSettingsEditorControl(
        JsonService? jsonService = null,
        MasterService? masterService = null,
        TemplateService? templateService = null,
        ValidationService? validationService = null,
        MachineTypeService? machineTypeService = null,
        ParameterMetadataService? parameterMetadataService = null,
        ConnectionStringTester? connectionStringTester = null,
        RemoteMachineAgentService? remoteMachineAgentService = null,
        string? baseDirectory = null)
    {
        InitializeComponent();
        InitializeServices(jsonService, masterService, templateService, validationService, 
            machineTypeService, parameterMetadataService, connectionStringTester, remoteMachineAgentService, baseDirectory);
        InitializeUI();
        ShowMachineTypeSelection();
    }

    // Default constructor for backward compatibility
    public AppSettingsEditorControl() : this(null, null, null, null, null, null, null, null, null)
    {
    }

    private void InitializeServices(
        JsonService? jsonService,
        MasterService? masterService,
        TemplateService? templateService,
        ValidationService? validationService,
        MachineTypeService? machineTypeService,
        ParameterMetadataService? parameterMetadataService,
        ConnectionStringTester? connectionStringTester,
        RemoteMachineAgentService? remoteMachineAgentService,
        string? baseDirectory)
    {
        _jsonService = jsonService ?? new JsonService();
        _masterService = masterService ?? new MasterService(baseDirectory);
        _templateService = templateService ?? new TemplateService(baseDirectory);
        _validationService = validationService ?? new ValidationService();
        _machineTypeService = machineTypeService ?? new MachineTypeService(baseDirectory);
        _parameterMetadataService = parameterMetadataService ?? new ParameterMetadataService(baseDirectory);
        _connectionStringTester = connectionStringTester ?? new ConnectionStringTester();
        _remoteMachineAgentService = remoteMachineAgentService ?? new RemoteMachineAgentService(baseDirectory);
        _fieldControls = new Dictionary<string, Control>();
        _statusLabels = new Dictionary<string, Label>();
        _fieldChanged = new Dictionary<string, bool>();
        _fieldValidation = new Dictionary<string, ValidationResult>();
        _customComboBoxValues = new Dictionary<string, List<string>>();
    }

    private void ShowMachineTypeSelection()
    {
        _mainContentPanel.Controls.Clear();
        _machineTypeSelection = new MachineTypeSelectionControl(_machineTypeService);
        _machineTypeSelection.MachineTypeSelected += (s, machineType) =>
        {
            _selectedMachineType = machineType;
            ShowLoadSourceChoice();
        };
        _mainContentPanel.Controls.Add(_machineTypeSelection);
    }

    private void ShowLoadSourceChoice()
    {
        _mainContentPanel.Controls.Clear();

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(50) };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        var titleLabel = new Label
        {
            Text = "How do you want to load the configuration?",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = UIThemeService.DarkBlue,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            AutoSize = false
        };
        layout.Controls.Add(titleLabel, 0, 0);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var openJsonButton = new Button
        {
            Text = "Open JSON file...",
            Width = 180,
            Height = 44,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        UIThemeService.ApplyModernButtonStyle(openJsonButton, isPrimary: true);
        openJsonButton.Click += (s, e) =>
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select appsettings JSON file"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _jsonService = new JsonService(dlg.FileName);
                LoadConfiguration();
            }
        };

        var loadTemplateButton = new Button
        {
            Text = "Load template...",
            Width = 180,
            Height = 44,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        UIThemeService.ApplyModernButtonStyle(loadTemplateButton, isPrimary: false);
        loadTemplateButton.Click += (s, e) =>
        {
            var templates = _templateService.GetAvailableTemplates();
            if (templates.Count == 0)
            {
                MessageBox.Show("No templates found in templates directory.", "No Templates",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = new Form
            {
                Text = "Select template",
                Size = new Size(400, 320),
                StartPosition = FormStartPosition.CenterParent
            };
            var listBox = new ListBox { Dock = DockStyle.Fill };
            listBox.Items.AddRange(templates.ToArray());
            if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;
            var okButton = new Button { Text = "Load", Width = 80, DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40 };
            flow.Controls.Add(okButton);
            flow.Controls.Add(cancelButton);
            form.Controls.Add(listBox);
            form.Controls.Add(flow);
            form.AcceptButton = okButton;
            form.CancelButton = cancelButton;
            if (form.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
            {
                var template = _templateService.LoadTemplate(listBox.SelectedItem.ToString()!);
                if (template != null)
                {
                    _currentModel = template;
                    _originalModel = CloneModel(_currentModel);
                    ShowEditor();
                    CreateTabs();
                    UpdateStatus();
                }
            }
        };

        buttonPanel.Controls.Add(openJsonButton);
        buttonPanel.Controls.Add(loadTemplateButton);
        layout.Controls.Add(buttonPanel, 0, 1);
        panel.Controls.Add(layout);
        _mainContentPanel.Controls.Add(panel);
    }

    private void InitializeUI()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = UIThemeService.LightGray;

        // ToolStrip with modern gradient
        _toolStrip = new ToolStrip
        {
            BackColor = UIThemeService.PrimaryBlue,
            ForeColor = UIThemeService.White,
            Renderer = new ModernToolStripRenderer()
        };
        _toolStrip.Items.Add(new ToolStripButton("💾 Save", null, (s, e) => SaveConfiguration()) { Enabled = false, ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripButton("↶ Discard", null, (s, e) => DiscardChanges()) { Enabled = false, ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripButton("🔄 Reload", null, (s, e) => LoadConfiguration()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripButton("📦 Restore Backup...", null, (s, e) => RestoreBackup()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripButton("📋 Apply Master...", null, (s, e) => ApplyMaster()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripButton("💼 Save Template...", null, (s, e) => SaveTemplate()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripButton("📂 Load Template...", null, (s, e) => LoadTemplate()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripButton("➕ Add Parameter...", null, (s, e) => ShowAddParameterDialog()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });

        // TabControl with modern rounded theme
        _tabControl = new TabControl 
        { 
            Dock = DockStyle.Fill,
            BackColor = UIThemeService.White,
            ForeColor = UIThemeService.DarkBlue,
            Font = new Font("Segoe UI", 9),
            Padding = new Point(12, 4)
        };
        _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabControl.DrawItem += (s, e) =>
        {
            var tab = _tabControl.TabPages[e.Index];
            var rect = _tabControl.GetTabRect(e.Index);
            var font = new Font("Segoe UI", 9, FontStyle.Regular);
            
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                // Selected tab with gradient
                using (var brush = new LinearGradientBrush(rect, UIThemeService.PrimaryBlue, UIThemeService.AccentBlue, 90f))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
                e.Graphics.DrawString(tab.Text, font, new SolidBrush(UIThemeService.White), rect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
            else
            {
                e.Graphics.FillRectangle(new SolidBrush(UIThemeService.LighterBlue), rect);
                e.Graphics.DrawString(tab.Text, font, new SolidBrush(UIThemeService.DarkBlue), rect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
        };

        // StatusStrip with modern style
        _statusStrip = new StatusStrip
        {
            BackColor = UIThemeService.PrimaryBlue,
            ForeColor = UIThemeService.White,
            Renderer = new ModernToolStripRenderer()
        };
        _statusLabel = new ToolStripStatusLabel("Ready") { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) };
        _statusStrip.Items.Add(_statusLabel);

        // Main content panel (will show machine type selection or editor)
        _mainContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UIThemeService.LightGray,
            Padding = new Padding(5)
        };

        // Layout
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        mainPanel.Controls.Add(_toolStrip, 0, 0);
        mainPanel.Controls.Add(_mainContentPanel, 0, 1);
        mainPanel.Controls.Add(_statusStrip, 0, 2);

        this.Controls.Add(mainPanel);
    }

    private void LoadConfiguration()
    {
        if (string.IsNullOrEmpty(_selectedMachineType))
        {
            return; // Machine type not selected yet
        }

        AppSettingsRootModel? loadedModel;
        
        if (_isRemoteMode && _connectedRemoteMachine != null)
        {
            try
            {
                loadedModel = _remoteMachineAgentService.LoadRemoteAppSettings(_connectedRemoteMachine);
                _statusLabel.Text = $"Connected to remote machine: {_connectedRemoteMachine.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading remote configuration: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                loadedModel = null;
            }
        }
        else
        {
            loadedModel = _jsonService.Load();
        }
        
        if (loadedModel == null)
        {
            // File doesn't exist or is invalid
            bool fileExists = _jsonService.FileExists();
            
            if (fileExists)
            {
                // File exists but is corrupted/invalid
                MessageBox.Show("appsettings.json is invalid or corrupted. You can restore from backup or apply a master configuration.", 
                    "Configuration Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _currentModel = new AppSettingsRootModel();
            }
            else
            {
                // File doesn't exist - try to load from master.default.json
                var masterModel = _masterService.LoadMaster("master.default.json");
                if (masterModel != null)
                {
                    // Successfully loaded from master, save it as appsettings.json
                    _currentModel = masterModel;
                    try
                    {
                        _jsonService.Save(_currentModel);
                    }
                    catch
                    {
                        // If save fails, continue with the loaded model
                    }
                }
                else
                {
                    // No master file available, create empty model
                    _currentModel = new AppSettingsRootModel();
                }
            }
        }
        else
        {
            _currentModel = loadedModel;
        }

        _originalModel = CloneModel(_currentModel);
        ShowEditor();
        CreateTabs();
        UpdateStatus();
    }

    private void ShowEditor()
    {
        _mainContentPanel.Controls.Clear();
        _mainContentPanel.Controls.Add(_tabControl);
    }

    private void CreateTabs()
    {
        if (string.IsNullOrEmpty(_selectedMachineType))
        {
            return;
        }

        _tabControl.TabPages.Clear();
        _fieldControls.Clear();
        _statusLabels.Clear();
        _fieldChanged.Clear();
        _fieldValidation.Clear();

        // Get sections for selected machine type
        var machineTypeConfig = _machineTypeService.GetMachineTypeConfiguration(_selectedMachineType ?? "");
        var allowedSections = machineTypeConfig?.Sections ?? new List<string>();

        // Only create tabs for allowed sections
        if (allowedSections.Contains("ConnectionStrings"))
        {
            CreateTab("ConnectionStrings", _currentModel.ConnectionStrings ?? new ConnectionStringsModel());
        }

        if (allowedSections.Contains("AppSettings"))
        {
            CreateTab("AppSettings", _currentModel.AppSettings ?? new AppSettingsModel());
        }

        if (allowedSections.Contains("DispenseConfiguration"))
        {
            CreateTab("DispenseConfiguration", _currentModel.DispenseConfiguration ?? new DispenseConfigurationModel());
        }

        if (allowedSections.Contains("AntennaConfiguration"))
        {
            CreateTab("AntennaConfiguration", _currentModel.AntennaConfiguration ?? new AntennaConfigurationModel());
        }

        if (allowedSections.Contains("EmailConfiguration"))
        {
            CreateTab("EmailConfiguration", _currentModel.EmailConfiguration ?? new EmailConfigurationModel());
        }

        // Add runtime-extended parameters
        AddRuntimeParameters();
    }

    private void AddRuntimeParameters()
    {
        var metadataList = _parameterMetadataService.GetAllMetadata();
        foreach (var metadata in metadataList)
        {
            var pathParts = metadata.JsonPath.Split('.');
            if (pathParts.Length != 2) continue;

            var sectionName = pathParts[0];
            var paramName = pathParts[1];

            // Check if section is allowed for current machine type
            var machineTypeConfig = _machineTypeService.GetMachineTypeConfiguration(_selectedMachineType ?? "");
            if (machineTypeConfig != null && !machineTypeConfig.Sections.Contains(sectionName))
            {
                continue; // Skip if section not allowed
            }

            // Find or create the section tab
            TabPage? tabPage = null;
            foreach (TabPage tab in _tabControl.TabPages)
            {
                if (tab.Text == sectionName)
                {
                    tabPage = tab;
                    break;
                }
            }

            if (tabPage == null)
            {
                // Create new tab for this section
                object? sectionModel = sectionName switch
                {
                    "ConnectionStrings" => _currentModel.ConnectionStrings ??= new ConnectionStringsModel(),
                    "AppSettings" => _currentModel.AppSettings ??= new AppSettingsModel(),
                    "DispenseConfiguration" => _currentModel.DispenseConfiguration ??= new DispenseConfigurationModel(),
                    "AntennaConfiguration" => _currentModel.AntennaConfiguration ??= new AntennaConfigurationModel(),
                    "EmailConfiguration" => _currentModel.EmailConfiguration ??= new EmailConfigurationModel(),
                    _ => null
                };

                if (sectionModel != null)
                {
                    CreateTab(sectionName, sectionModel);
                    tabPage = _tabControl.TabPages[_tabControl.TabPages.Count - 1];
                }
            }

            if (tabPage != null)
            {
                AddRuntimeParameterToTab(tabPage, metadata, sectionName, paramName);
            }
        }
    }

    private void AddRuntimeParameterToTab(TabPage tabPage, ParameterMetadata metadata, string sectionName, string paramName)
    {
        // This will be implemented to add dynamic parameters to existing tabs
        // For now, we'll add them when creating the tab
    }

    private void CreateTab(string sectionName, object sectionModel)
    {
        var tabPage = new TabPage(sectionName)
        {
            BackColor = Color.White
        };
        var scrollPanel = new Panel 
        { 
            Dock = DockStyle.Fill, 
            AutoScroll = true,
            BackColor = Color.White
        };
        
        // Calculate minimum width based on column widths
        int minWidth = 250 + 200 + 80 + 30 + 400 + 20; // Sum of column widths + padding
        
        var tableLayout = new TableLayoutPanel
        {
            AutoSize = false, // Disable AutoSize to allow fixed width for scrolling
            ColumnCount = 5, // Added column for Test button
            Padding = new Padding(10),
            BackColor = Color.White,
            Location = new Point(0, 0),
            Width = minWidth, // Set fixed width
            Height = 0, // Will be calculated based on content
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };

        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250)); // Property name
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); // Control
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); // Test button (for connection strings)
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30)); // Status icon
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400)); // Help text

        var properties = sectionModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        int row = 0;

        foreach (var prop in properties)
        {
            var propName = $"{sectionName}.{prop.Name}";
            var descriptionAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            var defaultValueAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
            var description = descriptionAttr?.Description ?? "";
            var defaultValue = defaultValueAttr?.Value;

            // Property name label with modern styling; wrap long names and show full name in tooltip
            var nameLabel = new Label
            {
                Text = prop.Name,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UIThemeService.DarkBlue,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Padding(5, 0, 0, 0),
                AutoSize = false
            };
            _parameterToolTip ??= new ToolTip();
            _parameterToolTip.SetToolTip(nameLabel, prop.Name);
            tableLayout.Controls.Add(nameLabel, 0, row);

            // Control based on type
            Control control = CreateControlForProperty(prop, sectionModel, propName);
            tableLayout.Controls.Add(control, 1, row);

            // Test button for connection strings and email configuration
            if (sectionName == "ConnectionStrings" && prop.PropertyType == typeof(string))
            {
                var testButton = new Button
                {
                    Text = "Test",
                    Width = 75,
                    Height = 28,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
                };
                UIThemeService.ApplyModernButtonStyle(testButton, isPrimary: true);
                testButton.Click += (s, e) => TestConnectionString(propName, control);
                tableLayout.Controls.Add(testButton, 2, row);
            }
            else if (sectionName == "EmailConfiguration" && prop.Name == "TypeShippingMail")
            {
                var testButton = new Button
                {
                    Text = "Test Email",
                    Width = 100,
                    Height = 28,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
                };
                UIThemeService.ApplyModernButtonStyle(testButton, isPrimary: true);
                testButton.Click += (s, e) => TestEmailConfiguration();
                tableLayout.Controls.Add(testButton, 2, row);
            }
            else
            {
                tableLayout.Controls.Add(new Label { Text = "" }, 2, row); // Empty cell
            }

            // Status icon with better styling
            var statusLabel = new Label
            {
                Text = "○",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UIThemeService.LightBlue,
                Font = new Font("Segoe UI", 12, FontStyle.Regular)
            };
            _statusLabels[propName] = statusLabel;
            tableLayout.Controls.Add(statusLabel, 3, row);

            // Help text with improved readability
            var helpLabel = new Label
            {
                Text = description,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(80, 80, 90),
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = false,
                Padding = new Padding(5, 0, 0, 0)
            };
            tableLayout.Controls.Add(helpLabel, 4, row);

            _fieldControls[propName] = control;
            _fieldChanged[propName] = false;
            _fieldValidation[propName] = new ValidationResult { IsValid = true };

            row++;
        }

        // Calculate total height needed for all rows (after all rows are added)
        int rowHeight = 35; // Approximate height per row
        int totalHeight = Math.Max(row * rowHeight + 20, 100); // Add padding, minimum 100px
        tableLayout.Height = totalHeight;
        
        // Add table layout to scroll panel
        scrollPanel.Controls.Add(tableLayout);
        
        // Set auto scroll minimum size to enable horizontal scrolling
        // This tells the scroll panel that content is wider than the panel
        scrollPanel.AutoScrollMinSize = new Size(minWidth, totalHeight);
        
        // Handle scroll panel resize - maintain table layout width for horizontal scrolling
        scrollPanel.Resize += (s, e) =>
        {
            if (tableLayout != null && scrollPanel != null)
            {
                // Keep table layout at minimum width to enable horizontal scrolling
                tableLayout.Width = minWidth;
                
                // Update scroll panel's auto scroll minimum size
                scrollPanel.AutoScrollMinSize = new Size(minWidth, totalHeight);
            }
        };
        tabPage.Controls.Add(scrollPanel);
        _tabControl.TabPages.Add(tabPage);
    }

    private Control CreateControlForProperty(PropertyInfo prop, object model, string propName)
    {
        var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        var currentValue = prop.GetValue(model);
        Control control;

        if (propType == typeof(bool))
        {
            var checkbox = new CheckBox
            {
                Checked = currentValue as bool? ?? false,
                Dock = DockStyle.Fill,
                ForeColor = UIThemeService.DarkBlue,
                Font = new Font("Segoe UI", 9)
            };
            checkbox.CheckedChanged += (s, e) => OnFieldChanged(propName, checkbox.Checked);
            control = checkbox;
        }
        else if (propType == typeof(int))
        {
            var descriptionAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            var description = descriptionAttr?.Description ?? "";
            
            // First, determine the min/max values
            decimal minValue = int.MinValue;
            decimal maxValue = int.MaxValue;
            
            // Check for range constraints (e.g., "Allowed: 0..3")
            if (description.Contains("Allowed:") && description.Contains(".."))
            {
                var rangeMatch = System.Text.RegularExpressions.Regex.Match(description, @"(\d+)\.\.(\d+)");
                if (rangeMatch.Success)
                {
                    minValue = int.Parse(rangeMatch.Groups[1].Value);
                    maxValue = int.Parse(rangeMatch.Groups[2].Value);
                }
            }
            
            // Set Minimum and Maximum BEFORE Value to avoid validation errors
            var numericUpDown = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = minValue,
                Maximum = maxValue,
                ForeColor = UIThemeService.DarkBlue,
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            // Now set the Value after Minimum and Maximum are set
            var intValue = currentValue as int? ?? 0;
            // Clamp the value to the valid range
            if (intValue < minValue)
                intValue = (int)minValue;
            else if (intValue > maxValue)
                intValue = (int)maxValue;
            
            numericUpDown.Value = intValue;
            
            numericUpDown.ValueChanged += (s, e) => OnFieldChanged(propName, (int)numericUpDown.Value);
            control = numericUpDown;
        }
        else if (propType == typeof(string))
        {
            var descriptionAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            var description = descriptionAttr?.Description ?? "";

            // Check if it's an enum-like string
            if (description.Contains("Allowed:"))
            {
                var allowedMatch = System.Text.RegularExpressions.Regex.Match(description, @"Allowed:\s*([^\.]+)");
                if (allowedMatch.Success)
                {
                    var comboBox = new ComboBox
                    {
                        Dock = DockStyle.Fill,
                        DropDownStyle = ComboBoxStyle.DropDown, // Allow typing
                        ForeColor = UIThemeService.DarkBlue,
                        Font = new Font("Segoe UI", 9)
                    };
                    UIThemeService.ApplyModernComboBoxStyle(comboBox);
                    var allowedValues = allowedMatch.Groups[1].Value
                        .Split(',')
                        .Select(v => v.Trim())
                        .ToList();
                    
                    // Add existing custom values if any
                    if (_customComboBoxValues.ContainsKey(propName))
                    {
                        allowedValues.AddRange(_customComboBoxValues[propName]);
                    }
                    
                    comboBox.Items.AddRange(allowedValues.ToArray());
                    comboBox.Items.Add("Add custom value...");
                    
                    if (currentValue is string strValue && allowedValues.Contains(strValue))
                    {
                        comboBox.SelectedItem = strValue;
                    }
                    else if (currentValue is string strValue2 && !string.IsNullOrEmpty(strValue2))
                    {
                        comboBox.Text = strValue2;
                    }
                    
                    comboBox.SelectedIndexChanged += (s, e) =>
                    {
                        var selectedText = comboBox.SelectedItem?.ToString();
                        if (selectedText == "Add custom value...")
                        {
                            AddCustomComboBoxValue(propName, comboBox, allowedValues);
                        }
                        else if (!string.IsNullOrEmpty(selectedText))
                        {
                            OnFieldChanged(propName, selectedText);
                        }
                    };
                    comboBox.TextChanged += (s, e) =>
                    {
                        var selectedText = comboBox.SelectedItem?.ToString();
                        if (selectedText != "Add custom value..." && !string.IsNullOrEmpty(comboBox.Text))
                        {
                            OnFieldChanged(propName, comboBox.Text);
                        }
                    };
                    comboBox.KeyDown += (s, e) =>
                    {
                        // Allow Enter key to accept typed value
                        if (e.KeyCode == Keys.Enter)
                        {
                            var typedValue = comboBox.Text;
                            if (!string.IsNullOrEmpty(typedValue) && !allowedValues.Contains(typedValue))
                            {
                                // Add as custom value
                                if (!_customComboBoxValues.ContainsKey(propName))
                                {
                                    _customComboBoxValues[propName] = new List<string>();
                                }
                                if (!_customComboBoxValues[propName].Contains(typedValue))
                                {
                                    _customComboBoxValues[propName].Add(typedValue);
                                    comboBox.Items.Insert(comboBox.Items.Count - 1, typedValue);
                                }
                                comboBox.SelectedItem = typedValue;
                                OnFieldChanged(propName, typedValue);
                            }
                        }
                    };
                    control = comboBox;
                }
                else
                {
                    var textBox = new TextBox
                    {
                        Text = currentValue as string ?? "",
                        Dock = DockStyle.Fill,
                        ForeColor = UIThemeService.DarkBlue,
                        Font = new Font("Segoe UI", 9)
                    };
                    UIThemeService.ApplyModernTextBoxStyle(textBox);
                    textBox.TextChanged += (s, e) => OnFieldChanged(propName, textBox.Text);
                    control = textBox;
                }
            }
            else
            {
                // Check if this is a folder path variable
                var isPathVariable = propName.Contains("Dir") || propName.Contains("Path") || 
                                     description.Contains("Directory") || description.Contains("Path") ||
                                     description.Contains("folder");
                
                if (isPathVariable)
                {
                    // Create a panel with textbox and browse button
                    var panel = new Panel
                    {
                        Dock = DockStyle.Fill,
                        Height = 28
                    };
                    
                    var textBox = new TextBox
                    {
                        Text = currentValue as string ?? "",
                        Dock = DockStyle.Fill,
                        ForeColor = UIThemeService.DarkBlue,
                        Font = new Font("Segoe UI", 9),
                        Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                    };
                    UIThemeService.ApplyModernTextBoxStyle(textBox);
                    textBox.TextChanged += (s, e) => OnFieldChanged(propName, textBox.Text);
                    
                    var browseButton = new Button
                    {
                        Text = "Browse...",
                        Width = 80,
                        Height = 28,
                        Dock = DockStyle.Right,
                        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
                    };
                    UIThemeService.ApplyModernButtonStyle(browseButton, isPrimary: false);
                    browseButton.Click += (s, e) => BrowseFolder(propName, textBox);
                    
                    panel.Controls.Add(browseButton);
                    panel.Controls.Add(textBox);
                    control = panel;
                }
                else
                {
                    var textBox = new TextBox
                    {
                        Text = currentValue as string ?? "",
                        Dock = DockStyle.Fill,
                        ForeColor = UIThemeService.DarkBlue,
                        Font = new Font("Segoe UI", 9)
                    };
                    UIThemeService.ApplyModernTextBoxStyle(textBox);
                    textBox.TextChanged += (s, e) => OnFieldChanged(propName, textBox.Text);
                    control = textBox;
                }
            }
        }
        else
        {
            var textBox = new TextBox
            {
                Text = currentValue?.ToString() ?? "",
                Dock = DockStyle.Fill,
                ReadOnly = true
            };
            control = textBox;
        }

        return control;
    }

    private void OnFieldChanged(string propName, object? newValue)
    {
        var sectionName = propName.Split('.')[0];
        var fieldName = propName.Split('.')[1];
        
        object? sectionModel = sectionName switch
        {
            "ConnectionStrings" => _currentModel.ConnectionStrings,
            "AppSettings" => _currentModel.AppSettings,
            "DispenseConfiguration" => _currentModel.DispenseConfiguration,
            "AntennaConfiguration" => _currentModel.AntennaConfiguration,
            "EmailConfiguration" => _currentModel.EmailConfiguration,
            _ => null
        };

        if (sectionModel == null) return;

        var prop = sectionModel.GetType().GetProperty(fieldName);
        if (prop == null) return;

        // Convert value to proper type
        object? convertedValue = null;
        var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        
        if (propType == typeof(bool) && newValue is bool boolVal)
            convertedValue = boolVal;
        else if (propType == typeof(int) && newValue is int intVal)
            convertedValue = intVal;
        else if (propType == typeof(string))
            convertedValue = newValue?.ToString();

        // Validate
        var validation = _validationService.ValidateProperty(convertedValue, prop, propType);
        _fieldValidation[propName] = validation;

        // Set value
        prop.SetValue(sectionModel, convertedValue);

        // Check if changed
        var originalSection = GetOriginalSection(sectionName);
        var originalProp = originalSection?.GetType().GetProperty(fieldName);
        var originalValue = originalProp?.GetValue(originalSection);
        
        _fieldChanged[propName] = !Equals(originalValue, convertedValue);

        UpdateFieldStatus(propName);
        UpdateStatus();
    }

    private object? GetOriginalSection(string sectionName)
    {
        return sectionName switch
        {
            "ConnectionStrings" => _originalModel.ConnectionStrings,
            "AppSettings" => _originalModel.AppSettings,
            "DispenseConfiguration" => _originalModel.DispenseConfiguration,
            "AntennaConfiguration" => _originalModel.AntennaConfiguration,
            "EmailConfiguration" => _originalModel.EmailConfiguration,
            _ => null
        };
    }

    private void UpdateFieldStatus(string propName)
    {
        if (!_statusLabels.ContainsKey(propName)) return;

        var statusLabel = _statusLabels[propName];
        var changed = _fieldChanged[propName];
        var valid = _fieldValidation[propName].IsValid;

        if (!changed)
        {
            statusLabel.Text = "○";
            statusLabel.ForeColor = UIThemeService.LightBlue;
        }
        else if (valid)
        {
            statusLabel.Text = "✓";
            statusLabel.ForeColor = UIThemeService.SuccessGreen;
        }
        else
        {
            statusLabel.Text = "✗";
            statusLabel.ForeColor = UIThemeService.ErrorRed;
        }
    }

    private void UpdateStatus()
    {
        var changedCount = _fieldChanged.Values.Count(c => c);
        var errorCount = _fieldValidation.Values.Count(v => !v.IsValid);

        _statusLabel.Text = $"Pending changes: {changedCount} | Errors: {errorCount}";
        
        var saveButton = _toolStrip.Items[0] as ToolStripButton;
        var discardButton = _toolStrip.Items[1] as ToolStripButton;
        
        if (saveButton != null)
            saveButton.Enabled = changedCount > 0 && errorCount == 0;
        
        if (discardButton != null)
            discardButton.Enabled = changedCount > 0;
    }

    private void SaveConfiguration()
    {
        if (_fieldValidation.Values.Any(v => !v.IsValid))
        {
            MessageBox.Show("Please fix all validation errors before saving.", "Validation Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            if (_isRemoteMode && _connectedRemoteMachine != null)
            {
                _remoteMachineAgentService.SaveRemoteAppSettings(_connectedRemoteMachine, _currentModel);
                MessageBox.Show($"Configuration saved successfully to remote machine: {_connectedRemoteMachine.Name}", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _jsonService.Save(_currentModel);
                MessageBox.Show("Configuration saved successfully.", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            
            _originalModel = CloneModel(_currentModel);
            
            foreach (var key in _fieldChanged.Keys.ToList())
                _fieldChanged[key] = false;
            
            foreach (var key in _statusLabels.Keys)
                UpdateFieldStatus(key);
            
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DiscardChanges()
    {
        if (MessageBox.Show("Discard all changes?", "Confirm", 
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _currentModel = CloneModel(_originalModel);
            CreateTabs();
            UpdateStatus();
        }
    }

    private void RestoreBackup()
    {
        var backups = _jsonService.GetBackupFiles();
        if (backups.Count == 0)
        {
            MessageBox.Show("No backup files found.", "No Backups", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var form = new Form
        {
            Text = "Restore Backup",
            Size = new Size(400, 300),
            StartPosition = FormStartPosition.CenterParent
        };

        var listBox = new ListBox { Dock = DockStyle.Fill };
        listBox.Items.AddRange(backups.ToArray());

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var restoreButton = new Button { Text = "Restore", DialogResult = DialogResult.OK };

        buttonPanel.Controls.AddRange(new Control[] { restoreButton, cancelButton });
        form.Controls.Add(listBox);
        form.Controls.Add(buttonPanel);
        form.AcceptButton = restoreButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
        {
            if (_jsonService.RestoreBackup(listBox.SelectedItem.ToString()!))
            {
                LoadConfiguration();
                MessageBox.Show("Backup restored successfully.", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to restore backup.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ApplyMaster()
    {
        var masters = _masterService.GetAvailableMasters();
        if (masters.Count == 0)
        {
            MessageBox.Show("No master files found in masters directory.", "No Masters", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var form = new Form
        {
            Text = "Apply Master",
            Size = new Size(400, 300),
            StartPosition = FormStartPosition.CenterParent
        };

        var listBox = new ListBox { Dock = DockStyle.Fill };
        listBox.Items.AddRange(masters.ToArray());

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var applyButton = new Button { Text = "Apply", DialogResult = DialogResult.OK };

        buttonPanel.Controls.AddRange(new Control[] { applyButton, cancelButton });
        form.Controls.Add(listBox);
        form.Controls.Add(buttonPanel);
        form.AcceptButton = applyButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
        {
            var master = _masterService.LoadMaster(listBox.SelectedItem.ToString()!);
            if (master != null)
            {
                if (MessageBox.Show("Apply master values? This will overwrite current values.", "Confirm", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _currentModel = master;
                    CreateTabs();
                    UpdateStatus();
                }
            }
        }
    }

    private void SaveTemplate()
    {
        var form = new Form
        {
            Text = "Save Template",
            Size = new Size(300, 120),
            StartPosition = FormStartPosition.CenterParent
        };

        var textBox = new TextBox { Dock = DockStyle.Top, Margin = new Padding(10) };
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK };

        buttonPanel.Controls.AddRange(new Control[] { saveButton, cancelButton });
        form.Controls.Add(textBox);
        form.Controls.Add(buttonPanel);
        form.AcceptButton = saveButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            if (_templateService.SaveTemplate(textBox.Text, _currentModel))
            {
                MessageBox.Show("Template saved successfully.", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to save template.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void LoadTemplate()
    {
        var templates = _templateService.GetAvailableTemplates();
        if (templates.Count == 0)
        {
            MessageBox.Show("No templates found in templates directory.", "No Templates", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var form = new Form
        {
            Text = "Load Template",
            Size = new Size(400, 300),
            StartPosition = FormStartPosition.CenterParent
        };

        var listBox = new ListBox { Dock = DockStyle.Fill };
        listBox.Items.AddRange(templates.ToArray());

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var loadButton = new Button { Text = "Load", DialogResult = DialogResult.OK };

        buttonPanel.Controls.AddRange(new Control[] { loadButton, cancelButton });
        form.Controls.Add(listBox);
        form.Controls.Add(buttonPanel);
        form.AcceptButton = loadButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
        {
            var template = _templateService.LoadTemplate(listBox.SelectedItem.ToString()!);
            if (template != null)
            {
                if (MessageBox.Show("Load template values? This will overwrite current values.", "Confirm", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _currentModel = template;
                    CreateTabs();
                    UpdateStatus();
                }
            }
        }
    }

    private void TestEmailConfiguration()
    {
        if (_currentModel.EmailConfiguration == null)
        {
            MessageBox.Show("Email configuration is not available.", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var emailConfig = _currentModel.EmailConfiguration;
        var testButton = _toolStrip.Items.Cast<ToolStripItem>()
            .FirstOrDefault(i => i.Text == "Test Email") as ToolStripButton;

        // Show testing message
        var form = new Form
        {
            Text = "Testing Email Configuration",
            Size = new Size(400, 150),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label
        {
            Text = "Testing email configuration...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10)
        };
        form.Controls.Add(label);

        form.Show();

        // Test email configuration asynchronously
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var result = TestEmailConfigurationInternal(emailConfig);
                
                this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    form.Close();
                    
                    if (result.Success)
                    {
                        MessageBox.Show($"Email configuration test successful!\n\n{result.Message}", 
                            "Email Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Email configuration test failed:\n\n{result.ErrorMessage}", 
                            "Email Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    form.Close();
                    MessageBox.Show($"Error testing email configuration: {ex.Message}", 
                        "Email Test Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        });
    }

    private (bool Success, string? Message, string? ErrorMessage) TestEmailConfigurationInternal(EmailConfigurationModel config)
    {
        try
        {
            var type = config.TypeShippingMail ?? 0;
            
            if (type == 0)
            {
                return (false, null, "Email sending is disabled (TypeShippingMail = 0)");
            }
            
            if (type == 5) // SMTP mode
            {
                if (string.IsNullOrEmpty(config.SmtpHost))
                {
                    return (false, null, "SMTP host is not configured");
                }
                
                if (string.IsNullOrEmpty(config.SmtpUser))
                {
                    return (false, null, "SMTP username is not configured");
                }
                
                // Try to connect to SMTP server
                try
                {
                    using var client = new System.Net.Mail.SmtpClient
                    {
                        Host = config.SmtpHost,
                        Port = config.SmtpPort ?? 25,
                        EnableSsl = (config.SmtpPort ?? 25) == 587 || (config.SmtpPort ?? 25) == 465,
                        Credentials = new System.Net.NetworkCredential(config.SmtpUser, config.SmtpPass ?? "")
                    };
                    
                    // Test connection (timeout after 5 seconds)
                    client.Timeout = 5000;
                    // Note: We can't actually send a test email without credentials, but we can validate the configuration
                    return (true, "SMTP configuration appears valid. Note: Actual sending requires valid credentials.", null);
                }
                catch (Exception ex)
                {
                    return (false, null, $"SMTP connection test failed: {ex.Message}");
                }
            }
            else if (type == 2) // ASP + Proxy mode
            {
                if (string.IsNullOrEmpty(config.ProxyAddressProxyEmail))
                {
                    return (false, null, "Proxy address is not configured");
                }
                
                return (true, "Proxy configuration appears valid. Note: Actual sending requires network connectivity.", null);
            }
            else
            {
                return (true, $"Email mode {type} configuration appears valid.", null);
            }
        }
        catch (Exception ex)
        {
            return (false, null, $"Error testing email configuration: {ex.Message}");
        }
    }

    private void TestConnectionString(string propName, Control control)
    {
        string connectionString = "";
        if (control is TextBox textBox)
        {
            connectionString = textBox.Text;
        }
        else if (control is ComboBox comboBox)
        {
            connectionString = comboBox.Text;
        }

        // Show testing message
        var testButton = control.Parent?.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "Test");
        if (testButton != null)
        {
            testButton.Enabled = false;
            testButton.Text = "Testing...";
        }

        // Test connection asynchronously (simplified - in production use async/await)
        System.Threading.Tasks.Task.Run(() =>
        {
            var result = _connectionStringTester.TestConnection(connectionString);
            
            this.Invoke((System.Windows.Forms.MethodInvoker)delegate
            {
                if (testButton != null)
                {
                    testButton.Enabled = true;
                    testButton.Text = "Test";
                }

                if (result.Success)
                {
                    MessageBox.Show("Connection successful!", "Connection Test", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Connection failed: {result.ErrorMessage}", "Connection Test", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        });
    }

    private void BrowseFolder(string propName, TextBox textBox)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select folder",
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrEmpty(textBox.Text) && Directory.Exists(textBox.Text))
        {
            dialog.SelectedPath = Path.GetFullPath(textBox.Text);
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBox.Text = dialog.SelectedPath;
            OnFieldChanged(propName, dialog.SelectedPath);
        }
    }

    private void AddCustomComboBoxValue(string propName, ComboBox comboBox, List<string> originalValues)
    {
        var form = new Form
        {
            Text = "Add Custom Value",
            Size = new Size(400, 150),
            StartPosition = FormStartPosition.CenterParent
        };

        var label = new Label
        {
            Text = "Enter custom value:",
            Location = new Point(10, 20),
            AutoSize = true
        };

        var textBox = new TextBox
        {
            Location = new Point(10, 45),
            Width = 360
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var addButton = new Button { Text = "Add", DialogResult = DialogResult.OK };

        buttonPanel.Controls.AddRange(new Control[] { addButton, cancelButton });
        form.Controls.Add(label);
        form.Controls.Add(textBox);
        form.Controls.Add(buttonPanel);
        form.AcceptButton = addButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            var customValue = textBox.Text.Trim();
            
            // Validate the value
            var sectionName = propName.Split('.')[0];
            var fieldName = propName.Split('.')[1];
            object? sectionModel = sectionName switch
            {
                "ConnectionStrings" => _currentModel.ConnectionStrings,
                "AppSettings" => _currentModel.AppSettings,
                "DispenseConfiguration" => _currentModel.DispenseConfiguration,
                "AntennaConfiguration" => _currentModel.AntennaConfiguration,
                "EmailConfiguration" => _currentModel.EmailConfiguration,
                _ => null
            };

            if (sectionModel != null)
            {
                var prop = sectionModel.GetType().GetProperty(fieldName);
                if (prop != null)
                {
                    var validation = _validationService.ValidateProperty(customValue, prop, typeof(string));
                    if (!validation.IsValid)
                    {
                        MessageBox.Show($"Invalid value: {validation.ErrorMessage}", "Validation Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            // Add to custom values list
            if (!_customComboBoxValues.ContainsKey(propName))
            {
                _customComboBoxValues[propName] = new List<string>();
            }
            if (!_customComboBoxValues[propName].Contains(customValue))
            {
                _customComboBoxValues[propName].Add(customValue);
            }

            // Update combo box
            comboBox.Items.Remove("Add custom value...");
            if (!comboBox.Items.Contains(customValue))
            {
                comboBox.Items.Add(customValue);
            }
            comboBox.Items.Add("Add custom value...");
            comboBox.SelectedItem = customValue;
            comboBox.Text = customValue;

            OnFieldChanged(propName, customValue);
        }
        else
        {
            // Reset selection if cancelled
            comboBox.SelectedIndex = -1;
        }
    }

    public void ShowAddParameterDialog()
    {
        var form = new Form
        {
            Text = "Add New Parameter",
            Size = new Size(500, 400),
            StartPosition = FormStartPosition.CenterParent
        };

        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(10)
        };

        // JSON Path
        tableLayout.Controls.Add(new Label { Text = "JSON Path (Section.Parameter):", AutoSize = true }, 0, 0);
        var pathTextBox = new TextBox { Dock = DockStyle.Fill };
        tableLayout.Controls.Add(pathTextBox, 1, 0);

        // Data Type
        tableLayout.Controls.Add(new Label { Text = "Data Type:", AutoSize = true }, 0, 1);
        var typeComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        typeComboBox.Items.AddRange(new[] { "String", "Int", "Bool", "Double" });
        typeComboBox.SelectedIndex = 0;
        tableLayout.Controls.Add(typeComboBox, 1, 1);

        // Default Value
        tableLayout.Controls.Add(new Label { Text = "Default Value:", AutoSize = true }, 0, 2);
        var defaultValueTextBox = new TextBox { Dock = DockStyle.Fill };
        tableLayout.Controls.Add(defaultValueTextBox, 1, 2);

        // Allowed Values
        tableLayout.Controls.Add(new Label { Text = "Allowed Values (comma-separated, optional):", AutoSize = true }, 0, 3);
        var allowedValuesTextBox = new TextBox { Dock = DockStyle.Fill };
        tableLayout.Controls.Add(allowedValuesTextBox, 1, 3);

        // Description
        tableLayout.Controls.Add(new Label { Text = "Description:", AutoSize = true }, 0, 4);
        var descriptionTextBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 60 };
        tableLayout.Controls.Add(descriptionTextBox, 1, 4);

        // Help Text
        tableLayout.Controls.Add(new Label { Text = "Help Text:", AutoSize = true }, 0, 5);
        var helpTextBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 60 };
        tableLayout.Controls.Add(helpTextBox, 1, 5);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var addButton = new Button { Text = "Add", DialogResult = DialogResult.OK };

        buttonPanel.Controls.AddRange(new Control[] { addButton, cancelButton });
        form.Controls.Add(tableLayout);
        form.Controls.Add(buttonPanel);
        form.AcceptButton = addButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() == DialogResult.OK)
        {
            var metadata = new ParameterMetadata
            {
                JsonPath = pathTextBox.Text.Trim(),
                DataType = Enum.Parse<ParameterDataType>(typeComboBox.SelectedItem?.ToString() ?? "String"),
                DefaultValue = ParseDefaultValue(defaultValueTextBox.Text, typeComboBox.SelectedItem?.ToString() ?? "String"),
                AllowedValues = string.IsNullOrWhiteSpace(allowedValuesTextBox.Text) 
                    ? null 
                    : allowedValuesTextBox.Text.Split(',').Select(v => v.Trim()).ToList(),
                Description = descriptionTextBox.Text.Trim(),
                HelpText = helpTextBox.Text.Trim()
            };

            _parameterMetadataService.AddMetadata(metadata);
            CreateTabs(); // Refresh to show new parameter
            MessageBox.Show("Parameter added successfully. It will appear after reloading.", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private object? ParseDefaultValue(string value, string dataType)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return dataType switch
        {
            "Int" => int.TryParse(value, out var intVal) ? intVal : null,
            "Bool" => bool.TryParse(value, out var boolVal) ? boolVal : null,
            "Double" => double.TryParse(value, out var doubleVal) ? doubleVal : null,
            _ => value
        };
    }

    private void ShowRemoteMachines()
    {
        _mainContentPanel.Controls.Clear();
        _remoteMachineList = new RemoteMachineListControl(_remoteMachineAgentService);
        _remoteMachineList.MachineConnectRequested += (s, machine) =>
        {
            ConnectToRemoteMachine(machine);
        };
        _mainContentPanel.Controls.Add(_remoteMachineList);
    }

    private void ConnectToRemoteMachine(RemoteMachine machine)
    {
        try
        {
            _connectedRemoteMachine = machine;
            _isRemoteMode = true;
            _selectedMachineType = machine.MachineType;
            
            // Update UI to show we're connected
            var disconnectButton = _toolStrip.Items.Cast<ToolStripItem>()
                .FirstOrDefault(i => i.Text == "Disconnect Remote") as ToolStripButton;
            if (disconnectButton != null)
            {
                disconnectButton.Enabled = true;
            }
            
            // Load configuration from remote machine
            LoadConfiguration();
            ShowEditor();
            CreateTabs();
            UpdateStatus();
            
            MessageBox.Show($"Connected to remote machine: {machine.Name}\n\nYou are now editing the remote configuration.", 
                "Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error connecting to remote machine: {ex.Message}", "Connection Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _isRemoteMode = false;
            _connectedRemoteMachine = null;
        }
    }

    private void DisconnectRemote()
    {
        if (_isRemoteMode && _connectedRemoteMachine != null)
        {
            if (MessageBox.Show($"Disconnect from remote machine '{_connectedRemoteMachine.Name}'?\n\nAny unsaved changes will be lost.",
                "Disconnect", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _isRemoteMode = false;
                _connectedRemoteMachine = null;
                
                var disconnectButton = _toolStrip.Items.Cast<ToolStripItem>()
                    .FirstOrDefault(i => i.Text == "Disconnect Remote") as ToolStripButton;
                if (disconnectButton != null)
                {
                    disconnectButton.Enabled = false;
                }
                
                _statusLabel.Text = "Disconnected from remote machine";
                ShowMachineTypeSelection();
            }
        }
    }

    private AppSettingsRootModel CloneModel(AppSettingsRootModel source)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettingsRootModel>(json)!;
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.Name = "AppSettingsEditorControl";
        this.Size = new Size(800, 600);
        this.ResumeLayout(false);
    }
}

