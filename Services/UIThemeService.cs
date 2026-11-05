using System.Drawing;
using System.Drawing.Drawing2D;

namespace AppSettingsEditor.Services;

internal static class UIThemeService
{
    // Modern color palette matching the screenshots
    public static Color PrimaryBlue => Color.FromArgb(64, 149, 191); // Softer, more modern blue
    public static Color LightBlue => Color.FromArgb(173, 216, 230); // Light blue background
    public static Color LighterBlue => Color.FromArgb(220, 240, 250); // Very light blue for panels
    public static Color DarkBlue => Color.FromArgb(50, 100, 150); // Darker blue for text
    public static Color AccentBlue => Color.FromArgb(100, 180, 220); // Accent blue
    public static Color White => Color.White;
    public static Color LightGray => Color.FromArgb(245, 245, 250);
    public static Color BorderGray => Color.FromArgb(220, 220, 230);
    
    // Status colors
    public static Color SuccessGreen => Color.FromArgb(76, 175, 80);
    public static Color ErrorRed => Color.FromArgb(244, 67, 54);
    public static Color WarningOrange => Color.FromArgb(255, 152, 0);
    public static Color InfoBlue => Color.FromArgb(33, 150, 243);

    // Create rounded rectangle path
    public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        var arcRect = new Rectangle(rect.Location, new Size(diameter, diameter));

        // Top left
        path.AddArc(arcRect, 180, 90);
        arcRect.X = rect.Right - diameter;
        // Top right
        path.AddArc(arcRect, 270, 90);
        arcRect.Y = rect.Bottom - diameter;
        // Bottom right
        path.AddArc(arcRect, 0, 90);
        arcRect.X = rect.Left;
        // Bottom left
        path.AddArc(arcRect, 90, 90);
        path.CloseFigure();

        return path;
    }

    // Draw rounded rectangle
    public static void DrawRoundedRectangle(Graphics g, Rectangle rect, Color fillColor, Color borderColor, int radius, int borderWidth = 1)
    {
        using (var path = CreateRoundedRectangle(rect, radius))
        {
            if (fillColor != Color.Transparent)
            {
                using (var brush = new SolidBrush(fillColor))
                {
                    g.FillPath(brush, path);
                }
            }
            if (borderColor != Color.Transparent && borderWidth > 0)
            {
                using (var pen = new Pen(borderColor, borderWidth))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }

    // Create gradient brush
    public static LinearGradientBrush CreateGradientBrush(Rectangle rect, Color startColor, Color endColor, float angle = 90f)
    {
        return new LinearGradientBrush(rect, startColor, endColor, angle);
    }

    // Apply modern button style
    public static void ApplyModernButtonStyle(Button button, bool isPrimary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
        
        if (isPrimary)
        {
            button.BackColor = PrimaryBlue;
            button.ForeColor = White;
            button.Font = new Font(button.Font.FontFamily, button.Font.Size, FontStyle.Bold);
        }
        else
        {
            button.BackColor = LighterBlue;
            button.ForeColor = DarkBlue;
        }
        
        // Add hover effect
        button.MouseEnter += (s, e) =>
        {
            if (isPrimary)
            {
                button.BackColor = AccentBlue;
            }
            else
            {
                button.BackColor = LightBlue;
            }
        };
        
        button.MouseLeave += (s, e) =>
        {
            if (isPrimary)
            {
                button.BackColor = PrimaryBlue;
            }
            else
            {
                button.BackColor = LighterBlue;
            }
        };
    }

    // Apply modern textbox style
    public static void ApplyModernTextBoxStyle(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = White;
        textBox.ForeColor = DarkBlue;
        textBox.Font = new Font(textBox.Font.FontFamily, 9.5f);
    }

    // Apply modern combobox style
    public static void ApplyModernComboBoxStyle(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = White;
        comboBox.ForeColor = DarkBlue;
        comboBox.Font = new Font(comboBox.Font.FontFamily, 9.5f);
    }

    // Apply modern form styling
    public static void ApplyModernFormStyle(Form form)
    {
        form.BackColor = White;
        form.Font = new Font("Segoe UI", 9);
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.StartPosition = FormStartPosition.CenterParent;
    }

    // Apply modern listbox style
    public static void ApplyModernListBoxStyle(ListBox listBox)
    {
        listBox.BackColor = White;
        listBox.ForeColor = DarkBlue;
        listBox.Font = new Font("Segoe UI", 9);
        listBox.BorderStyle = BorderStyle.FixedSingle;
    }
}

