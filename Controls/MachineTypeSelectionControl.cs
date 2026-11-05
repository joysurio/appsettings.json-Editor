using AppSettingsEditor.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppSettingsEditor.Controls;

internal class MachineTypeSelectionControl : UserControl
{
    public event EventHandler<string>? MachineTypeSelected;

    private MachineTypeService _machineTypeService;
    private ComboBox _machineTypeComboBox = null!;
    private Button _continueButton = null!;

    public MachineTypeSelectionControl(MachineTypeService machineTypeService)
    {
        _machineTypeService = machineTypeService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = UIThemeService.LightGray;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(50)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        // Title with modern styling
        var titleLabel = new Label
        {
            Text = "Select Machine Type",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = UIThemeService.DarkBlue,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        mainPanel.Controls.Add(titleLabel, 0, 0);

        // Selection panel
        var selectionPanel = new Panel
        {
            Dock = DockStyle.Fill
        };

        var label = new Label
        {
            Text = "Machine Type:",
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            ForeColor = UIThemeService.DarkBlue,
            AutoSize = true,
            Location = new Point(0, 15)
        };

        _machineTypeComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10),
            Width = 320,
            Height = 32,
            Location = new Point(130, 12)
        };
        UIThemeService.ApplyModernComboBoxStyle(_machineTypeComboBox);

        var machineTypes = _machineTypeService.GetAvailableMachineTypes();
        foreach (var mt in machineTypes)
        {
            _machineTypeComboBox.Items.Add(new { Display = mt.DisplayName, Value = mt.MachineType });
        }
        _machineTypeComboBox.DisplayMember = "Display";
        _machineTypeComboBox.ValueMember = "Value";

        if (_machineTypeComboBox.Items.Count > 0)
        {
            _machineTypeComboBox.SelectedIndex = 0;
        }

        _continueButton = new Button
        {
            Text = "Continue →",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Width = 160,
            Height = 38,
            Location = new Point(460, 10)
        };
        UIThemeService.ApplyModernButtonStyle(_continueButton, isPrimary: true);
        _continueButton.Click += ContinueButton_Click;

        selectionPanel.Controls.Add(label);
        selectionPanel.Controls.Add(_machineTypeComboBox);
        selectionPanel.Controls.Add(_continueButton);

        mainPanel.Controls.Add(selectionPanel, 0, 1);

        this.Controls.Add(mainPanel);
    }

    private void ContinueButton_Click(object? sender, EventArgs e)
    {
        if (_machineTypeComboBox.SelectedItem != null)
        {
            var selectedItem = _machineTypeComboBox.SelectedItem;
            var machineType = selectedItem.GetType().GetProperty("Value")?.GetValue(selectedItem)?.ToString();
            if (!string.IsNullOrEmpty(machineType))
            {
                MachineTypeSelected?.Invoke(this, machineType);
            }
        }
    }
}

