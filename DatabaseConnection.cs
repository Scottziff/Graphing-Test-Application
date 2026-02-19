using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace Graphing_Test_Application
{
    // ============================================================
    // DatabaseConfig & DatabaseConnection - Shared AlpacaTrader pattern
    // Loads config from %AppData%\AlpacaTrader\dbconfig.json
    // ============================================================
    public class DatabaseConfig
    {
        public string Server { get; set; } = @"Tiger2023\ZProduction";
        public string Database { get; set; } = "Stock System";
        public string TableName { get; set; } = "Schwab Positions";
        public bool UseWindowsAuth { get; set; } = true;
        public bool TrustServerCertificate { get; set; } = true;

        public string ConnectionString
        {
            get
            {
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = Server,
                    InitialCatalog = Database,
                    IntegratedSecurity = UseWindowsAuth,
                    TrustServerCertificate = TrustServerCertificate
                };
                return builder.ConnectionString;
            }
        }
    }

    public class DatabaseConnection
    {
        private static string AppDataPath =>
            Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), "AlpacaTrader");

        private static string DbConfigPath =>
            Path.Combine(AppDataPath, "dbconfig.json");

        public DatabaseConfig LoadConfig()
        {
            try
            {
                if (File.Exists(DbConfigPath))
                {
                    var json = File.ReadAllText(DbConfigPath);
                    return JsonConvert.DeserializeObject<DatabaseConfig>(json)
                            ?? new DatabaseConfig();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading database config: {ex.Message}");
            }

            return new DatabaseConfig();
        }

        public async Task<bool> TestConnectionAsync()
        {
            var config = LoadConfig();
            try
            {
                using (var connection = new SqlConnection(config.ConnectionString))
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<SqlConnection> GetOpenConnectionAsync()
        {
            var config = LoadConfig();
            var connection = new SqlConnection(config.ConnectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}
