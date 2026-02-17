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
using Whispbot.Commands.ERLCCommands.Commands;
using Whispbot.Tools.Infra;

Logger.Initialize();

bool dev = Config.IsDev;

Log.Information(@$"
 _    _ _     _           _           _   
| |  | | |   (_)         | | V{Config.versionText,-7}| |  
| |  | | |__  _ ___ _ __ | |__   ___ | |_ 
| |/\| | '_ \| / __| '_ \| '_ \ / _ \| __|
\  /\  / | | | \__ \ |_) | |_) | (_) | |_ 
 \/  \/|_| |_|_|___/ .__/|_.__/ \___/ \__|{(dev ? ".dev" : "")}
                   | |                    
                   |_|                    
");

// Since env vars are copied from railwaay deployment, use different env vars for dev and prod
string? token = dev ? Environment.GetEnvironmentVariable("DEV_TOKEN") : Environment.GetEnvironmentVariable("CLIENT_TOKEN");

if (token is null)
{
    Log.Fatal("Please set the CLIENT_TOKEN environment variable.");
    Logger.Shutdown();
    return;
}

// -- Init Databases and Services --
_ = Task.Run(Redis.Init);
_ = Task.Run(Postgres.Init);
_ = Task.Run(SentryConnection.Init);
_ = Task.Run(Strings.GetLanguages);
// Tracer.CreateListener();

// Thread for API (communication between services / health check)
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

// Thread for cache updater (pg listener)
Thread CacheThread = new(new ThreadStart(async () =>
{
    await UpdateHandler.ListenForUpdates();
}))
{
    Name = "Whisp Cache",
    IsBackground = true
};
CacheThread.Start();

int clusters = 1;
if (!dev)
{
    // Fetch the number of replicas from railway instead of hardcoding value
    clusters = await Railway.getReplicaCount();
}

Log.Information($"Starting cluster. Total clusters: {clusters}.");


ISubscriber? pubSub = null;

while (pubSub is null && clusters > 1) // Redis pubsub vital for clustering; must wait for start
{
    pubSub = Redis.GetSubscriber();
    if (pubSub is null) {
        if (Config.IsDev) Log.Debug("[Debug] Waiting for redis to connect...");
        Thread.Sleep(500);
    }
}

if (clusters > 1) // We dont need to cluster if there is only 1 of them
{
    Config.replica = new(
        Config.deploymentId,
        Config.replicaId ?? Guid.NewGuid().ToString(),
        clusters
    );

    await Config.replica.Start(); // Init replica manager

    // Waits until replica has recieved a heartbeat and is active
    while (!Config.replica.active) Thread.Sleep(100);

    // Registers and assigns clusters
    void register(RedisValue message)
    {
        string[] parts = message.ToString().Split(':');
        string deploymentId = parts[0];
        string replicaId = parts[1];

        if (deploymentId != Config.deploymentId) return;

        if (!Config.replicas.Contains(replicaId)) Config.replicas.Add(replicaId);

        pubSub.Publish($"{Config.deploymentId}-replicas", JsonConvert.SerializeObject(Config.replicas));
    }

    // Leader handles cluster assignment
    if (Config.replica.IsLeader)
    {
        Config.cluster = 0;
        Config.replicas.Add(Config.replicaId);

        pubSub!.Subscribe("register", (channel, message) => { register(message); });

        _ = DiscordLogger.Log($"[{{dt}}] {{emoji.loading}} Re-clustering {clusters} clusters...".Process());
    }
    else
    {
        pubSub!.Subscribe($"{Config.deploymentId}-replicas", (channel, message) =>
        {
            Config.replicas = JsonConvert.DeserializeObject<List<string>>(message.ToString()) ?? [];
            Config.cluster = Config.replicas.IndexOf(Config.replicaId);
        });
    }

    Config.replica.OnElected += (_, _) => // Subscribe to registration while elected
    {
        pubSub.Subscribe("register", (channel, message) => { register(message); });
    };

    Config.replica.OnLostLeadership += (_, _) => // ^^^
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
    if (Config.replicaId is not null)
    {
        Config.replicas.Add(Config.replicaId);
    }
    else
    {
        Config.replicas.Add("dev");
    }

    Log.Warning("Only 1 cluster, skipping cluster init");
}

string? shardsEnv = Config.IsDev ? null : Environment.GetEnvironmentVariable("SHARDS");
int? shards = 3;// shardsEnv is null ? null : int.Parse(shardsEnv);

ShardingManager sharding = new(
    token,
    Intents.GuildMessages | 
    Intents.MessageContent | 
    Intents.Guilds | 
    Intents.GuildMembers | 
    Intents.GuildModeration | 
    Intents.GuildIntegrations |
    Intents.AutoModerationExecution,
    shards
);

// Calculate which shards this cluster will handle
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

ERLCCommandManager erlcCommands = new();
Config.erlcCommands = erlcCommands;

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
            Log.Verbose($"[{message.type}] {message.message}");
        };
    }

    shard.client.Error += (client, error) =>
    {
        Log.Error(error, $"An error occured on shard {shard.id} of cluster {Config.cluster}");

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
if (!start) // Wait for start signal if not cluster 0
{
    Log.Debug("Waiting for start signal...");
    pubSub!.Subscribe($"start", (channel, message) =>
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
while (!start) // Wait for start signal, but periodically request if can start in-case of missed message or restart
{ 
    Thread.Sleep(1000);
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
    while (!sharding.shards.All(s => s.client.ready)) Thread.Sleep(100); // Wait for all shards to be ready
    _ = DiscordLogger.Log($"[{{dt}}] {{emoji.clockedin}} Cluster `{Config.cluster}` is ready.".Process());

    Thread.Sleep(5000); // Wait 5 seconds (Discord's wait between identifies) before starting next cluster
    if (nextReplica is not null && !isLastCluster)
    {
        // Signal next cluster to start
        pubSub.Publish("start", $"{nextReplica}:0");
        Log.Information($"All cluster {Config.cluster} shards started, sending command to start cluster {Config.cluster + 1}.");
    }
    else
    {
        // Last cluster, listen for clusters that have restarted and are requesting start
        Log.Information($"All cluster {Config.cluster} shards started, all cluters started.");
        pubSub.Subscribe($"{Config.deploymentId}-can-start", (channel, message) =>
        {
            pubSub.Publish("start", $"{message}:1");
            pubSub.Unsubscribe($"{Config.deploymentId}-can-start");
            Log.Information($"Cluster {Config.replicas.IndexOf(message.ToString())} taking last cluster from {Config.cluster}.");
        });
    }
});

_ = Task.Run(() => sharding.Start());

await Sigterm.WaitForSigterm();

foreach (Shard shard in sharding.shards)
{
    Log.Information($"Stopping shard {shard.id} of cluster {Config.cluster}.");
    shard.client.Disconnect();

    Thread.Sleep(200);
}

Log.Information("Goodbye");
Logger.Shutdown();
Environment.Exit(0);