using System;
using System.Collections.Generic;
using System.Text;
using YellowMacaroni.Redis.Queue;

namespace Whispbot.Tools.Games.ERLC
{
    public static class ERLCQueue
    {
        private static QueueClient? _client;
        public static QueueClient Client
        {
            get
            {
                string host = Environment.GetEnvironmentVariable($"REDIS_{(Config.isDev ? "PUBLIC_" : "")}HOST") ?? throw new InvalidOperationException($"Environment variable 'REDIS_{(Config.isDev ? "PUBLIC_" : "")}HOST' is not set");
                string port = Environment.GetEnvironmentVariable($"REDIS_{(Config.isDev ? "PUBLIC_" : "")}PORT") ?? throw new InvalidOperationException($"Environment variable 'REDIS_{(Config.isDev ? "PUBLIC_" : "")}PORT' is not set");
                string password = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? throw new InvalidOperationException("Environment variable 'REDIS_PASSWORD' is not set");

                _client ??= new($"{host}:{port},password={password},abortConnect=false");
                return _client ?? throw new InvalidOperationException("Failed to initialize ERLC queue client...");
            }
        }

        private static RedisQueue? _queue;
        public static RedisQueue Queue
        {
            get
            {
                _queue ??= new(Client, $"prc_api{(Config.isDev ? "_dev" : "")}", new QueueOptions
                {
                    GroupName = "bot",
                    MachineId = $"bot-{Environment.GetEnvironmentVariable("RAILWAY_REPLICA_ID") ?? "dev"}"
                });
                return _queue ?? throw new InvalidOperationException("Failed to initialize ERLC queue...");
            }
        }
    }
}
