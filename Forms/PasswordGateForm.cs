using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppSettingsEditor.Forms;

internal enum PasswordGatePurpose
{
    /// <summary>Open main UI after optional cancel (stay in tray).</summary>
    UnlockSettings,
    /// <summary>Confirm application exit; cancel keeps the app running.</summary>
    ConfirmExit
}

internal sealed class PasswordGateForm : Form
{
    private const string RequiredCode = "9999";
    private const int DialogButtonHeight = 34;
    private static readonly Color PrimaryButtonBorder = Color.FromArgb(0, 80, 150);
    private readonly TextBox _codeTextBox;
    private readonly Label _statusLabel;
    private readonly Button _primaryButton;
    private bool _authenticated;

    public PasswordGateForm(PasswordGatePurpose purpose = PasswordGatePurpose.UnlockSettings)
    {
        _authenticated = false;

        Text = purpose == PasswordGatePurpose.ConfirmExit
            ? "Confirm exit"
            : "Authentication";

        Size = new Size(420, 280);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = true;
        TopMost = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 5
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = purpose == PasswordGatePurpose.ConfirmExit
                ? "Enter access code to exit the application"
                : "Enter access code",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold)
        };
        layout.Controls.Add(title, 0, 0);

        _codeTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 13.5f),
            PasswordChar = '*',
            MaxLength = 8,
            TextAlign = HorizontalAlignment.Center
        };
        layout.Controls.Add(_codeTextBox, 0, 1);

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Firebrick,
            Text = ""
        };
        layout.Controls.Add(_statusLabel, 0, 2);

        // Same font + zero vertical margins on both; Cancel had explicit horizontal gap while
        // primary kept WinForms' default ~3px vertical margin and looked shorter / misaligned.
        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.None,
            Margin = new Padding(0, 0, 10, 0),
            Padding = Padding.Empty,
            TextAlign = ContentAlignment.MiddleCenter,
            UseCompatibleTextRendering = false,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = SystemColors.ControlText
        };
        cancelButton.FlatAppearance.BorderColor = SystemColors.ControlDark;
        cancelButton.FlatAppearance.BorderSize = 1;
        cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 245, 245);
        cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 235, 235);
        ApplyDialogButtonSize(cancelButton, minWidth: 84, DialogButtonHeight);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _primaryButton = new Button
        {
            Text = purpose == PasswordGatePurpose.ConfirmExit ? "Exit" : "Unlock",
            DialogResult = DialogResult.None,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            TextAlign = ContentAlignment.MiddleCenter,
            UseCompatibleTextRendering = false
        };
        _primaryButton.Click += (_, _) => TryUnlock();
        AcceptButton = _primaryButton;

        StyleAccentButton(_primaryButton);

        // TableLayoutPanel + fixed row height + Dock Fill keeps both buttons the same height (FlowLayoutPanel misaligns mixed flat styles).
        cancelButton.Dock = DockStyle.Fill;
        _primaryButton.Dock = DockStyle.Fill;

        var buttonStrip = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Anchor = AnchorStyles.None,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        buttonStrip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonStrip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonStrip.RowStyles.Add(new RowStyle(SizeType.Absolute, DialogButtonHeight));
        buttonStrip.Controls.Add(cancelButton, 0, 0);
        buttonStrip.Controls.Add(_primaryButton, 1, 0);

        var buttonPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };
        buttonPanel.Controls.Add(buttonStrip);
        void CenterButtonRow()
        {
            buttonStrip.Left = Math.Max(0, (buttonPanel.ClientSize.Width - buttonStrip.Width) / 2);
            buttonStrip.Top = Math.Max(0, (buttonPanel.ClientSize.Height - buttonStrip.Height) / 2);
        }

        buttonPanel.Layout += (_, _) => CenterButtonRow();
        layout.Controls.Add(buttonPanel, 0, 3);

        var spacer = new Panel { Dock = DockStyle.Fill, Height = 8 };
        layout.Controls.Add(spacer, 0, 4);

        Controls.Add(layout);
        CancelButton = cancelButton;

        Shown += (_, _) =>
        {
            CenterButtonRow();
            _codeTextBox.Focus();
        };
    }

    /// <summary>
    /// GDI text (UseCompatibleTextRendering = false) + fixed height centers label text reliably on Flat buttons.
    /// </summary>
    private static void ApplyDialogButtonSize(Button b, int minWidth, int heightPx = DialogButtonHeight)
    {
        b.AutoSize = false;
        var sz = TextRenderer.MeasureText(b.Text, b.Font);
        b.Size = new Size(Math.Max(minWidth, sz.Width + 26), heightPx);
    }

    private static void StyleAccentButton(Button okButton)
    {
        okButton.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        ApplyDialogButtonSize(okButton, minWidth: 92, DialogButtonHeight);

        okButton.BackColor = Color.FromArgb(0, 120, 215);
        okButton.ForeColor = Color.White;
        okButton.FlatStyle = FlatStyle.Flat;
        okButton.FlatAppearance.BorderSize = 1;
        okButton.FlatAppearance.BorderColor = PrimaryButtonBorder;
        okButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 100, 190);
        okButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 90, 170);

        okButton.MouseEnter += (_, _) =>
            okButton.BackColor = Color.FromArgb(0, 100, 190);
        okButton.MouseLeave += (_, _) =>
            okButton.BackColor = Color.FromArgb(0, 120, 215);
    }

    private void TryUnlock()
    {
        var entered = _codeTextBox.Text?.Trim() ?? string.Empty;
        if (string.Equals(entered, RequiredCode, StringComparison.Ordinal))
        {
            _authenticated = true;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        _statusLabel.Text = "Incorrect code. Try again.";
        _codeTextBox.Clear();
        _codeTextBox.Focus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Allow closing without code when user cancels or closes the dialog (X).
        if (_authenticated || DialogResult == DialogResult.Cancel)
        {
            base.OnFormClosing(e);
            return;
        }

        if (e.CloseReason == CloseReason.UserClosing)
        {
            DialogResult = DialogResult.Cancel;
            base.OnFormClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnFormClosing(e);
    }
}
