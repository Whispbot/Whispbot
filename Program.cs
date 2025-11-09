using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Sharding;
using Sentry;
using Whispbot.Commands;
using Whispbot;
using Whispbot.Databases;
using Npgsql;
using Whispbot.Extensions;
using Serilog;
using Whispbot.API;
using YellowMacaroni.Discord.Extentions;
using Whispbot.Interactions;
using Whispbot.Tools;

Logger.Initialize();

bool dev = Config.IsDev;
if (dev) Log.Information("Running in development mode.");

string? token = dev ? Environment.GetEnvironmentVariable("DEV_TOKEN") : Environment.GetEnvironmentVariable("CLIENT_TOKEN");

if (token is null)
{
    Log.Fatal("Please set the CLIENT_TOKEN environment variable.");
    Logger.Shutdown();
    return;
}

_ = Task.Run(Redis.Init);
_ = Task.Run(Postgres.Init);
_ = Task.Run(SentryConnection.Init);
_ = Task.Run(Strings.GetLanguages);

Thread APIThread = new(new ThreadStart(() =>
{
    if (Config.IsDev)
    {
        while (Config.cluster == -1) Thread.Sleep(100);
        if (Config.cluster != 0)
        {
            Log.Information("Skipping API startup in non-leader cluster.");
            return;
        }
    }

    WhispbotAPI.Start();
}))
{
    Name = "Whispbot API",
    IsBackground = true
};
APIThread.Start();

int clusters = 1;
if (!dev)
{
    clusters = await Railway.getReplicaCount();
}

Log.Information($"Starting cluster. Total clusters: {clusters}.");

Config.replica = new(
    Config.deploymentId,
    Config.replicaId ?? Guid.NewGuid().ToString(),
    clusters
);

ISubscriber? pubSub = null;

while (pubSub is null)
{
    pubSub = Redis.GetSubscriber();
    if (pubSub is null) {
        if (Config.IsDev) Log.Debug("[Debug] Waiting for redis to connect...");
        Thread.Sleep(500);
    }
}

if (!Config.IsDev)
{
    await Config.replica.Start();

    while (!Config.replica.active) Thread.Sleep(100);

    void register(RedisValue message)
    {
        string[] parts = message.ToString().Split(':');
        string deploymentId = parts[0];
        string replicaId = parts[1];

        if (deploymentId != Config.deploymentId) return;

        if (!Config.replicas.Contains(replicaId)) Config.replicas.Add(replicaId);

        pubSub.Publish($"{Config.deploymentId}-replicas", JsonConvert.SerializeObject(Config.replicas));
    }

    if (Config.replica.IsLeader)
    {
        Config.cluster = 0;
        Config.replicas.Add(Config.replicaId);

        pubSub.Subscribe("register", (channel, message) => { register(message); });

        _ = DiscordLogger.Log($"[{{dt}}] {{emoji.loading}} Re-clustering {clusters} clusters...".Process());
    }
    else
    {
        pubSub.Subscribe($"{Config.deploymentId}-replicas", (channel, message) =>
        {
            Config.replicas = JsonConvert.DeserializeObject<List<string>>(message.ToString()) ?? [];
            Config.cluster = Config.replicas.IndexOf(Config.replicaId);
        });
    }

    Config.replica.OnElected += (_, _) =>
    {
        pubSub.Subscribe("register", (channel, message) => { register(message); });
    };

    Config.replica.OnLostLeadership += (_, _) =>
    {
        pubSub.Unsubscribe("register");
    };

    while (Config.cluster == -1)
    {
        pubSub.Publish("register", $"{Config.deploymentId}:{Config.replicaId}");
        Thread.Sleep(200);
    }

    Log.Information($"Cluster {Config.cluster} established and waiting for start signal.");
}
else
{
    Config.cluster = 0;
    Config.replicas.Add("dev");
    Log.Warning("In development mode, skipping cluster init");
}

string? shardsEnv = Config.IsDev ? null : Environment.GetEnvironmentVariable("SHARDS");
int? shards = shardsEnv is null ? null : int.Parse(shardsEnv);

ShardingManager sharding = new(
    token,
    Intents.GuildMessages | Intents.MessageContent | Intents.Guilds,
    shards
);

int shardCount = sharding.shards.Count;
int clusterStart = (int)(Config.cluster * ((float)shardCount/(float)clusters));
int clusterEnd = (int)((Config.cluster + 1) * ((float)shardCount/(float)clusters));

sharding.shards = [..sharding.shards.Skip(clusterStart).Take(clusterEnd - clusterStart)];

CommandManager commands = new();
Config.commands = commands;
commands.Attach(sharding);

InteractionManager interactions = new();
Config.interactions = interactions;
interactions.Attach(sharding);

foreach (Shard shard in sharding.shards)
{
    shard.client.On("READY", (client, obj) =>
    {
        if (shard.id == clusterStart)
        {
            _ = Task.Run(async () => await Strings.GetEmojis(client));
        }

        _ = DiscordLogger.Log($"[{{dt}}] {{emoji.clockedin}} Shard `{shard.id}` of cluster `{Config.cluster}` is ready.".Process());
        Log.Information($"Cluster {Config.cluster.ToString().PadLeft((Config.replicas.Count - 1).ToString().Length, '0')}, shard {shard.id.ToString().PadLeft((shardCount - 1).ToString().Length, '0')} online!");
    });

    if (Config.IsDev)
    {
        shard.client.Debug += (client, message) =>
        {
            Log.Debug($"[{message.type}] {message.message}");
        };
    }

    shard.client.Error += (client, error) =>
    {
        if (Config.IsDev)
        {
            Log.Error(error, $"An error occured on shard {shard.id} of cluster {Config.cluster}");
        }

        _ = DiscordLogger.Log($"[{{dt}}] {{emoji.clockedout}} Shard `{shard.id}` of cluster `{Config.cluster}` encountered an error.\n```\n{error}\n```");
    };

    shard.client.Connecting += (client, args) =>
    {
        _ = DiscordLogger.Log($"[{{dt}}] {{emoji.break}} Shard `{shard.id}` of cluster `{Config.cluster}` is starting...".Process());
        Log.Information($"Cluster {Config.cluster.ToString().PadLeft((Config.replicas.Count - 1).ToString().Length, '0')}, shard {shard.id.ToString().PadLeft((shardCount - 1).ToString().Length, '0')} starting...");
    };
}

bool start = Config.cluster == 0;
bool isLastCluster = false;
if (!start)
{
    if (Config.IsDev) Log.Debug("[Debug] Waiting for start signal...");
    pubSub.Subscribe($"start", (channel, message) =>
    {
        string[] parts = message.ToString().Split(':');
        string replica = parts[0];
        if (replica == Config.replicaId)
        {
            start = true;
            isLastCluster = parts[1] == "1";
        }
    });
    
}

double lastRequestedStart = 0;
while (!start) 
{ 
    Thread.Sleep(100);
    if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastRequestedStart > 5000)
    {
        lastRequestedStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        pubSub.Publish($"{Config.deploymentId}-can-start", Config.replicaId);
    }
}

Log.Information($"Cluster {Config.cluster} starting shards {clusterStart} to {clusterEnd - 1}.");
_ = DiscordLogger.Log($"[{{dt}}] {{emoji.break}} Cluster `{Config.cluster}` is starting...".Process());

_ = Task.Run(() =>
{
    string? nextReplica = Config.replicas.ElementAtOrDefault(Config.cluster + 1);
    while (!sharding.shards.All(s => s.client.ready)) Thread.Sleep(100);
    _ = DiscordLogger.Log($"[{{dt}}] {{emoji.clockedin}} Cluster `{Config.cluster}` is ready.".Process());

    Thread.Sleep(5000);
    if (nextReplica is not null && !isLastCluster)
    {
        pubSub.Publish("start", $"{nextReplica}:0");
        Log.Information($"All cluster {Config.cluster} shards started, sending command to start cluster {Config.cluster + 1}.");
    }
    else
    {
        Log.Information($"All cluster {Config.cluster} shards started, all cluters started.");
        pubSub.Subscribe($"{Config.deploymentId}-can-start", (channel, message) =>
        {
            pubSub.Publish("start", $"{message}:1");
            pubSub.Unsubscribe($"{Config.deploymentId}-can-start");
            Log.Information($"Cluster {Config.replicas.IndexOf(message.ToString())} taking last cluster from {Config.cluster}.");
        });
    }
});

sharding.Start();
sharding.Hold();
Log.Information("Stopping bot...");
Logger.Shutdown();