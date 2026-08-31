using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using Whispbot;
using Whispbot.API;
using Whispbot.Commands;
using Whispbot.Commands.ERLC.Commands;
using Whispbot.Databases;
using Whispbot.Interactions;
using Whispbot.Tools;
using Whispbot.Tools.Bot;
using Whispbot.Tools.Disc;
using Whispbot.Tools.Logger;

Logger.Initialize();
Shutdown.Init();

bool dev = Config.isDev;

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
_ = Task.Run(Emojis.GetEmojis);
Tracer.CreateListener();

// Thread for API (communication between services / health check)
Thread APIThread = new(new ThreadStart(() =>
{
    if (Config.isDev)
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

string? shardsEnv = Config.isDev ? null : Environment.GetEnvironmentVariable("SHARDS");
int? shards = shardsEnv is null ? null : int.Parse(shardsEnv);

var config = new DiscordSocketConfig
{
    AuditLogCacheSize = 5,
    MessageCacheSize = 10,

    // Always cache users - some ERLC features don't work without this
    // Unfortunately Discord.NET doesn't provide a way to download users by ID
    // so this is the only reasonable way to ensure that the users we need are
    // easily accessible. The alt is fetching 50 users 1 by 1 using the API :(
    AlwaysDownloadUsers = true,

    LogLevel = Config.isDev ? LogSeverity.Debug : LogSeverity.Info,

    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,

    TotalShards = shards
};

var client = new DiscordShardedClient(config);
Config.client = client;

client.ShardReady += Logging.ReadyAsync;
client.Log += Logging.LogAsync;

CommandManager.Init(client);
ERLCCommandManager.Init(client);
InteractionManager.Init(client);

Preloading.Init(client);
DiscordPublisher.Start(client);
DiscordModeration.RegisterClient(client);

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

await Shutdown.WaitAsync();
