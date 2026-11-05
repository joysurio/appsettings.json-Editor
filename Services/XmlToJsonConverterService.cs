using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AppSettingsEditor.Services;

public class XmlToJsonConverterService
{
    public class ConversionResult
    {
        public bool Success { get; set; }
        public string JsonOutput { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> MappedKeys { get; set; } = new();
        public List<string> UnmappedKeys { get; set; } = new();
        public List<string> MissingExpectedKeys { get; set; } = new();
    }

    public ConversionResult ConvertXmlToJson(string xmlFilePath)
    {
        var result = new ConversionResult();

        try
        {
            if (!File.Exists(xmlFilePath))
            {
                result.Success = false;
                result.ErrorMessage = $"XML file not found: {xmlFilePath}";
                return result;
            }

            var xmlDoc = XDocument.Load(xmlFilePath);
            var root = xmlDoc.Element("Root");
            if (root == null)
            {
                result.Success = false;
                result.ErrorMessage = "Invalid XML structure: Root element not found";
                return result;
            }

            var jsonRoot = new Dictionary<string, object>();

            // Section 7.1: Servidor → ConnectionStrings
            var connectionStrings = BuildConnectionStrings(root);
            if (connectionStrings.Count > 0)
                jsonRoot["ConnectionStrings"] = connectionStrings;

            // Section 7.2: Configuraciones → AppSettings
            var appSettings = BuildAppSettings(root, result);
            if (appSettings.Count > 0)
                jsonRoot["AppSettings"] = appSettings;

            // Section 7.3: SMTP Settings → EmailConfiguration
            var emailConfig = BuildEmailConfiguration(root, result);
            if (emailConfig.Count > 0)
                jsonRoot["EmailConfiguration"] = emailConfig;

            // Antenna params are also stored in Configuraciones
            var antennaConfig = BuildAntennaConfiguration(root);
            if (antennaConfig.Count > 0)
                jsonRoot["AntennaConfiguration"] = antennaConfig;

            // Section 7.4: ConfigDispensa → DispenseConfiguration
            var dispenseConfig = BuildDispenseConfiguration(root, result);
            if (dispenseConfig.Count > 0)
                jsonRoot["DispenseConfiguration"] = dispenseConfig;

            // Collect all unmapped settings
            CollectUnmappedSettings(root, result);

            // Generate JSON with proper formatting (PascalCase to match expected output)
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
                // No PropertyNamingPolicy - use PascalCase as in expected appsettings.json
            };

            result.JsonOutput = JsonSerializer.Serialize(jsonRoot, options);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error converting XML: {ex.Message}";
        }

        return result;
    }

    private Dictionary<string, object> BuildConnectionStrings(XElement root)
    {
        var connectionStrings = new Dictionary<string, object>();
        var servidorSection = root.Elements("section")
            .FirstOrDefault(s => s.Attribute("name")?.Value == "Servidor");

        if (servidorSection != null)
        {
            var cadenaCompleta = GetSettingValue(servidorSection, "CadenaCompleta");
            if (!string.IsNullOrWhiteSpace(cadenaCompleta))
            {
                connectionStrings["DefaultConnection"] = NormalizeConnectionString(cadenaCompleta);
            }
            else
            {
                var cadena = GetSettingValue(servidorSection, "Cadena");
                var bbdD = GetSettingValue(servidorSection, "BBDD");
                var user = GetSettingValue(servidorSection, "user") ?? GetSettingValue(servidorSection, "User");
                var password = GetSettingValue(servidorSection, "Password");

                if (!string.IsNullOrEmpty(cadena) && !string.IsNullOrEmpty(bbdD))
                {
                    // Build SQL connection string including credentials when provided.
                    string connectionString;
                    if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
                    {
                        connectionString =
                            $"Data Source={cadena};Initial Catalog={bbdD};User ID={user};Password={password};Trust Server Certificate=True;";
                    }
                    else
                    {
                        connectionString = $"Data Source={cadena};Initial Catalog={bbdD};Trust Server Certificate=True;";
                    }

                    connectionStrings["DefaultConnection"] = connectionString;
                }
            }
        }

        // New version schema expects an offline entry as a string.
        connectionStrings["DefaultOfflineConnection"] = "";
        return connectionStrings;
    }

    private string NormalizeConnectionString(string connectionString)
    {
        if (connectionString.Contains("Trust Server Certificate", StringComparison.OrdinalIgnoreCase))
            return connectionString;
        return connectionString.TrimEnd(';') + ";Trust Server Certificate=True;";
    }

    private Dictionary<string, object> BuildAppSettings(XElement root, ConversionResult result)
    {
        var appSettings = new Dictionary<string, object>();
        var configSection = root.Elements("section")
            .FirstOrDefault(s => s.Attribute("name")?.Value == "Configuraciones");

        if (configSection == null)
            return appSettings;

        // Mapping table: XML (Configuraciones/setting name) → appsettings.json (AppSettings/setting name)
        var mappings = new Dictionary<string, string>
        {
            { "IdMaquinaDispensacion", "DispensingMachineId" },
            { "MaxSegundosInactividad", "MaxSecondsInactivity" },
            { "Idioma", "DefaultLanguage" },
            { "DirImagenLogos", "DirImageLogos" },
            { "pathImagenModeloPrenda", "PathGarmentModelImage" },
            { "NivelLog", "LogLevel" },
            { "TipoAutenticacion", "AuthenticationType" },
            { "LectorHID", "ReaderHID" },
            { "OcultarBotonesMaquina", "HideButtonsMachine" },
            { "CambiaTalla", "ChangeSize" },
            { "mostrarBotonesParejaPrendas", "ShowPairButtonsClothes" },
            { "MostrarCargaDispensadora", "ShowDispensingLoad" },
            { "IdiomasAMostrar", "LanguagesToShow" },
            { "HabilitarEnvioSMS", "EnableSMSSend" },
            { "CableadoTaquillasDirectoCerradura", "DirectLockerWiringLock" },
            { "Llegadai7ParaPrendaCaida", "Arrivali7ForGarmentDrop" },
            { "MostrarMensajeOtraPrendaRecolectora", "ShowMessageAnotherCollectorGarment" },
            { "PuertaDispensacion", "DispensingDoor" },
            { "LectorHuella", "FingerprintReader" },
            { "DirHuellasUsuarios", "UserFingerprintDir" },
            { "Etiqueta2", "Tag2" },
            { "MostrarMensajeCreaccionPrenda", "ShowMessageCreationGarment" },
            { "TipoDriver", "DriverType" },

            // PLC program flag - move to AppSettings (was previously treated as dispense-specific)
            { "PLCProgramado", "ProgrammedPLC" },
            { "PLCProgramdo", "ProgrammedPLC" },

            // Locker-specific options (Taquillas XML)
            { "NoTenerSeleccionadosModelosTaquillas", "NoHaveSelectedLockerModels" },
            { "ModoDiferencia", "DifferenceMode" },
            { "NoMostrarAvisoPrendaMalColocadaTaquilla", "NoShowNoticeGarmentWronglyPlacedLocker" },
            { "TaquillasConPLC", "LockersWithPLC" }
        };

        foreach (var mapping in mappings)
        {
            var xmlValue = GetSettingValue(configSection, mapping.Key);
            if (xmlValue != null)
            {
                var jsonKey = mapping.Value;
                var convertedValue = ConvertValue(xmlValue, jsonKey);
                appSettings[jsonKey] = convertedValue;
                result.MappedKeys.Add($"Configuraciones/{mapping.Key} → AppSettings/{jsonKey}");
            }
        }

        var explicitMappingKeys = new HashSet<string>(mappings.Keys, StringComparer.OrdinalIgnoreCase);
        var reservedForOtherSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TipoEnvioMail", "smtpHost", "smtpPort", "smtpUser", "smtpPass", "RemitenteEnviosSmtp",
            "PotenciaAntena", "SensibilidadAntena", "SensibilidadAntena2", "TiempoEsperaSlate", "ConfiguracionImpinj2", "RSSIAntena",
            "ContPrendaNoDispensada", "MostrarMensajeOtraPrendaDispensadora", "mostrarPrendasDisponiblesOtraMaquina", "OrdenDescendenteCargaAuto"
            ,
            // These keys belong to DispenseConfiguration in the new schema.
            "PrendaIntroducidaBlackBox", "PrendaIntroducidaBlackBox2",
            "LEDBlackBox",
            "PuertaCerraduraPLC", "PuertaCerradaBlackBox", "SopladorBlackBoxPLC",
            "SaltarSensorPrendaCaida",
            "MaximoTiempoSoplado", "TiempoAdicionalSoplado",
            "SolicitarConfirmacionUsuario",
            "OcultarContadorRecolectora",
            "PuertaSiempreCerradaSopladora", "CerraduraTaquilla",
            "TiempoMensajeRecepcionCorrecta",
            "AdmitirPrendaNoIdentificada", "AdmitirPrendaSinChip",
            "MensajePrincipalRecolectora",
            "NoPermitirDevolverSinIdentificarse",
            "DeshabilitaIman", "EsperaIman", "negarEstadoLED",
            "Llegadai7ParaPrendaCaida","MostrarMensajeOtraPrendaRecolectora",
            "PuertaDispensacion", "LectorHuella", "DirHuellasUsuarios",
            "Etiqueta2", "MostrarMensajeCreaccionPrenda", "TipoDriver",

            // Parameter exists in some XML versions, but not in the New version schema.
            "TaquillasShowRoom"
        };

        // Keep any extra machine/customer-specific keys from Configuraciones.
        foreach (var setting in configSection.Elements("setting"))
        {
            var xmlKey = setting.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(xmlKey))
                continue;

            if (explicitMappingKeys.Contains(xmlKey) || reservedForOtherSections.Contains(xmlKey))
                continue;

            var xmlValue = setting.Attribute("value")?.Value?.Trim();
            if (string.IsNullOrEmpty(xmlValue))
                continue;

            if (!appSettings.ContainsKey(xmlKey))
            {
                appSettings[xmlKey] = ConvertValue(xmlValue, xmlKey);
                result.MappedKeys.Add($"Configuraciones/{xmlKey} → AppSettings/{xmlKey}");
            }
        }

        return appSettings;
    }

    private Dictionary<string, object> BuildEmailConfiguration(XElement root, ConversionResult result)
    {
        var emailConfig = new Dictionary<string, object>();
        var configSection = root.Elements("section")
            .FirstOrDefault(s => s.Attribute("name")?.Value == "Configuraciones");

        if (configSection == null)
            return emailConfig;

        var mappings = new Dictionary<string, string>
        {
            { "TipoEnvioMail", "TypeShippingMail" },
            { "smtpHost", "SmtpHost" },
            { "smtpPort", "SmtpPort" },
            { "smtpUser", "SmtpUser" },
            { "smtpPass", "SmtpPass" },
            { "RemitenteEnviosSmtp", "SenderShippingSmtp" }
        };

        foreach (var mapping in mappings)
        {
            var xmlValue = GetSettingValue(configSection, mapping.Key);
            if (xmlValue != null)
            {
                var jsonKey = mapping.Value;
                var convertedValue = ConvertValue(xmlValue, jsonKey);
                emailConfig[jsonKey] = convertedValue;
                result.MappedKeys.Add($"Configuraciones/{mapping.Key} → EmailConfiguration/{jsonKey}");
            }
        }

        return emailConfig;
    }

    private Dictionary<string, object> BuildAntennaConfiguration(XElement root)
    {
        var antennaConfig = new Dictionary<string, object>();
        var configSection = root.Elements("section")
            .FirstOrDefault(s => s.Attribute("name")?.Value == "Configuraciones");

        if (configSection == null)
            return antennaConfig;

        // Mapping is derived from XML antenna setting names used by the CTO configs.
        var mappings = new Dictionary<string, string>
        {
            { "PotenciaAntena", "PowerAntenna" },
            { "SensibilidadAntena", "AntennaSensitivity" },
            { "SensibilidadAntena2", "AntennaSensitivity2" },
            { "TiempoEsperaSlate", "TimeWaitSlate" },
            { "ConfiguracionImpinj2", "ConfigurationImpinj2" },
            { "RSSIAntena", "RSSIAntenna" }
        };

        foreach (var mapping in mappings)
        {
            var xmlValue = GetSettingValue(configSection, mapping.Key);
            if (xmlValue != null)
            {
                var jsonKey = mapping.Value;
                antennaConfig[jsonKey] = ConvertValue(xmlValue, jsonKey);
            }
        }

        return antennaConfig;
    }

    private Dictionary<string, object> BuildDispenseConfiguration(XElement root, ConversionResult result)
    {
        var dispenseConfig = new Dictionary<string, object>();

        // Some parameters related to dispensing display logic are stored in Configuraciones (not ConfigDispensa)
        var configSection = root.Elements("section")
            .FirstOrDefault(s => s.Attribute("name")?.Value == "Configuraciones");

        var configDispensaSection = root.Elements("section")
            .FirstOrDefault(s => s.Attribute("name")?.Value == "ConfigDispensa");

        // 1) Map logic/display fields from Configuraciones → DispenseConfiguration
        if (configSection != null)
        {
            var configuracionesMappings = new Dictionary<string, string>
            {
                { "ContPrendaNoDispensada", "NonDispensedGarmentCount" },
                { "MostrarMensajeOtraPrendaDispensadora", "ShowMessageOtherGarmentDispenser" },
                { "mostrarPrendasDisponiblesOtraMaquina", "ShowAvailableGarmentsAnotherMachine" },
                // Best-effort mapping: the XML uses "OrdenDescendenteCargaAuto" for auto-loading order.
                // The app schema doesn't have a 1:1 field, so we map it to a relevant preset toggle.
                { "OrdenDescendenteCargaAuto", "PresetAlwaysOnYUp" },

                // Gravity/collector parameters (Recolectoras) - new version schema stores them inside DispenseConfiguration.
                { "PrendaIntroducidaBlackBox", "GarmentIntroducedBlackBox" },
                { "PrendaIntroducidaBlackBox2", "GarmentIntroducedBlackBox2" },
                { "LEDBlackBox", "LEDBlackBox" },
                { "NoPermitirDevolverSinIdentificarse", "NoAllowReturnWithoutLogin" },
                { "AdmitirPrendaNoIdentificada", "AdmitUnidentifiedGarment" },
                { "AdmitirPrendaSinChip", "AdmitGarmentWithoutChip" },
                { "MensajePrincipalRecolectora", "MainCollectorMessage" },
                { "DeshabilitaIman", "DisableMagnet" },
                { "EsperaIman", "WaitMagnet" },
                { "TiempoMensajeRecepcionCorrecta", "CorrectReceptionMessageTime" },

                // Sopladoras parameters - also inside DispenseConfiguration.
                { "PuertaCerraduraPLC", "ElectronicLockPort" },
                { "PuertaCerradaBlackBox", "ClosedDoorBlackBox" },
                { "SopladorBlackBoxPLC", "BlowerBlackBoxPLC" },
                { "SaltarSensorPrendaCaida", "JumpSensorGarmentFall" },
                { "MaximoTiempoSoplado", "MaximumBlownTime" },
                { "TiempoAdicionalSoplado", "AdditionalTimeBlown" },
                { "SolicitarConfirmacionUsuario", "RequestUserConfirmation" },
                { "OcultarContadorRecolectora", "HideCollectorCounter" }
            };

            foreach (var mapping in configuracionesMappings)
            {
                var xmlValue = GetSettingValue(configSection, mapping.Key);
                if (xmlValue != null)
                {
                    var jsonKey = mapping.Value;
                    var convertedValue = ConvertValue(xmlValue, jsonKey);
                    dispenseConfig[jsonKey] = convertedValue;
                    result.MappedKeys.Add($"Configuraciones/{mapping.Key} → DispenseConfiguration/{jsonKey}");
                }
            }
        }

        // 2) Map motion/sensors fields from ConfigDispensa → DispenseConfiguration
        if (configDispensaSection != null)
        {
            var explicitDispenseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dispenseMappings = new Dictionary<string, string>
            {
                { "OrientalMotor", "OrientalMotor" },

                { "CabezalPinza", "HeadClamp" },
                { "CabezalPinza2", "HeadClamp2" },
                { "VersionPinza", "VersionClip" },
                { "RelesEstadoSolido", "RelaysStateSolid" },
                { "MaquinaSimetrica", "SymmetricalMachine" },
                { "PresetSiempreALaIzquierda", "PresetAlwaysLeft" },
                { "MaquinaAsimetrica", "AsymmetricalMachine" },

                { "SensorPrendaEnCajon", "SensorGarmentInDrawer" },
                { "SensorPrendaEnPinza", "SensorGarmentInClamp" },
                { "SensorPuertaCerrada", "SensorDoorClosed" },

                { "RevueltaSoltarPrenda", "RevoltDropGarment" },
                { "SegundosEsperaEntregaPrenda", "SecondsWaitDeliveryGarment" },
                { "PulsosSubidaPinza", "PulsesRiseClip" },

                { "CurrPush", "CurrPush" },
                { "VelPush", "SpeedPush" },

                { "NumeroReintentosDispensadora", "NumberRetriesDispenser" },

                { "VelocidadRevueltaPinza1", "SpeedRevolutionPinch1" },
                { "VelocidadRevueltaPinza2", "SpeedRevolutionPinch2" },
                { "VelocidadRevueltaPinza3", "SpeedRevolutionPinch3" },
                { "VelocidadRevueltaPinza4", "SpeedRevolutionPinch4" },

                { "PulsosMoverPinzaSiNoCogePrenda", "PulsesMoveGripperIfItDoesNotPickUpGarment" },

                // Motion positions & speeds used by the app schema
                { "PosDisX", "PosDisX" },
                { "PosXReposo", "PosXRest" },
                { "PosYReposo", "PosYRest" },
                { "PosYSubida", "PostYUpload" },

                { "VelXHoming", "SpeedXHoming" },
                { "VelYHoming", "SpeedYHoming" },
                { "VelXTrabajo", "SpeedXWork" },
                { "VelYTrabajo", "SpeedYWork" },

                { "AcelX", "AcelX" },
                { "AcelY", "AcelY" },

                { "YLimit", "YLimit" },
                { "XLeft", "XLeft" },
                { "XRight", "XRight" },
                { "DistanciaBalda", "DistanceBalda" },

                // Multi-height & lowering thresholds (best-effort mapping)
                { "YUp", "DistanceBaldaYUP" },
                { "YMED", "DistanceBaldaYMED" },
                { "YDown", "DistanceBaldaYDOWN" },
                { "YDown2", "DistanceBaldaYDOWN2" },
                { "YDown3", "DistanceBaldaYDOWN3" },
                { "YDown4", "DistanceBaldaYDOWN4" },

                { "MayorBajadoPermitido", "MajorLoweredAllowed" },
                { "MayorBajadoPermitidoYUp", "MajorLoweredAllowedYUp" },
                { "MayorBajadoPermitidoYMed", "MajorLoweredAllowedYMed" },
                { "MayorBajadoPermitidoYDown", "MajorLoweredAllowedYDown" },
                { "MayorBajadoPermitidoYDown2", "MajorLoweredAllowedYDown2" },
                { "MayorBajadoPermitidoYDown3", "MajorLoweredAllowedYDown3" },
                { "MayorBajadoPermitidoYDown4", "MajorLoweredAllowedYDown4" }
            };

            foreach (var mapping in dispenseMappings)
            {
                explicitDispenseKeys.Add(mapping.Key);
                var xmlValue = GetSettingValue(configDispensaSection, mapping.Key);
                if (xmlValue != null)
                {
                    var jsonKey = mapping.Value;
                    var convertedValue = ConvertValue(xmlValue, jsonKey);
                    dispenseConfig[jsonKey] = convertedValue;
                    result.MappedKeys.Add($"ConfigDispensa/{mapping.Key} → DispenseConfiguration/{jsonKey}");
                }
            }

            // Preserve any additional keys that exist in ConfigDispensa.
            foreach (var setting in configDispensaSection.Elements("setting"))
            {
                var xmlKey = setting.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(xmlKey) || explicitDispenseKeys.Contains(xmlKey))
                    continue;

                var xmlValue = setting.Attribute("value")?.Value?.Trim();
                if (string.IsNullOrEmpty(xmlValue))
                    continue;

                if (!dispenseConfig.ContainsKey(xmlKey))
                {
                    dispenseConfig[xmlKey] = ConvertValue(xmlValue, xmlKey);
                    result.MappedKeys.Add($"ConfigDispensa/{xmlKey} → DispenseConfiguration/{xmlKey}");
                }
            }
        }

        return dispenseConfig;
    }

    private Dictionary<string, object> BuildTestScreenConfiguration(XElement root, ConversionResult result)
    {
        var testScreenConfig = new Dictionary<string, object>();
        var configPantallaTestSection = root.Elements("section")
            .FirstOrDefault(s => s.Attribute("name")?.Value == "ConfigPantallaTest");

        if (configPantallaTestSection == null)
            return testScreenConfig;

        var mappings = new Dictionary<string, string>
        {
            { "TipoMaquina", "TipoMaquina" },
            { "PuertoCom", "PuertoCom" },
            { "TipoLector", "TipoLector" },
            { "COMTarjetas", "COMTarjetas" }
        };

        foreach (var mapping in mappings)
        {
            var xmlValue = GetSettingValue(configPantallaTestSection, mapping.Key);
            if (xmlValue != null)
            {
                var jsonKey = mapping.Value;
                var convertedValue = ConvertValue(xmlValue, jsonKey);
                testScreenConfig[jsonKey] = convertedValue;
                result.MappedKeys.Add($"ConfigPantallaTest/{mapping.Key} → TestScreenConfiguration/{jsonKey}");
            }
        }

        return testScreenConfig;
    }

    private void CollectUnmappedSettings(XElement root, ConversionResult result)
    {
        // Intentionally disabled:
        // this converter now preserves all non-empty XML settings (typed when possible),
        // so static "known mapping" checks create false positives for machine-specific keys.
        _ = root;
        _ = result;
    }

    private string? GetSettingValue(XElement section, string settingName)
    {
        var setting = section.Elements("setting")
            .FirstOrDefault(s =>
                string.Equals(
                    s.Attribute("name")?.Value,
                    settingName,
                    StringComparison.OrdinalIgnoreCase));
        var value = setting?.Attribute("value")?.Value;
        if (value == null)
            return null;

        // Treat empty/whitespace values as "not configured" and don't emit them in JSON.
        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private object ConvertValue(string xmlValue, string jsonKey)
    {
        // Languages are represented as a JSON array in the new schema.
        if (string.Equals(jsonKey, "LanguagesToShow", StringComparison.OrdinalIgnoreCase))
        {
            return xmlValue.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        // Section 6.1: Boolean conversion
        if (bool.TryParse(xmlValue, out bool boolValue))
        {
            return boolValue;
        }

        // Section 6.2: Integer conversion (including negative)
        if (int.TryParse(xmlValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
        {
            return intValue;
        }

        // Section 6.2b: Floating point conversion (e.g., "100.4")
        if (double.TryParse(xmlValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
        {
            return doubleValue;
        }

        // Section 6.5: Windows paths - JSON serializer will handle escaping automatically
        // Section 6.3: Strings remain strings
        return xmlValue;
    }
}
