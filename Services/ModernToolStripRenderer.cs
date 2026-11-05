using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppSettingsEditor.Services;

internal class ModernToolStripRenderer : ToolStripProfessionalRenderer
{
    public ModernToolStripRenderer() : base(new ModernColorTable())
    {
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is ToolStripButton button && button.Selected)
        {
            var rect = new Rectangle(0, 0, e.Item.Width, e.Item.Height);
            using (var brush = new LinearGradientBrush(rect, 
                Color.FromArgb(100, 180, 220), 
                Color.FromArgb(64, 149, 191), 
                90f))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
        }
        else
        {
            base.OnRenderButtonBackground(e);
        }
    }
}

internal class ModernColorTable : ProfessionalColorTable
{
    public override Color ToolStripGradientBegin => Color.FromArgb(64, 149, 191);
    public override Color ToolStripGradientMiddle => Color.FromArgb(64, 149, 191);
    public override Color ToolStripGradientEnd => Color.FromArgb(64, 149, 191);
    public override Color MenuItemSelected => Color.FromArgb(100, 180, 220);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(100, 180, 220);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(64, 149, 191);
    public override Color ButtonSelectedHighlight => Color.FromArgb(100, 180, 220);
    public override Color ButtonSelectedHighlightBorder => Color.FromArgb(64, 149, 191);
}

