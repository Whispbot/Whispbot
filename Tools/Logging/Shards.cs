using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Whispbot.Databases;
using Serilog;
using Discord.WebSocket;
using Whispbot.Commands;
using Whispbot.Extensions;

namespace Whispbot.Tools.Logging
{
    public static class ShardLogger
    {
        //    id uuid default gen_random_uuid() not null
        //    primary key,
        //cluster_id           integer not null,
        //shard_id             integer not null,
        //ping                 real not null,
        //guilds               smallint not null,
        //users                integer not null,
        //status               smallint not null,
        //cluster_mem_usage_mb smallint,
        //cluster_cpu_usage_p smallint

        public enum Status
        {
            WAITING = 0,
            CONNECTING = 1,
            STARTING = 2,
            ONLINE = 3,
            OFFLINE = 4
        }

        public static async Task LogShardInfo(DiscordSocketClient shard, Status status)
        {
            // If in dev env or there are ignored guilds (another instance is starting) ignore
            if (Config.EnvId != EnvironmentType.Prod || CommandManager.ignoreGuilds.Count > 0) return;

            int clusterId = Config.cluster;
            int shardId = shard.ShardId;
            int ping = shard.Latency;
            int guilds = shard.Guilds.Count;
            int users = 0;
            int memUsageMb = (int)(Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024));
            int cpuUsageP = 0;

            int result = Postgres.Execute(@"
                INSERT INTO shard_updates (cluster_id, shard_id, ping, guilds, users, status, cluster_mem_usage_mb, cluster_cpu_usage_p)
                VALUES (@1, @2, @3, @4, @5, @6, @7, @8)
            ", [clusterId, shardId, ping, guilds, users, (int)status, memUsageMb, cpuUsageP]);

            if (result == 0)
            {
                Log.Warning($"Failed to update shard status: Cluster {clusterId} Shard {shardId}");
            }
        }

        public static void InitDB(int clusterId, int startShard, int endShard)
        {
            // If in dev env or there are ignored guilds (another instance is starting) ignore
            if (Config.EnvId != EnvironmentType.Prod || CommandManager.ignoreGuilds.Count > 0) return;

            int i = 2;
            Postgres.Execute($@"
                INSERT INTO shard_updates (cluster_id, shard_id, ping, guilds, users, status, cluster_mem_usage_mb, cluster_cpu_usage_p)
                VALUES {Enumerable.Range(0, endShard - startShard + 1).Select(_ => $"(@1, @{i++}, 0, 0, 0, {(int)Status.WAITING}, 0, 0)").Join(", ")}
            ", [clusterId, .. Enumerable.Range(startShard, endShard - startShard + 1)]);
        }

        public static void Init(DiscordShardedClient client)
        {
            client.ShardReady           +=  (c)          => LogShardInfo(c, Status.ONLINE);
            client.ShardConnected       +=  (c)          => LogShardInfo(c, Status.ONLINE);
            client.ShardDisconnected    +=  (e, c)       => LogShardInfo(c, Status.OFFLINE);
            client.ShardLatencyUpdated  +=  (_, l, c)    => LogShardInfo(c, Status.ONLINE);

            int start = client.Shards.Min(s => s.ShardId);
            int end = client.Shards.Max(s => s.ShardId);
            InitDB(Config.cluster, start, end);
        }
    }
}
