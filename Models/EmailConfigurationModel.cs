using System.ComponentModel;

namespace AppSettingsEditor.Models;

internal class EmailConfigurationModel
{
    [Description("Mail sending mode (0=None, 1=Normal, 2=ASP+proxy, 3=ASP no proxy, 4=Schema without ASP no proxy, 5=SMTP). Default: 0")]
    [DefaultValue(0)]
    public int? TypeShippingMail { get; set; } = 0;

    [Description("CASP server URL/address")]
    public string? CASPServer { get; set; }

    [Description("Proxy username (mode 2). Default: 99016006V")]
    [DefaultValue("99016006V")]
    public string? UserNameProxyEmail { get; set; } = "99016006V";

    [Description("Proxy password (mode 2). Default: caule123")]
    [DefaultValue("caule123")]
    public string? UserPwdProxyEmail { get; set; } = "caule123";

    [Description("Proxy domain (mode 2)")]
    public string? domainProxyEmail { get; set; }

    [Description("Proxy address (mode 2). Default: http://10.37.190.37:3128")]
    [DefaultValue("http://10.37.190.37:3128")]
    public string? ProxyAddressProxyEmail { get; set; } = "http://10.37.190.37:3128";

    [Description("SMTP username (mode 5). Default: avisos.lavander@ssibe.cat")]
    [DefaultValue("avisos.lavander@ssibe.cat")]
    public string? SmtpUser { get; set; } = "avisos.lavander@ssibe.cat";

    [Description("SMTP password (mode 5). Default: LavAv2017")]
    [DefaultValue("LavAv2017")]
    public string? SmtpPass { get; set; } = "LavAv2017";

    [Description("SMTP port. Default: 25")]
    [DefaultValue(25)]
    public int? SmtpPort { get; set; } = 25;

    [Description("SMTP host/server")]
    public string? SmtpHost { get; set; }

    [Description("Override sender address to avoid domain blocks. Default: maquinasleon@lafabricadesoftware.com")]
    [DefaultValue("maquinasleon@lafabricadesoftware.com")]
    public string? SenderShippingSmtp { get; set; } = "maquinasleon@lafabricadesoftware.com";

    [Description("Primary mail recipient(s) for notifications")]
    public string? RecipientShippingMail { get; set; }

    [Description("Secondary mail recipient(s) for notifications")]
    public string? RecipientShippingMail2 { get; set; }

    [Description("Email subject for shipping/notification mails")]
    public string? SubjectShippingMail { get; set; }

    [Description("Alternate subject")]
    public string? SubjectShippingMail2 { get; set; }

    [Description("Display name for sender")]
    public string? UserNameSenderShippingMail { get; set; }

    [Description("Password for sender (non-SMTP modes)")]
    public string? PassSenderShippingMail { get; set; }

    [Description("Mail gateway URL (ASP-related modes)")]
    public string? UrlShippingMail { get; set; }

    [Description("Alternate gateway URL")]
    public string? UrlShippingMail2 { get; set; }

    [Description("Schema name used in schema-based mail modes")]
    public string? SchemaName { get; set; }

    [Description("When enabled, incident notifications go to technical service")]
    public bool? SendIncidencesToTechnicalService { get; set; }

    [Description("Enable mail on \"max garments per head reached\"")]
    public bool? SendMaxGarmentsHeadMail { get; set; }

    [Description("Enable mail on critical errors")]
    public bool? SendCriticalErrorsMail { get; set; }
}

