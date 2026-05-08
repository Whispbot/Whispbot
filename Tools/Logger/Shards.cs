using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Sharding;
using Whispbot.Databases;
using Serilog;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Tools.Logger
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

        public static void LogShardInfo(Shard shard, Status status)
        {
            // If in dev env or there are ignored guilds (another instance is starting) ignore
            if (Config.EnvId != EnvironmentType.Prod || Config.commands?.ignoreGuilds.Count > 0) return;

            int clusterId = Config.cluster;
            int shardId = shard.id;
            double ping = shard.client.ping;
            int guilds = DiscordCache.Guilds.Count;
            int users = DiscordCache.Users.Count;
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
            if (Config.EnvId != EnvironmentType.Prod || Config.commands?.ignoreGuilds.Count > 0) return;

            int i = 2;
            Postgres.Execute($@"
                INSERT INTO shard_updates (cluster_id, shard_id, ping, guilds, users, status, cluster_mem_usage_mb, cluster_cpu_usage_p)
                VALUES {Enumerable.Range(0, endShard - startShard + 1).Select(_ => $"(@1, @{i++}, 0, 0, 0, {(int)Status.WAITING}, 0, 0)").Join(", ")}
            ", [clusterId, .. Enumerable.Range(startShard, endShard - startShard + 1)]);
        }

        public static void Init(ShardingManager manager)
        {
            manager.shards.ForEach(shard =>
            {
                shard.client.Ready        += (_, _) => LogShardInfo(shard, Status.ONLINE);
                shard.client.Connecting   += (_, _) => LogShardInfo(shard, Status.CONNECTING);
                shard.client.Disconnected += (_, _) => LogShardInfo(shard, Status.OFFLINE);
                shard.client.Connecting   += (_, _) => LogShardInfo(shard, Status.STARTING);
                shard.client.Pinged       += (_, _) => LogShardInfo(shard, Status.ONLINE);
            });

            int start = manager.shards.Min(s => s.id);
            int end = manager.shards.Max(s => s.id);
            InitDB(Config.cluster, start, end);
        }
    }
}
