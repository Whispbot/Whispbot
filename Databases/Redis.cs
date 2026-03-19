using Serilog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Databases
{
    public static class Redis
    {
        private static ConnectionMultiplexer? _redis = null;
        private static ISubscriber? _pubSub = null;
        private static IDatabase? _db = null;
        private static bool _connecting = false;
        private static bool _connected = false;
        private static DateTime _lastConnectionAttempt = DateTime.MinValue;
        private static readonly TimeSpan _reconnectInterval = TimeSpan.FromMinutes(2); // Retry after 2 minutes

        /// <summary>
        /// Gets the Redis database. If the connection is not established or has been closed,
        /// it will attempt to reconnect if the last failed attempt was more than the reconnect interval ago.
        /// </summary>
        /// <returns>The Redis database, or null if connection is not available</returns>
        public static IDatabase? GetDatabase()
        {
            // If we're connected and the connection is valid, return the database
            if (_connected && _redis?.IsConnected == true)
            {
                return _db;
            }

            // If we're not connected and the last attempt was recent, return null
            if (!_connected && DateTime.UtcNow - _lastConnectionAttempt < _reconnectInterval)
            {
                return null;
            }

            // Try to reconnect
            if (Init())
            {
                return _db;
            }

            // Connection failed
            return null;
        }

        /// <summary>
        /// Gets the Redis subscriber for pub/sub operations
        /// </summary>
        /// <returns>The Redis subscriber, or null if connection is not available</returns>
        public static ISubscriber? GetSubscriber()
        {
            // If we're connected and the connection is valid, return the subscriber
            if (_connected && _redis?.IsConnected == true)
            {
                return _pubSub;
            }

            // If we're not connected and the last attempt was recent, return null
            if (!_connected && DateTime.UtcNow - _lastConnectionAttempt < _reconnectInterval)
            {
                return null;
            }

            // Try to reconnect
            if (Init())
            {
                return _pubSub;
            }

            // Connection failed
            return null;
        }

        public static bool Init()
        {
            if (_connecting) return false;
            _connecting = true;
            double start = DateTimeOffset.UtcNow.UtcTicks;
            Log.Information("Connecting to Redis...");
            _lastConnectionAttempt = DateTime.UtcNow;

            try
            {
                if (_redis != null)
                {
                    try
                    {
                        _redis.Close();
                        _redis.Dispose();
                    }
                    catch { }
                }

                string? redis_host = Environment.GetEnvironmentVariable("REDIS_HOST");
                string? redis_port = Environment.GetEnvironmentVariable("REDIS_PORT");
                string? redis_password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");

                var missingVars = new List<string>();
                if (string.IsNullOrEmpty(redis_host)) missingVars.Add("REDIS_HOST");
                if (string.IsNullOrEmpty(redis_port)) missingVars.Add("REDIS_PORT");
                if (string.IsNullOrEmpty(redis_password)) missingVars.Add("REDIS_PASSWORD");

                if (Config.isDev)
                {
                    string? public_url = Environment.GetEnvironmentVariable("REDIS_PUBLIC_URL");
                    if (!string.IsNullOrEmpty(public_url) && public_url.Contains('@') && public_url.Contains(':'))
                    {
                        string[] parts = public_url.Split('@');
                        if (parts.Length > 1)
                        {
                            string[] hostParts = parts[1].Split(':');
                            if (hostParts.Length > 1)
                            {
                                redis_host = hostParts[0];
                                redis_port = hostParts[1];
                            }
                        }
                    }
                    else if (missingVars.Contains("REDIS_HOST") || missingVars.Contains("REDIS_PORT"))
                    {
                        missingVars.Add("REDIS_PUBLIC_URL");
                    }
                }

                if (missingVars.Count > 0)
                {
                    Log.Fatal("ERROR: Missing required Redis environment variables:");
                    foreach (var var in missingVars)
                    {
                        Log.Fatal($"  - {var}");
                    }
                    Log.Fatal("\nPlease set these environment variables and restart the application.");
                    Logger.Shutdown();

                    Environment.Exit(1);
                    return false;
                }

                string redis_url = $"{redis_host}:{redis_port},password={redis_password},abortConnect=false";

                var options = ConfigurationOptions.Parse(redis_url);
                options.ConnectTimeout = 5000; // 5 seconds
                options.SyncTimeout = 5000;
                options.AbortOnConnectFail = false;

                _redis = ConnectionMultiplexer.Connect(options);
                _db = _redis.GetDatabase();
                _pubSub = _redis.GetSubscriber();

                _db.Ping();

                Log.Information($"Connected to Redis in {(DateTimeOffset.UtcNow.UtcTicks - start) / 10000}ms");
                _connected = true;
                _connecting = false;
                return true;
            }
            catch (RedisConnectionException ex)
            {
               Log.Error($"Redis connection error: {ex.Message}");
                _connected = false;
                _connecting = false;
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error during Redis connection: {ex.Message}");
                _connected = false;
                _connecting = false;
                return false;
            }
        }

        public static bool IsConnected => _connected;

        /// <summary>
        /// Checks if the Redis connection is still valid
        /// </summary>
        public static bool IsConnectionValid()
        {
            if (_redis == null || !_redis.IsConnected)
            {
                return false;
            }

            try
            {
                _db?.Ping();
                return true;
            }
            catch
            {
                _connected = false;
                return false;
            }
        }
    }
}