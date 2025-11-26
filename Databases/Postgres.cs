using Npgsql;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Extensions;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Databases
{
    public static class Postgres
    {
        private static NpgsqlDataSource? _dataSource = null;
        private static bool _initialized = false;
        private static DateTime _lastConnectionAttempt = DateTime.MinValue;
        private static readonly TimeSpan _reconnectInterval = TimeSpan.FromMinutes(2); // Retry after 2 minutes

        private static readonly TimeSpan _pingMeasureInterval = TimeSpan.FromMinutes(5);
        private static double _ping = -1d;
        private static bool _initializing = false;

        /// <summary>
        /// The database ping in ms
        /// </summary>
        public static double Ping
        {
            get
            {
                if (_ping < 0 || DateTime.UtcNow - _lastConnectionAttempt > _pingMeasureInterval)
                {
                    _ping = MeasurePing();
                }
                return _ping;
            }
        }

        public static double MeasurePing()
        {
            if (_dataSource == null)
            {
                return -1d;
            }
            double start = DateTimeOffset.UtcNow.UtcTicks;
            try
            {
                using var connection = _dataSource.OpenConnection();
                using var command = new NpgsqlCommand("SELECT 1", connection);
                command.ExecuteScalar();
                return (DateTimeOffset.UtcNow.UtcTicks - start) / 10000;
            }
            catch
            {
                return -1d;
            }
        }

        /// <summary>
        /// Gets a database connection from the connection pool. If the data source is not initialized,
        /// it will attempt to initialize if the last failed attempt was more than the reconnect interval ago.
        /// </summary>
        /// <returns>A database connection from the pool, or null if data source is not available</returns>
        public static NpgsqlConnection? GetConnection()
        {
            // If we have a data source, get a connection from the pool
            if (_initialized && _dataSource != null)
            {
                try
                {
                    return _dataSource.OpenConnection();
                }
                catch
                {
                    // If we can't get a connection, mark as not initialized and fall through to reinit
                    _initialized = false;
                }
            }

            // If we're not initialized and the last attempt was recent, return null
            if (!_initialized && DateTime.UtcNow - _lastConnectionAttempt < _reconnectInterval)
            {
                return null;
            }

            // Try to initialize
            if (Init())
            {
                try
                {
                    return _dataSource?.OpenConnection();
                }
                catch
                {
                    return null;
                }
            }

            // Initialization failed
            return null;
        }

        public static bool Init()
        {
            if (_initializing) return false;
            _initializing = true;
            double start = DateTimeOffset.UtcNow.UtcTicks;
            Log.Information("Initializing postgres connection pool...");
            _lastConnectionAttempt = DateTime.UtcNow;

            try
            {
                // Dispose existing data source if it exists
                if (_dataSource != null)
                {
                    try
                    {
                        _dataSource.Dispose();
                    }
                    catch { }
                }

                string? host = Environment.GetEnvironmentVariable("DB_HOST");
                string? port = Environment.GetEnvironmentVariable("DB_PORT");
                string? username = Environment.GetEnvironmentVariable("DB_USERNAME");
                string? password = Environment.GetEnvironmentVariable("DB_PASSWORD");
                string? database = Environment.GetEnvironmentVariable("DB_DATABASE");

                var missingVars = new List<string>();
                if (string.IsNullOrEmpty(host)) missingVars.Add("DB_HOST");
                if (string.IsNullOrEmpty(port)) missingVars.Add("DB_PORT");
                if (string.IsNullOrEmpty(username)) missingVars.Add("DB_USERNAME");
                if (string.IsNullOrEmpty(password)) missingVars.Add("DB_PASSWORD");
                if (string.IsNullOrEmpty(database)) missingVars.Add("DB_DATABASE");

                if (Config.IsDev)
                {
                    string? public_url = Environment.GetEnvironmentVariable("DB_PUBLIC_URL");
                    if (!string.IsNullOrEmpty(public_url) && public_url.Contains('@') && public_url.Contains(':'))
                    {
                        string[] parts = public_url.Split('@');
                        if (parts.Length > 1)
                        {
                            string[] hostParts = parts[1].Split(':');
                            if (hostParts.Length > 1)
                            {
                                host = hostParts[0];
                                string[] portParts = hostParts[1].Split('/');
                                if (portParts.Length > 0)
                                {
                                    port = portParts[0];
                                }
                            }
                        }
                    }
                    else if (missingVars.Contains("DB_HOST") || missingVars.Contains("DB_PORT"))
                    {
                        missingVars.Add("DB_PUBLIC_URL");
                    }
                }

                if (missingVars.Count > 0)
                {
                    Log.Fatal("ERROR: Missing required environment variables:");
                    foreach (var var in missingVars)
                    {
                        Log.Fatal($"  - {var}");
                    }
                    Log.Fatal("\nPlease set these environment variables and restart the application.");
                    Logger.Shutdown();

                    Environment.Exit(1);
                    return false;
                }

                // Build connection string with connection pooling parameters
                var connectionStringBuilder = new NpgsqlConnectionStringBuilder
                {
                    Host = host,
                    Port = int.Parse(port!),
                    Username = username,
                    Password = password,
                    Database = database,
                    Timeout = 15,
                    CommandTimeout = 30,
                    // Connection pooling settings
                    MinPoolSize = 5,
                    MaxPoolSize = 20,
                    ConnectionIdleLifetime = 300, // 5 minutes
                    ConnectionPruningInterval = 10 // 10 seconds
                };

                // Create data source with connection pooling
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ToString());
                _dataSource = dataSourceBuilder.Build();

                // Test the connection pool
                using (var connection = _dataSource.OpenConnection())
                {
                    using var command = new NpgsqlCommand("SELECT 1", connection);
                    command.ExecuteNonQuery();
                }

                Log.Information($"Postgres connection pool initialized in {(DateTimeOffset.UtcNow.UtcTicks - start) / 10000}ms");
                _initialized = true;
                _initializing = false;
                return true;
            }
            catch (NpgsqlException ex)
            {
                Log.Error($"Database connection pool initialization error: {ex.Message}");
                _initialized = false;
                _initializing = false;
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error during database connection pool initialization: {ex.Message}");
                _initialized = false;
                _initializing = false;
                return false;
            }
        }

        /// <summary>
        /// Checks if the connection pool is initialized and available
        /// </summary>
        /// <returns></returns>
        public static bool IsConnected()
        {
            return _initialized && _dataSource != null;
        }

        /// <summary>
        /// Checks if the connection pool is still valid by testing a connection
        /// </summary>
        public static bool IsConnectionValid()
        {
            if (_dataSource == null)
            {
                return false;
            }

            try
            {
                using var connection = _dataSource.OpenConnection();
                using var command = new NpgsqlCommand("SELECT 1", connection);
                command.ExecuteScalar();
                return true;
            }
            catch
            {
                _initialized = false;
                return false;
            }
        }

        private static NpgsqlCommand AddArgs(this NpgsqlCommand command, List<object> args)
        {
            int i = 1;
            foreach (var arg in args)
            {
                command.Parameters.AddWithValue($"@{i}", arg);
                i++;
            }
            return command;
        }

        public static NpgsqlTransaction? BeginTransaction()
        {
            using var connection = GetConnection();
            if (connection is null) return null;

            return connection.BeginTransaction();
        }

        public static List<T>? Select<T>(string sql, List<object>? args = null, NpgsqlTransaction? transaction = null) where T : new()
        {
            using var connection = transaction?.Connection ?? GetConnection();
            if (connection is null) return null;

            using var command = new NpgsqlCommand(sql, connection, transaction);
            command.AddArgs(args ?? []);

            using var reader = command.ExecuteReader();
            return reader.ToList<T>();
        }

        public static List<dynamic>? Select(string sql, List<object>? args = null, NpgsqlTransaction? transaction = null)
        {
            using var connection = transaction?.Connection ?? GetConnection();
            if (connection is null) return null;

            using var command = new NpgsqlCommand(sql, connection, transaction);
            command.AddArgs(args ?? []);

            using var reader = command.ExecuteReader();
            return reader.ToDynamicList();
        }

        public static T? SelectFirst<T>(string sql, List<object>? args = null, NpgsqlTransaction? transaction = null) where T : new()
        {
            using var connection = transaction?.Connection ?? GetConnection();
            if (connection is null) return default;

            using var command = new NpgsqlCommand(sql, connection, transaction);
            command.AddArgs(args ?? []);

            using var reader = command.ExecuteReader();
            return reader.FirstOrDefault<T>();
        }

        public static int Execute(string sql, List<object>? args = null, NpgsqlTransaction? transaction = null)
        {
            using var connection = transaction?.Connection ?? GetConnection();
            if (connection is null) return -1;

            using var command = new NpgsqlCommand(sql, connection, transaction);
            command.AddArgs(args ?? []);

            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// Disposes the connection pool and cleans up resources
        /// </summary>
        public static void Dispose()
        {
            try
            {
                _dataSource?.Dispose();
                _dataSource = null;
                _initialized = false;
                Log.Information("Postgres connection pool disposed");
            }
            catch (Exception ex)
            {
                Log.Error($"Error disposing postgres connection pool: {ex.Message}");
            }
        }
    }

    public class PostgresCount
    {
        public long count;
    }
}