using AppSettingsEditor.Models;
using AppSettingsEditor.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppSettingsEditor.Controls;

internal partial class RemoteMachineListControl : UserControl
{
    private readonly RemoteMachineAgentService _agentService;
    private ListView _machineListView = null!;
    private ToolStrip _toolStrip = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;

    public event EventHandler<RemoteMachine>? MachineConnectRequested;

    public RemoteMachineListControl(RemoteMachineAgentService agentService)
    {
        _agentService = agentService;
        InitializeComponent();
        InitializeUI();
        RefreshMachineList();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.Name = "RemoteMachineListControl";
        this.Size = new Size(800, 600);
        this.ResumeLayout(false);
    }

    private void InitializeUI()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = UIThemeService.LightGray;

        // ToolStrip with modern style
        _toolStrip = new ToolStrip
        {
            BackColor = UIThemeService.PrimaryBlue,
            ForeColor = UIThemeService.White,
            Renderer = new ModernToolStripRenderer()
        };
        _toolStrip.Items.Add(new ToolStripButton("➕ Add Machine...", null, (s, e) => AddMachine()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripButton("✏️ Edit...", null, (s, e) => EditMachine()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripButton("🗑️ Remove", null, (s, e) => RemoveMachine()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripButton("🔍 Test Connection", null, (s, e) => TestConnection()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripButton("🔌 Connect", null, (s, e) => ConnectToMachine()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripButton("🔄 Refresh", null, (s, e) => RefreshMachineList()) { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) });

        // ListView with modern styling
        _machineListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BackColor = UIThemeService.White,
            ForeColor = UIThemeService.DarkBlue,
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.None,
            UseCompatibleStateImageBehavior = false,
            OwnerDraw = true
        };
        _machineListView.DrawItem += (s, e) => DrawListViewItem(e);
        _machineListView.DrawSubItem += (s, e) => DrawListViewSubItem(e);
        _machineListView.DrawColumnHeader += (s, e) => DrawListViewColumnHeader(e);
        
        _machineListView.Columns.Add("Name", 180);
        _machineListView.Columns.Add("Address", 140);
        _machineListView.Columns.Add("Connection Method", 150);
        _machineListView.Columns.Add("Status", 120);
        _machineListView.Columns.Add("Machine Type", 130);
        _machineListView.Columns.Add("Last Connection", 180);
        _machineListView.SelectedIndexChanged += (s, e) => UpdateToolStripButtons();
        _machineListView.DoubleClick += (s, e) => ConnectToMachine();

        // StatusStrip with modern style
        _statusStrip = new StatusStrip
        {
            BackColor = UIThemeService.PrimaryBlue,
            ForeColor = UIThemeService.White,
            Renderer = new ModernToolStripRenderer()
        };
        _statusLabel = new ToolStripStatusLabel("Ready") { ForeColor = UIThemeService.White, Font = new Font("Segoe UI", 9) };
        _statusStrip.Items.Add(_statusLabel);

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
        mainPanel.Controls.Add(_machineListView, 0, 1);
        mainPanel.Controls.Add(_statusStrip, 0, 2);

        this.Controls.Add(mainPanel);
        UpdateToolStripButtons();
    }

    private void RefreshMachineList()
    {
        _machineListView.Items.Clear();
        var machines = _agentService.GetAllMachines();

        foreach (var machine in machines)
        {
            var item = new ListViewItem(machine.Name);
            item.SubItems.Add(machine.Address);
            item.SubItems.Add(machine.ConnectionMethod);
            item.SubItems.Add(machine.LastStatus.ToString());
            item.SubItems.Add(machine.MachineType ?? "");
            item.SubItems.Add(machine.LastConnectionAttempt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
            item.Tag = machine;

            // Color code by status (stored in tag for drawing)
            item.Tag = machine;

            _machineListView.Items.Add(item);
        }

        _statusLabel.Text = $"Total machines: {machines.Count}";
        UpdateToolStripButtons();
    }

    private void AddMachine()
    {
        var machine = new RemoteMachine();
        if (ShowMachineEditDialog(machine, "Add Remote Machine"))
        {
            try
            {
                _agentService.AddMachine(machine);
                RefreshMachineList();
                MessageBox.Show("Machine added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding machine: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void EditMachine()
    {
        var selected = GetSelectedMachine();
        if (selected == null)
        {
            MessageBox.Show("Please select a machine to edit.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var machineCopy = CloneMachine(selected);
        if (ShowMachineEditDialog(machineCopy, "Edit Remote Machine"))
        {
            try
            {
                _agentService.UpdateMachine(machineCopy);
                RefreshMachineList();
                MessageBox.Show("Machine updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating machine: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void RemoveMachine()
    {
        var selected = GetSelectedMachine();
        if (selected == null)
        {
            MessageBox.Show("Please select a machine to remove.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show($"Are you sure you want to remove '{selected.Name}'?", "Confirm Removal",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try
            {
                _agentService.RemoveMachine(selected.Id);
                RefreshMachineList();
                MessageBox.Show("Machine removed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing machine: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void TestConnection()
    {
        var selected = GetSelectedMachine();
        if (selected == null)
        {
            MessageBox.Show("Please select a machine to test.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _statusLabel.Text = $"Testing connection to {selected.Name}...";
        Application.DoEvents();

        try
        {
            var result = _agentService.TestConnection(selected);
            RefreshMachineList();

            if (result.Success)
            {
                MessageBox.Show($"Connection successful!{Environment.NewLine}{result.Message ?? ""}", "Connection Test",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Connection failed: {result.ErrorMessage}", "Connection Test",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error testing connection: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _statusLabel.Text = "Ready";
        }
    }

    private void ConnectToMachine()
    {
        var selected = GetSelectedMachine();
        if (selected == null)
        {
            MessageBox.Show("Please select a machine to connect to.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Test connection first
        _statusLabel.Text = $"Connecting to {selected.Name}...";
        Application.DoEvents();

        var testResult = _agentService.TestConnection(selected);
        RefreshMachineList();

        if (!testResult.Success)
        {
            MessageBox.Show($"Cannot connect to machine: {testResult.ErrorMessage}\n\nPlease check the connection settings and try again.",
                "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Ready";
            return;
        }

        MachineConnectRequested?.Invoke(this, selected);
        _statusLabel.Text = $"Connected to {selected.Name}";
    }

    private RemoteMachine? GetSelectedMachine()
    {
        if (_machineListView.SelectedItems.Count == 0)
            return null;

        return _machineListView.SelectedItems[0].Tag as RemoteMachine;
    }

    private void UpdateToolStripButtons()
    {
        var hasSelection = _machineListView.SelectedItems.Count > 0;
        _toolStrip.Items[1].Enabled = hasSelection; // Edit
        _toolStrip.Items[2].Enabled = hasSelection; // Remove
        _toolStrip.Items[4].Enabled = hasSelection; // Test Connection
        _toolStrip.Items[5].Enabled = hasSelection; // Connect
    }

    private bool ShowMachineEditDialog(RemoteMachine machine, string title)
    {
        var form = new Form
        {
            Text = title,
            Size = new Size(500, 450),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(10)
        };

        // Name
        tableLayout.Controls.Add(new Label { Text = "Name:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        var nameTextBox = new TextBox { Dock = DockStyle.Fill, Text = machine.Name };
        tableLayout.Controls.Add(nameTextBox, 1, 0);

        // Address
        tableLayout.Controls.Add(new Label { Text = "Address (IP/Hostname):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        var addressTextBox = new TextBox { Dock = DockStyle.Fill, Text = machine.Address };
        tableLayout.Controls.Add(addressTextBox, 1, 1);

        // Connection Method
        tableLayout.Controls.Add(new Label { Text = "Connection Method:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        var methodComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        methodComboBox.Items.AddRange(new[] { "NetworkShare", "RustDesk", "RDP" });
        methodComboBox.SelectedItem = machine.ConnectionMethod;
        if (methodComboBox.SelectedItem == null) methodComboBox.SelectedIndex = 0;
        tableLayout.Controls.Add(methodComboBox, 1, 2);

        // Network Share Path
        tableLayout.Controls.Add(new Label { Text = "Network Share Path:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        var sharePathTextBox = new TextBox { Dock = DockStyle.Fill, Text = machine.NetworkSharePath ?? "" };
        tableLayout.Controls.Add(sharePathTextBox, 1, 3);

        // Username
        tableLayout.Controls.Add(new Label { Text = "Username (optional):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        var usernameTextBox = new TextBox { Dock = DockStyle.Fill, Text = machine.Username ?? "" };
        tableLayout.Controls.Add(usernameTextBox, 1, 4);

        // Password
        tableLayout.Controls.Add(new Label { Text = "Password (optional):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        var passwordTextBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Text = machine.Password ?? "" };
        tableLayout.Controls.Add(passwordTextBox, 1, 5);

        // RustDesk ID
        tableLayout.Controls.Add(new Label { Text = "RustDesk ID (if using RustDesk):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        var rustDeskIdTextBox = new TextBox { Dock = DockStyle.Fill, Text = machine.RustDeskId ?? "" };
        tableLayout.Controls.Add(rustDeskIdTextBox, 1, 6);

        // Remote AppSettings Path
        tableLayout.Controls.Add(new Label { Text = "Remote appsettings.json Path:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        var remotePathTextBox = new TextBox { Dock = DockStyle.Fill, Text = machine.RemoteAppSettingsPath };
        tableLayout.Controls.Add(remotePathTextBox, 1, 7);

        // Machine Type
        tableLayout.Controls.Add(new Label { Text = "Machine Type:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
        var machineTypeTextBox = new TextBox { Dock = DockStyle.Fill, Text = machine.MachineType ?? "" };
        tableLayout.Controls.Add(machineTypeTextBox, 1, 8);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK };

        buttonPanel.Controls.AddRange(new Control[] { okButton, cancelButton });
        form.Controls.Add(tableLayout);
        form.Controls.Add(buttonPanel);
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() == DialogResult.OK)
        {
            machine.Name = nameTextBox.Text.Trim();
            machine.Address = addressTextBox.Text.Trim();
            machine.ConnectionMethod = methodComboBox.SelectedItem?.ToString() ?? "NetworkShare";
            machine.NetworkSharePath = sharePathTextBox.Text.Trim();
            machine.Username = usernameTextBox.Text.Trim();
            machine.Password = passwordTextBox.Text;
            machine.RustDeskId = rustDeskIdTextBox.Text.Trim();
            machine.RemoteAppSettingsPath = remotePathTextBox.Text.Trim();
            machine.MachineType = machineTypeTextBox.Text.Trim();
            return true;
        }

        return false;
    }

    private RemoteMachine CloneMachine(RemoteMachine source)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<RemoteMachine>(json)!;
    }

    private void DrawListViewItem(DrawListViewItemEventArgs e)
    {
        e.DrawDefault = false;
        var item = e.Item;
        var machine = item.Tag as RemoteMachine;
        
        // Background
        Color backColor = e.Item.Selected 
            ? UIThemeService.LighterBlue 
            : (e.ItemIndex % 2 == 0 ? UIThemeService.White : UIThemeService.LightGray);
        
        using (var brush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }
        
        // Draw first subitem (Name)
        if (item.SubItems.Count > 0)
        {
            var textColor = machine?.LastStatus switch
            {
                ConnectionStatus.Connected => UIThemeService.SuccessGreen,
                ConnectionStatus.Error => UIThemeService.ErrorRed,
                ConnectionStatus.Connecting => UIThemeService.WarningOrange,
                _ => UIThemeService.DarkBlue
            };
            
            var font = new Font("Segoe UI", 9, FontStyle.Regular);
            TextRenderer.DrawText(e.Graphics, item.Text, font,
                e.Bounds, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private void DrawListViewSubItem(DrawListViewSubItemEventArgs e)
    {
        if (e.ColumnIndex == 0) return; // Already drawn in DrawListViewItem
        
        var item = e.Item;
        var machine = item.Tag as RemoteMachine;
        
        Color textColor = UIThemeService.DarkBlue;
        if (e.ColumnIndex == 3 && machine != null) // Status column
        {
            textColor = machine.LastStatus switch
            {
                ConnectionStatus.Connected => UIThemeService.SuccessGreen,
                ConnectionStatus.Error => UIThemeService.ErrorRed,
                ConnectionStatus.Connecting => UIThemeService.WarningOrange,
                _ => Color.Gray
            };
        }
        
        var text = e.SubItem?.Text ?? "";
        var font = new Font("Segoe UI", 9);
        TextRenderer.DrawText(e.Graphics, text, font,
            e.Bounds, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void DrawListViewColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        e.DrawDefault = false;
        
        // Gradient background for header
        using (var brush = new LinearGradientBrush(e.Bounds, UIThemeService.PrimaryBlue, UIThemeService.AccentBlue, 90f))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }
        
        // Border
        using (var pen = new Pen(UIThemeService.BorderGray, 1))
        {
            e.Graphics.DrawRectangle(pen, e.Bounds);
        }
        
        // Text
        var headerText = e.Header?.Text ?? "";
        var font = new Font("Segoe UI", 9, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, headerText, font,
            e.Bounds, UIThemeService.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

