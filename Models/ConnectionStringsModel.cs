using System.ComponentModel;

namespace AppSettingsEditor.Models;

internal class ConnectionStringsModel
{
    [Description("Connection string to the main database. Use: Data Source=server;Initial Catalog=dbname;Trust Server Certificate=True;")]
    public string? DefaultConnection { get; set; }

    [Description("Connection string to the local/offline database. Use: Data Source=path or Data Source=localhost;Initial Catalog=dbname;Trust Server Certificate=True;")]
    public string? DefaultOfflineConnection { get; set; }
}

