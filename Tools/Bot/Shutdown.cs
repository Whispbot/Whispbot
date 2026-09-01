using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Whispbot.Commands;
using Whispbot.Databases;
using Whispbot.Tools.Logging;
using static Whispbot.Tools.Logging.Logging;

namespace Whispbot.Tools.Bot
{
    public static class Shutdown
    {
        public static void Init()
        {
            PosixSignalRegistration.Create(PosixSignal.SIGINT, async (e) => { e.Cancel = true; await Start(); });
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, async (e) => { e.Cancel = true; await Start(); });
            Console.CancelKeyPress += async (_, e) => { e.Cancel = true; await Start(); };
            AppDomain.CurrentDomain.ProcessExit += async (_, _) => { await Start(); };

            var subscriber = Redis.GetSubscriber();
            int attempts = 0;

            while (subscriber is null && attempts <= 10)
            {
                subscriber = Redis.GetSubscriber();
                attempts++;
                Thread.Sleep(1000 * attempts);
            }

            subscriber?.Subscribe("whispbot:ignore_guilds", (_, value) =>
            {
                var split = value.ToString().Split(':', 3);
                var ver = split[0];
                var build = split[1];
                var guildIds = split[2];

                if (ver != ((int)Config.EnvId).ToString()) return;
                if (build != (Environment.GetEnvironmentVariable("RAILWATY_DEPLOYMENT_ID") ?? "dev")) return;

                var guilds = JsonConvert.DeserializeObject<List<ulong>>(guildIds);

                CommandManager.ignoreGuilds.AddRange(guilds ?? []);
            });
        }

        private static bool _started = false;
        public static bool IsShuttingDown => _started;
        public static event Action? OnShuttingDown;
        public static async Task Start()
        {
            if (_started) return;
            _started = true;
            OnShuttingDown?.Invoke();
            Warning("Starting shutdown...");

            var client = Config.client;
            if (client is not null)
            {
                await client.LogoutAsync();
                await client.DisposeAsync();
            }

            Postgres.Dispose();
            Redis.Dispose();

            Info("Goodbye!");
            Logger.Shutdown();
            _signal.Release();
        }

        private static readonly SemaphoreSlim _signal = new(0, 1);
        public static void Wait()
        {
            _signal.Wait();
        }
        public static async Task WaitAsync()
        {
            await _signal.WaitAsync();
        }
    }
}
