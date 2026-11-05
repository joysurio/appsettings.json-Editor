using Microsoft.Data.SqlClient;

namespace AppSettingsEditor.Services;

/// <summary>
/// Database operations matching <c>RemoteMachineAgent.Services.DatabaseService</c> (test, list machines, insert command).
/// </summary>
internal class RemoteMachineAgentOperationsService
{
    internal sealed class OperationResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
    }

    internal sealed class PendingCommand
    {
        public int Id { get; init; }
        public string CommandType { get; init; } = string.Empty;
        public string CommandData { get; init; } = string.Empty;
    }

    internal sealed class MachineCatalogItem
    {
        public string Id { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    internal sealed class MasterControlEmailSettings
    {
        public string Server { get; init; } = string.Empty;
        public int Port { get; init; }
        public bool UseAuthentication { get; init; }
        public bool EnableSsl { get; init; }
        public string User { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string From { get; init; } = string.Empty;
    }

    private const string EnsureSchemaSql = """
        IF OBJECT_ID(N'dbo.Machines', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Machines
            (
                MachineId NVARCHAR(100) NOT NULL PRIMARY KEY,
                MachineName NVARCHAR(255) NULL,
                IPAddress NVARCHAR(64) NULL,
                LastHeartbeat DATETIME2(0) NULL,
                IsOnline BIT NOT NULL CONSTRAINT DF_Machines_IsOnline DEFAULT(0)
            );
        END;

        IF OBJECT_ID(N'dbo.Commands', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Commands
            (
                Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                MachineId NVARCHAR(100) NOT NULL,
                CommandType NVARCHAR(100) NOT NULL,
                CommandData NVARCHAR(MAX) NULL,
                Status NVARCHAR(50) NOT NULL CONSTRAINT DF_Commands_Status DEFAULT(N'Pending'),
                CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Commands_CreatedAt DEFAULT(SYSUTCDATETIME()),
                ExecutedAt DATETIME2(0) NULL,
                Result NVARCHAR(MAX) NULL,
                ErrorMessage NVARCHAR(MAX) NULL
            );

            CREATE INDEX IX_Commands_MachineId_Status_CreatedAt
                ON dbo.Commands (MachineId, Status, CreatedAt DESC);
        END;
        """;

    private const string EnsureMissingColumnsSql = """
        -- Machines (do not drop: other DB objects may reference this table).
        IF COL_LENGTH('dbo.Machines', 'MachineId') IS NULL
            ALTER TABLE dbo.Machines ADD MachineId NVARCHAR(100) NULL;
        IF COL_LENGTH('dbo.Machines', 'MachineName') IS NULL
            ALTER TABLE dbo.Machines ADD MachineName NVARCHAR(255) NULL;
        IF COL_LENGTH('dbo.Machines', 'IPAddress') IS NULL
            ALTER TABLE dbo.Machines ADD IPAddress NVARCHAR(64) NULL;
        IF COL_LENGTH('dbo.Machines', 'LastHeartbeat') IS NULL
            ALTER TABLE dbo.Machines ADD LastHeartbeat DATETIME2(0) NULL;
        IF COL_LENGTH('dbo.Machines', 'IsOnline') IS NULL
            ALTER TABLE dbo.Machines ADD IsOnline BIT NOT NULL CONSTRAINT DF_Machines_IsOnline_Auto DEFAULT(0) WITH VALUES;

        -- Commands (do not drop: keep compatibility with existing foreign keys).
        IF COL_LENGTH('dbo.Commands', 'Id') IS NULL
            ALTER TABLE dbo.Commands ADD Id INT IDENTITY(1,1) NOT NULL;
        IF COL_LENGTH('dbo.Commands', 'MachineId') IS NULL
            ALTER TABLE dbo.Commands ADD MachineId NVARCHAR(100) NULL;
        IF COL_LENGTH('dbo.Commands', 'CommandType') IS NULL
            ALTER TABLE dbo.Commands ADD CommandType NVARCHAR(100) NULL;
        IF COL_LENGTH('dbo.Commands', 'CommandData') IS NULL
            ALTER TABLE dbo.Commands ADD CommandData NVARCHAR(MAX) NULL;
        IF COL_LENGTH('dbo.Commands', 'Status') IS NULL
            ALTER TABLE dbo.Commands ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_Commands_Status_Auto DEFAULT(N'Pending') WITH VALUES;
        IF COL_LENGTH('dbo.Commands', 'CreatedAt') IS NULL
            ALTER TABLE dbo.Commands ADD CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Commands_CreatedAt_Auto DEFAULT(SYSUTCDATETIME()) WITH VALUES;
        IF COL_LENGTH('dbo.Commands', 'ExecutedAt') IS NULL
            ALTER TABLE dbo.Commands ADD ExecutedAt DATETIME2(0) NULL;
        IF COL_LENGTH('dbo.Commands', 'Result') IS NULL
            ALTER TABLE dbo.Commands ADD Result NVARCHAR(MAX) NULL;
        IF COL_LENGTH('dbo.Commands', 'ErrorMessage') IS NULL
            ALTER TABLE dbo.Commands ADD ErrorMessage NVARCHAR(MAX) NULL;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.Commands')
              AND name = N'IX_Commands_MachineId_Status_CreatedAt'
        )
        BEGIN
            CREATE INDEX IX_Commands_MachineId_Status_CreatedAt
                ON dbo.Commands (MachineId, Status, CreatedAt DESC);
        END;
        """;

    public async Task<bool> EnsureAgentSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var result = await EnsureAgentSchemaDetailedAsync(connectionString, cancellationToken);
        return result.Success;
    }

    public async Task<OperationResult> EnsureAgentSchemaDetailedAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new OperationResult { Success = false, ErrorMessage = "Connection string is empty." };

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaCompatibilityAsync(connection, cancellationToken);
            return new OperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var result = await TestConnectionDetailedAsync(connectionString, cancellationToken);
        return result.Success;
    }

    public async Task<OperationResult> TestConnectionDetailedAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new OperationResult { Success = false, ErrorMessage = "Connection string is empty." };
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaCompatibilityAsync(connection, cancellationToken);
            return new OperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<(List<MachineCatalogItem> Items, string? ErrorMessage)> GetMachineCatalogAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var items = new List<MachineCatalogItem>();
        if (string.IsNullOrWhiteSpace(connectionString))
            return (items, "Connection string is empty.");

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            const string query = """
                SELECT CAST(id AS nvarchar(100)) AS id, CAST(Descripcion AS nvarchar(255)) AS Descripcion
                FROM dbo.MasterMaquinaDispensacion
                ORDER BY Descripcion;
                """;
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
                var description = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    items.Add(new MachineCatalogItem
                    {
                        Id = id,
                        Description = string.IsNullOrWhiteSpace(description) ? id : description
                    });
                }
            }

            return (items, null);
        }
        catch (Exception ex)
        {
            return (items, ex.Message);
        }
    }

    public async Task<(MasterControlEmailSettings? Settings, string? ErrorMessage)> GetMasterControlEmailSettingsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return (null, "Connection string is empty.");

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string query = """
                SELECT TOP (1)
                    CAST(EmailServidor AS nvarchar(255)) AS EmailServidor,
                    CAST(EmailPuerto AS nvarchar(50)) AS EmailPuerto,
                    CAST(EmailUseAutentication AS nvarchar(20)) AS EmailUseAutentication,
                    CAST(EmailSSL AS nvarchar(20)) AS EmailSSL,
                    CAST(EmailUser AS nvarchar(255)) AS EmailUser,
                    CAST(EmailPasswd AS nvarchar(255)) AS EmailPasswd
                FROM dbo.mastercontrol;
                """;
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return (null, "No rows found in dbo.mastercontrol.");

            string server = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
            string portRaw = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
            string useAuthRaw = reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim();
            string sslRaw = reader.IsDBNull(3) ? string.Empty : reader.GetString(3).Trim();
            string user = reader.IsDBNull(4) ? string.Empty : reader.GetString(4).Trim();
            string password = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);

            if (string.IsNullOrWhiteSpace(server))
                return (null, "mastercontrol.EmailServidor is empty.");

            if (!int.TryParse(portRaw, out var port) || port <= 0)
                return (null, $"mastercontrol.EmailPuerto has invalid value: '{portRaw}'.");

            var useAuthentication = IsTrueValue(useAuthRaw);
            var enableSsl = IsTrueValue(sslRaw);

            if (useAuthentication && (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)))
            {
                return (null, "mastercontrol requires EmailUser and EmailPasswd when EmailUseAutentication is enabled.");
            }

            var settings = new MasterControlEmailSettings
            {
                Server = server,
                Port = port,
                UseAuthentication = useAuthentication,
                EnableSsl = enableSsl,
                User = user,
                Password = password,
                From = string.IsNullOrWhiteSpace(user) ? "noreply@localhost" : user
            };
            return (settings, null);
        }
        catch (Exception ex)
        {
            return (null, $"Failed to load SMTP settings from dbo.mastercontrol: {ex.Message}");
        }
    }

    private static bool IsTrueValue(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        if (int.TryParse(rawValue.Trim(), out var numeric))
            return numeric == 1;

        return bool.TryParse(rawValue.Trim(), out var flag) && flag;
    }

    public async Task<List<string>> GetAllMachineIdsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(connectionString))
            return list;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaCompatibilityAsync(connection, cancellationToken);
            const string query = "SELECT DISTINCT MachineId FROM Machines ORDER BY MachineId";
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                list.Add(reader.GetString(0));
        }
        catch
        {
            // return what we have (empty on error)
        }

        return list;
    }

    public async Task<int?> InsertCommandAsync(
        string connectionString,
        string targetMachineId,
        string commandType,
        string commandData,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(targetMachineId) ||
            string.IsNullOrWhiteSpace(commandType))
            return null;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaCompatibilityAsync(connection, cancellationToken);
            const string query = """
                INSERT INTO Commands (MachineId, CommandType, CommandData, Status)
                VALUES (@MachineId, @CommandType, @CommandData, 'Pending');
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;
            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@MachineId", targetMachineId);
            command.Parameters.AddWithValue("@CommandType", commandType);
            command.Parameters.AddWithValue("@CommandData", commandData ?? string.Empty);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null ? Convert.ToInt32(result) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task UpsertMachineHeartbeatAsync(
        string connectionString,
        string machineId,
        string machineName,
        string ipAddress,
        bool isOnline = true,
        CancellationToken cancellationToken = default)
    {
        _ = await UpsertMachineHeartbeatDetailedAsync(
            connectionString,
            machineId,
            machineName,
            ipAddress,
            isOnline,
            cancellationToken);
    }

    public async Task<OperationResult> UpsertMachineHeartbeatDetailedAsync(
        string connectionString,
        string machineId,
        string machineName,
        string ipAddress,
        bool isOnline = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(machineId))
            return new OperationResult { Success = false, ErrorMessage = "Connection string or MachineId is empty." };

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaCompatibilityAsync(connection, cancellationToken);

            var machineIdType = await GetColumnDataTypeAsync(connection, "Machines", "MachineId", cancellationToken);
            if (!IsTextSqlType(machineIdType))
            {
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = $"Machines.MachineId has type '{machineIdType ?? "unknown"}' but RemoteMachineAgent expects a text type (nvarchar/varchar)."
                };
            }

            const string query = """
                UPDATE dbo.Machines
                SET MachineName = @MachineName,
                    IPAddress = @IPAddress,
                    LastHeartbeat = SYSUTCDATETIME(),
                    IsOnline = @IsOnline
                WHERE MachineId = @MachineId;

                IF @@ROWCOUNT = 0
                BEGIN
                    INSERT INTO dbo.Machines (MachineId, MachineName, IPAddress, LastHeartbeat, IsOnline)
                    VALUES (@MachineId, @MachineName, @IPAddress, SYSUTCDATETIME(), @IsOnline);
                END
                """;

            await using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@MachineId", System.Data.SqlDbType.NVarChar, 100).Value = machineId;
            command.Parameters.Add("@MachineName", System.Data.SqlDbType.NVarChar, 255).Value = machineName ?? machineId;
            command.Parameters.Add("@IPAddress", System.Data.SqlDbType.NVarChar, 64).Value = ipAddress ?? string.Empty;
            command.Parameters.Add("@IsOnline", System.Data.SqlDbType.Bit).Value = isOnline;
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new OperationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static async Task EnsureSchemaCompatibilityAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        // Bootstrap empty DB first.
        await using (var bootstrap = new SqlCommand(EnsureSchemaSql, connection) { CommandTimeout = 30 })
        {
            await bootstrap.ExecuteNonQueryAsync(cancellationToken);
        }
        // Add missing columns/indexes only. Never drop tables; avoids FK-related failures.
        await using var migrate = new SqlCommand(EnsureMissingColumnsSql, connection) { CommandTimeout = 30 };
        await migrate.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> GetColumnDataTypeAsync(SqlConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        const string query = """
            SELECT DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName;
            """;

        await using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        cmd.Parameters.AddWithValue("@ColumnName", columnName);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result?.ToString();
    }

    private static bool IsTextSqlType(string? dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return false;

        return dataType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) ||
               dataType.Equals("varchar", StringComparison.OrdinalIgnoreCase) ||
               dataType.Equals("nchar", StringComparison.OrdinalIgnoreCase) ||
               dataType.Equals("char", StringComparison.OrdinalIgnoreCase) ||
               dataType.Equals("text", StringComparison.OrdinalIgnoreCase) ||
               dataType.Equals("ntext", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<PendingCommand?> ClaimNextPendingCommandAsync(
        string connectionString,
        string machineId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(machineId))
            return null;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaCompatibilityAsync(connection, cancellationToken);

            const string query = """
                ;WITH cte AS
                (
                    SELECT TOP (1) *
                    FROM dbo.Commands WITH (READPAST, UPDLOCK, ROWLOCK)
                    WHERE MachineId = @MachineId AND Status = 'Pending'
                    ORDER BY CreatedAt ASC, Id ASC
                )
                UPDATE cte
                SET Status = 'InProgress'
                OUTPUT inserted.Id, inserted.CommandType, COALESCE(inserted.CommandData, N'');
                """;

            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@MachineId", machineId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new PendingCommand
            {
                Id = reader.GetInt32(0),
                CommandType = reader.GetString(1),
                CommandData = reader.GetString(2)
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CompleteCommandAsync(
        string connectionString,
        int commandId,
        string? resultMessage,
        CancellationToken cancellationToken = default)
    {
        return await UpdateCommandFinalStatusAsync(connectionString, commandId, "Completed", resultMessage, null, cancellationToken);
    }

    public async Task<bool> FailCommandAsync(
        string connectionString,
        int commandId,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        return await UpdateCommandFinalStatusAsync(connectionString, commandId, "Failed", null, errorMessage, cancellationToken);
    }

    private static async Task<bool> UpdateCommandFinalStatusAsync(
        string connectionString,
        int commandId,
        string status,
        string? resultMessage,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || commandId <= 0)
            return false;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            const string query = """
                UPDATE dbo.Commands
                SET Status = @Status,
                    ExecutedAt = SYSUTCDATETIME(),
                    Result = @Result,
                    ErrorMessage = @ErrorMessage
                WHERE Id = @Id;
                """;
            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Status", status);
            command.Parameters.AddWithValue("@Result", (object?)resultMessage ?? DBNull.Value);
            command.Parameters.AddWithValue("@ErrorMessage", (object?)errorMessage ?? DBNull.Value);
            command.Parameters.AddWithValue("@Id", commandId);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
        catch
        {
            return false;
        }
    }
}
