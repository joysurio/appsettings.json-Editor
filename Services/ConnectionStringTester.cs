using Microsoft.Data.SqlClient;

namespace AppSettingsEditor.Services;

internal class ConnectionStringTestResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

internal class ConnectionStringTester
{
    public ConnectionStringTestResult TestConnection(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new ConnectionStringTestResult
            {
                Success = false,
                ErrorMessage = "Connection string is empty"
            };
        }

        // Validate connection string format and authentication
        var validationResult = ValidateConnectionString(connectionString);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        try
        {
            // Try SQL Server connection (most common)
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            
            // Test with a simple query
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.ExecuteScalar();
            
            return new ConnectionStringTestResult { Success = true };
        }
        catch (SqlException ex)
        {
            // Provide more helpful error messages for common authentication issues
            var errorMessage = ex.Message;
            
            // Check for authentication-related errors
            if (ex.Number == 18456) // Login failed for user
            {
                errorMessage = $"Authentication failed: {ex.Message}\n\n" +
                    "Please verify:\n" +
                    "- Username and Password are correct (for SQL Authentication)\n" +
                    "- Or use 'Trusted_Connection=true' for Windows Authentication";
            }
            else if (ex.Number == 2 || ex.Message.Contains("network-related") || ex.Message.Contains("instance"))
            {
                errorMessage = $"Cannot connect to server: {ex.Message}\n\n" +
                    "Please verify:\n" +
                    "- Server name/address is correct\n" +
                    "- Server is running and accessible\n" +
                    "- Firewall allows the connection";
            }
            else if (ex.Message.Contains("user") && ex.Message.Contains("password"))
            {
                errorMessage = $"Authentication error: {ex.Message}\n\n" +
                    "The connection string requires authentication. Please add:\n" +
                    "- For SQL Authentication: 'User ID=username;Password=password;'\n" +
                    "- For Windows Authentication: 'Trusted_Connection=true;'";
            }
            
            return new ConnectionStringTestResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
        catch (Exception ex)
        {
            // Try to detect if it's a different provider format
            // For now, return the error
            return new ConnectionStringTestResult
            {
                Success = false,
                ErrorMessage = $"Connection test failed: {ex.Message}"
            };
        }
    }

    private ConnectionStringTestResult ValidateConnectionString(string connectionString)
    {
        // Normalize the connection string for parsing (case-insensitive)
        var normalized = connectionString.ToLowerInvariant();
        
        // Check if connection string has Server/Data Source
        if (!normalized.Contains("server=") && !normalized.Contains("data source="))
        {
            return new ConnectionStringTestResult
            {
                Success = false,
                ErrorMessage = "Connection string is missing Server or Data Source.\n\n" +
                    "Example format: Server=localhost;Database=MyDB;Trusted_Connection=true;"
            };
        }

        // Check for authentication method
        bool hasTrustedConnection = normalized.Contains("trusted_connection=true") || 
                                   normalized.Contains("integrated security=true");
        bool hasUserId = normalized.Contains("user id=") || normalized.Contains("uid=");
        bool hasPassword = normalized.Contains("password=") || normalized.Contains("pwd=");

        // If using SQL Authentication, both User ID and Password are required
        if (hasUserId && !hasPassword)
        {
            return new ConnectionStringTestResult
            {
                Success = false,
                ErrorMessage = "Connection string has User ID but is missing Password.\n\n" +
                    "For SQL Server Authentication, add: Password=yourpassword;\n" +
                    "Or use Windows Authentication: Trusted_Connection=true;"
            };
        }

        if (hasPassword && !hasUserId)
        {
            return new ConnectionStringTestResult
            {
                Success = false,
                ErrorMessage = "Connection string has Password but is missing User ID.\n\n" +
                    "For SQL Server Authentication, add: User ID=username;\n" +
                    "Or use Windows Authentication: Trusted_Connection=true;"
            };
        }

        // If neither authentication method is specified, suggest one
        if (!hasTrustedConnection && !hasUserId && !hasPassword)
        {
            return new ConnectionStringTestResult
            {
                Success = false,
                ErrorMessage = "Connection string is missing authentication credentials.\n\n" +
                    "Please add one of the following:\n" +
                    "1. For Windows Authentication: Trusted_Connection=true;\n" +
                    "2. For SQL Authentication: User ID=username;Password=password;\n\n" +
                    "Example: Server=localhost;Database=MyDB;Trusted_Connection=true;"
            };
        }

        return new ConnectionStringTestResult { Success = true };
    }
}

