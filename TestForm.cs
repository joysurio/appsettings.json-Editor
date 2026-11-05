using AppSettingsEditor.Controls;
using System.Windows.Forms;

namespace AppSettingsEditor;

internal partial class TestForm : Form
{
    public TestForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "AppSettings Editor - Test Form";
        this.Size = new System.Drawing.Size(1000, 700);
        this.StartPosition = FormStartPosition.CenterScreen;

        var editor = new AppSettingsEditorControl();
        editor.Dock = DockStyle.Fill;
        this.Controls.Add(editor);
    }
}

