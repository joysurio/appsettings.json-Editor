using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AppSettingsEditor;

/// <summary>
/// Converts Markdown reports to PDF using QuestPDF (no Python or external tools required).
/// </summary>
public static class PdfConverter
{
    private static readonly QuestPDF.Infrastructure.Color TitleColor = QuestPDF.Infrastructure.Color.FromHex("#2c3e50");
    private static readonly QuestPDF.Infrastructure.Color H2Color = QuestPDF.Infrastructure.Color.FromHex("#34495e");
    private static readonly QuestPDF.Infrastructure.Color H3Color = QuestPDF.Infrastructure.Color.FromHex("#555555");
    private static readonly QuestPDF.Infrastructure.Color TableHeaderBg = QuestPDF.Infrastructure.Color.FromHex("#3498db");
    private static readonly QuestPDF.Infrastructure.Color TableRowAlt = QuestPDF.Infrastructure.Color.FromHex("#f2f2f2");

    static PdfConverter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void ConvertToPdf(string mdFile, string pdfFile)
    {
        if (!File.Exists(mdFile))
            throw new FileNotFoundException($"Markdown file not found: {mdFile}");

        var mdContent = File.ReadAllText(mdFile);
        var lines = mdContent.Replace("\r\n", "\n").Split('\n');

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(72);
                page.DefaultTextStyle(x => x.FontSize(10).LineHeight(1.4f).FontFamily(Fonts.SegoeUI));

                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    RenderMarkdownLines(column, lines);
                });
            });
        }).GeneratePdf(pdfFile);

        if (!File.Exists(pdfFile))
            throw new InvalidOperationException($"PDF file was not created: {pdfFile}");
    }

    private static void RenderMarkdownLines(ColumnDescriptor column, IReadOnlyList<string> lines)
    {
        var i = 0;
        var inTable = false;
        var tableLines = new List<string>();

        while (i < lines.Count)
        {
            var line = lines[i].TrimEnd();

            if (line.Contains('|', StringComparison.Ordinal) && !line.StartsWith("#", StringComparison.Ordinal))
            {
                if (!inTable)
                {
                    inTable = true;
                    tableLines = [line];
                }
                else
                {
                    tableLines.Add(line);
                }

                i++;
                continue;
            }

            if (inTable && tableLines.Count > 0)
            {
                AppendTable(column, string.Join("\n", tableLines));
                column.Item().Height(14.4f); // ~0.2 inch spacer like Python
                inTable = false;
                tableLines = [];
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                column.Item().Height(7.2f); // ~0.1 inch
                i++;
                continue;
            }

            // Longest # prefix first (## matches # otherwise)
            if (line.StartsWith("#### ", StringComparison.Ordinal))
            {
                var text = line[5..].Trim();
                column.Item().PaddingTop(4).Text(text).FontSize(14).FontColor(H3Color).SemiBold();
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                var text = line[4..].Trim();
                column.Item().PaddingTop(6).Text(text).FontSize(14).FontColor(H3Color).SemiBold();
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var text = line[3..].Trim();
                column.Item().PaddingTop(8).Text(text).FontSize(16).FontColor(H2Color).SemiBold();
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var text = line[2..].Trim();
                column.Item().Text(text).FontSize(20).FontColor(TitleColor).SemiBold();
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                var text = "• " + line[2..].Trim();
                column.Item().Text(t => AppendListOrNormalSpans(t, text, 10));
            }
            else if (Regex.IsMatch(line, @"^\d+\.\s"))
            {
                column.Item().Text(line);
            }
            else if (line.StartsWith("```", StringComparison.Ordinal))
            {
                i++;
                continue;
            }
            else
            {
                var text = ReplaceEmojiForRegularParagraph(line);
                column.Item().Text(t => AppendListOrNormalSpans(t, text, 10));
            }

            i++;
        }

        if (inTable && tableLines.Count > 0)
            AppendTable(column, string.Join("\n", tableLines));
    }

    private static string ReplaceEmojiForRegularParagraph(string line) =>
        line
            .Replace("🔴", "[CRITICAL]", StringComparison.Ordinal)
            .Replace("⚠️", "[WARNING]", StringComparison.Ordinal)
            .Replace("✅", "[OK]", StringComparison.Ordinal)
            .Replace("✓", "[OK]", StringComparison.Ordinal)
            .Replace("❌", "[ERROR]", StringComparison.Ordinal);

    private static void AppendListOrNormalSpans(TextDescriptor text, string line, float fontSize)
    {
        AppendMarkdownInline(text, line, fontSize);
    }

    /// <summary>Handles **bold**, *italic*, `code` similar to the old Python converter.</summary>
    private static void AppendMarkdownInline(TextDescriptor text, string line, float fontSize)
    {
        var i = 0;
        while (i < line.Length)
        {
            var boldStart = line.IndexOf("**", i, StringComparison.Ordinal);
            if (boldStart < 0)
            {
                AppendPlainWithCodeAndItalic(text, line[i..], fontSize);
                return;
            }

            if (boldStart > i)
                AppendPlainWithCodeAndItalic(text, line[i..boldStart], fontSize);

            var boldEnd = line.IndexOf("**", boldStart + 2, StringComparison.Ordinal);
            if (boldEnd < 0)
            {
                AppendPlainWithCodeAndItalic(text, line[boldStart..], fontSize);
                return;
            }

            text.Span(line.Substring(boldStart + 2, boldEnd - boldStart - 2)).SemiBold().FontSize(fontSize);
            i = boldEnd + 2;
        }
    }

    private static void AppendPlainWithCodeAndItalic(TextDescriptor text, string segment, float fontSize)
    {
        if (string.IsNullOrEmpty(segment))
            return;

        var parts = segment.Split('`');
        for (var p = 0; p < parts.Length; p++)
        {
            if (p % 2 == 1)
                text.Span(parts[p]).FontFamily(Fonts.CourierNew).FontSize(9);
            else
                AppendItalicSegments(text, parts[p], fontSize);
        }
    }

    private static void AppendItalicSegments(TextDescriptor text, string s, float fontSize)
    {
        var i = 0;
        while (i < s.Length)
        {
            var star = s.IndexOf('*', i);
            if (star < 0)
            {
                if (i < s.Length)
                    text.Span(s[i..]).FontSize(fontSize);
                return;
            }

            if (star > i)
                text.Span(s[i..star]).FontSize(fontSize);

            if (star + 1 < s.Length && s[star + 1] == '*')
            {
                text.Span("**").FontSize(fontSize);
                i = star + 2;
                continue;
            }

            var endStar = s.IndexOf('*', star + 1);
            if (endStar < 0)
            {
                text.Span(s[star..]).FontSize(fontSize);
                return;
            }

            text.Span(s.Substring(star + 1, endStar - star - 1)).Italic().FontSize(fontSize);
            i = endStar + 1;
        }
    }

    private static void AppendTable(ColumnDescriptor column, string mdTable)
    {
        var tableData = ParseMarkdownTable(mdTable);
        if (tableData.Count == 0)
            return;

        var colCount = tableData.Max(r => r.Count);
        if (colCount == 0)
            return;

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (var c = 0; c < colCount; c++)
                    columns.RelativeColumn();
            });

            for (var rowIndex = 0; rowIndex < tableData.Count; rowIndex++)
            {
                var row = tableData[rowIndex];
                var isHeader = rowIndex == 0;

                for (var colIndex = 0; colIndex < colCount; colIndex++)
                {
                    var cellText = colIndex < row.Count ? row[colIndex] : string.Empty;

                    var cell = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Medium);

                    if (isHeader)
                    {
                        cell.Background(TableHeaderBg).Padding(8)
                            .Text(cellText).FontColor(Colors.White).SemiBold().FontSize(10);
                    }
                    else
                    {
                        var bg = rowIndex % 2 == 0 ? Colors.White : TableRowAlt;
                        cell.Background(bg).Padding(5).Text(cellText).FontSize(10);
                    }
                }
            }
        });
    }

    private static List<List<string>> ParseMarkdownTable(string mdTable)
    {
        var rows = new List<List<string>>();
        foreach (var rawLine in mdTable.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim();
            if (!line.Contains('|', StringComparison.Ordinal))
                continue;

            var cells = line.Split('|')
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .ToList();

            if (cells.Count == 0)
                continue;

            if (cells.TrueForAll(c => c is "---" or ""))
                continue;

            rows.Add(cells);
        }

        return rows;
    }
}
