using System.Text;
using System.Xml;
using AppSettingsEditor.Models;

namespace AppSettingsEditor.Services;

internal class ResidentOpenExeConfigService
{
    private static void EnsureRoot(XmlDocument doc)
    {
        if (doc.DocumentElement != null && doc.DocumentElement.Name == "Root")
            return;

        doc.RemoveAll();
        var root = doc.CreateElement("Root");
        doc.AppendChild(root);
    }

    private static XmlElement? GetSection(XmlElement root, string sectionName)
    {
        var node = root.SelectSingleNode($"section[@name='{sectionName}']") as XmlElement;
        return node;
    }

    private static void RemoveSection(XmlDocument doc, string sectionName)
    {
        var root = doc.DocumentElement;
        if (root == null) return;
        var section = GetSection(root, sectionName);
        section?.ParentNode?.RemoveChild(section);
    }

    private static void UpsertSetting(XmlDocument doc, string sectionName, string name, string value)
    {
        EnsureRoot(doc);
        var root = doc.DocumentElement!;
        var section = GetSection(root, sectionName);
        if (section == null)
        {
            section = doc.CreateElement("section");
            section.SetAttribute("name", sectionName);
            root.AppendChild(section);
        }

        var xpath = $"section[@name='{sectionName}']/setting[@name='{name}']";
        var existing = root.SelectSingleNode(xpath) as XmlElement;
        if (existing != null)
        {
            existing.SetAttribute("value", value);
            return;
        }

        var setting = doc.CreateElement("setting");
        setting.SetAttribute("name", name);
        setting.SetAttribute("value", value);
        section.AppendChild(setting);
    }

    private static string? ReadSetting(XmlDocument doc, string sectionName, string name)
    {
        var root = doc.DocumentElement;
        if (root == null) return null;
        var xpath = $"//section[@name='{sectionName}']/setting[@name='{name}']";
        var node = root.SelectSingleNode(xpath) as XmlElement;
        var attr = node?.GetAttribute("value");
        return string.IsNullOrEmpty(attr) ? null : attr;
    }

    public ResidentOpenExeSettings Load(string configFilePath)
    {
        var s = new ResidentOpenExeSettings();

        if (!File.Exists(configFilePath))
            return s;

        var doc = new XmlDocument();
        doc.Load(configFilePath);

        s.ExecutableFileName = ReadSetting(doc, "Parameters", "NombreExe") ?? s.ExecutableFileName;
        s.ExecutableDirectory = ReadSetting(doc, "Parameters", "RutaExe") ?? s.ExecutableDirectory;
        s.Executable2FileName = ReadSetting(doc, "Parameters", "NombreExe2");
        s.Executable2Directory = ReadSetting(doc, "Parameters", "RutaExe2");

        if (int.TryParse(ReadSetting(doc, "Parameters", "TimerEspera"), out var interval) && interval > 0)
            s.CheckIntervalSeconds = interval;

        if (int.TryParse(ReadSetting(doc, "Parameters", "SoloUnaVez"), out var once) && once == 1)
            s.RunOnceThenExit = true;

        if (bool.TryParse(ReadSetting(doc, "Parameters", "FormularioSetToFront"), out var front))
            s.BringWindowToFront = front;

        s.TrayDisplayName = ReadSetting(doc, "Parameters", "NombreResidenteOpenExe");

        if (bool.TryParse(ReadSetting(doc, "Parameters", "OpenAsAdministrator"), out var admin))
            s.OpenAsAdministrator = admin;

        var root = doc.DocumentElement;
        s.IncludeCloseExeSection = root?.SelectSingleNode("section[@name='CloseExe']") != null;

        if (int.TryParse(ReadSetting(doc, "CloseExe", "TiempoMax"), out var tmax) && tmax > 0)
            s.HeartbeatTimeoutMs = tmax;

        s.LogDirectory = ReadSetting(doc, "CloseExe", "rutaGuardarLogs");

        return s;
    }

    public void Save(string configFilePath, ResidentOpenExeSettings settings)
    {
        var doc = new XmlDocument();
        if (File.Exists(configFilePath))
            doc.Load(configFilePath);
        else
            EnsureRoot(doc);

        var soloUnaVez = settings.RunOnceThenExit ? "1" : "0";

        UpsertSetting(doc, "Parameters", "NombreExe", settings.ExecutableFileName.Trim());
        UpsertSetting(doc, "Parameters", "RutaExe", NormalizeDirectory(settings.ExecutableDirectory));

        if (!string.IsNullOrWhiteSpace(settings.Executable2FileName) &&
            !string.IsNullOrWhiteSpace(settings.Executable2Directory))
        {
            UpsertSetting(doc, "Parameters", "NombreExe2", settings.Executable2FileName.Trim());
            UpsertSetting(doc, "Parameters", "RutaExe2", NormalizeDirectory(settings.Executable2Directory));
        }
        else
        {
            UpsertSetting(doc, "Parameters", "NombreExe2", "");
            UpsertSetting(doc, "Parameters", "RutaExe2", "");
        }

        UpsertSetting(doc, "Parameters", "TimerEspera", Math.Clamp(settings.CheckIntervalSeconds, 1, 3600).ToString());
        UpsertSetting(doc, "Parameters", "SoloUnaVez", soloUnaVez);
        UpsertSetting(doc, "Parameters", "FormularioSetToFront", settings.BringWindowToFront.ToString());
        UpsertSetting(doc, "Parameters", "NombreResidenteOpenExe", settings.TrayDisplayName?.Trim() ?? "");
        UpsertSetting(doc, "Parameters", "OpenAsAdministrator", settings.OpenAsAdministrator.ToString());

        if (settings.IncludeCloseExeSection)
        {
            UpsertSetting(doc, "CloseExe", "FuncionCloseExe", "true");
            UpsertSetting(doc, "CloseExe", "TiempoMax", Math.Max(1000, settings.HeartbeatTimeoutMs).ToString());
            UpsertSetting(doc, "CloseExe", "rutaGuardarLogs", settings.LogDirectory?.Trim() ?? "");
            UpsertSetting(doc, "CloseExe", "NombreExe", settings.ExecutableFileName.Trim());
            UpsertSetting(doc, "CloseExe", "RutaExe", NormalizeDirectory(settings.ExecutableDirectory));
            UpsertSetting(doc, "CloseExe", "TimerEspera", Math.Clamp(settings.CheckIntervalSeconds, 1, 3600).ToString());
            UpsertSetting(doc, "CloseExe", "SoloUnaVez", soloUnaVez);
            UpsertSetting(doc, "CloseExe", "OpenAsAdministrator", settings.OpenAsAdministrator.ToString());
            UpsertSetting(doc, "CloseExe", "NombreResidenteOpenExe", settings.TrayDisplayName?.Trim() ?? "");

            if (!string.IsNullOrWhiteSpace(settings.Executable2FileName) &&
                !string.IsNullOrWhiteSpace(settings.Executable2Directory))
            {
                UpsertSetting(doc, "CloseExe", "RutaExe2", NormalizeDirectory(settings.Executable2Directory));
                UpsertSetting(doc, "CloseExe", "NombreExe2", settings.Executable2FileName.Trim());
            }
        }
        else
        {
            RemoveSection(doc, "CloseExe");
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(configFilePath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var settingsWriter = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            NewLineChars = "\r\n"
        };

        using (var writer = XmlWriter.Create(configFilePath, settingsWriter))
        {
            doc.Save(writer);
        }
    }

    private static string NormalizeDirectory(string path)
    {
        var t = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return t.Length == 0 ? path.Trim() : t + Path.DirectorySeparatorChar;
    }
}
