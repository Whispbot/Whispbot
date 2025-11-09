using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;

namespace Whispbot.Tools
{
    public static class ERLC
    {
        private static HttpClient _client = new();
        private static bool _initialized = false;
        public static string? apiUrl = Config.IsDev ? "http://localhost:5001" : Environment.GetEnvironmentVariable("PRC_API_URL");
        public static string? authKey = Environment.GetEnvironmentVariable("PRC_API_KEY");

        public static void Init()
        {
            if (apiUrl is null) throw new Exception("PRC_API_URL environment variable not set");

            _client.BaseAddress = new Uri(apiUrl ?? "http://localhost:5001");
            if (authKey is not null) _client.DefaultRequestHeaders.Add("Authorization", authKey);

            _initialized = true;
        }

        public static PRC_Response? CheckCache(Endpoint endpoint, string? apiKey)
        {
            if (Config.IsDev) return null;
                
            var (_, method, _, _) = endpoints[endpoint];
            if (method != HttpMethod.Get) return null;

            string cacheKey = $"prcapiworker:{endpoint}:{apiKey ?? "unauthenticated"}";

            var redis = Redis.GetDatabase();

            string? cacheValue = redis?.StringGet(cacheKey);
            if (cacheValue is null) return null;

            string? timestamp = redis!.StringGet($"{cacheKey}:timestamp");
            long? cachedAtMs = timestamp is not null ? long.Parse(timestamp) : null;

            return new PRC_Response { code = ErrorCode.Cached, message = "Item Cached", data = JsonConvert.SerializeObject(cacheValue), cachedAt = cachedAtMs };
        }

        public static async Task<PRC_Response?> Request(Endpoint endpoint, string? apiKey = null, StringContent? content = null)
        {
            if (!_initialized) Init();

            var cachedValue = CheckCache(endpoint, apiKey);
            if (cachedValue is not null) return cachedValue;

            var (url, method, type, requiresKey) = endpoints[endpoint];

            HttpRequestMessage request = new(method, url)
            {
                Content = content
            };

            if (requiresKey)
            {
                if (apiKey is null)
                {
                    Log.Error("Attempted to make request to endpoint requiring API key without API key.");
                    return null;
                }
                else
                {
                    request.Headers.Add("Server-Key", apiKey);
                }
            }                

            HttpResponseMessage response = await _client.SendAsync(request);

            PRC_Response? data = JsonConvert.DeserializeObject<PRC_Response>(await response.Content.ReadAsStringAsync());

            return data;
        }

        public static async Task<PRC_Response?> GetServerInfo(string apiKey)
        {
            var response = await Request(Endpoint.ServerInfo, apiKey);
            if (response is not null) _ = Task.Run(() => PostGetServerInfo(response, apiKey));
            return response;
        }

        public static async Task<PRC_Response?> GetPlayers(string apiKey)
        {
            var response = await Request(Endpoint.ServerPlayers, apiKey);
            if (response is not null) _ = Task.Run(() => PostGetPlayers(response, apiKey));
            return response;
        }

        public static async Task<PRC_Response?> GetJoins(string apiKey)
        {
            return await Request(Endpoint.ServerJoinlogs, apiKey);
        }

        public static async Task<PRC_Response?> GetQueue(string apiKey)
        {
            return await Request(Endpoint.ServerQueue, apiKey);
        }

        public static async Task<PRC_Response?> GetKills(string apiKey)
        {
            return await Request(Endpoint.ServerKilllogs, apiKey);
        }

        public static async Task<PRC_Response?> GetCommands(string apiKey)
        {
            return await Request(Endpoint.ServerCommandlogs, apiKey);
        }

        public static async Task<PRC_Response?> GetModcalls(string apiKey)
        {
            return await Request(Endpoint.ServerModcalls, apiKey);
        }

        public static async Task<PRC_Response?> GetBans(string apiKey)
        {
            return await Request(Endpoint.ServerBans, apiKey);
        }

        public static async Task<PRC_Response?> GetVehicles(string apiKey)
        {
            return await Request(Endpoint.ServerVehicles, apiKey);
        }

        public static async Task<PRC_Response?> GetStaff(string apiKey)
        {
            return await Request(Endpoint.ServerStaff, apiKey);
        }

        public static async Task<PRC_Response?> SendCommand(string apiKey, string command)
        {
            return await Request(Endpoint.ServerCommand, apiKey, new StringContent(JsonConvert.SerializeObject(new { command })));
        }

        public static async Task<PRC_Response?> ResetGlobalKey()
        {
            return await Request(Endpoint.ResetAPIKey);
        }

        private static ERLCServerConfig? GetServerFromAPIKey(string apiKey)
        {
            var servers = WhispCache.ERLCServerConfigs.Find((s, _) => s.Any(e => e.api_key == apiKey));
            var server = servers?.FirstOrDefault(e => e.api_key == apiKey);

            return server;
        }

        private static void UpdateServerFromAPIKey(string apiKey, ERLCServerConfig? config)
        {
            if (config is null) return;

            var servers = WhispCache.ERLCServerConfigs.Find((s, _) => s.Any(e => e.api_key == apiKey));
            if (servers is null) return;
            var server = servers.Find(s => s.api_key == apiKey);
            if (server is null) return;
            servers.RemoveAt(servers.IndexOf(server));
            servers.Add(config);
        }
            
        private static void PostGetServerInfo(PRC_Response response, string apiKey)
        {
            PRC_Server? serverInfo = JsonConvert.DeserializeObject<PRC_Server>(response.data?.ToString() ?? "{}");
            if (serverInfo is null) return;

            ERLCServerConfig? server = GetServerFromAPIKey(apiKey);

            if (server is null) return;
            if (server.ingame_players == serverInfo.currentPlayers && server.name == serverInfo.name) return;

            ERLCServerConfig? updatedServer = Postgres.SelectFirst<ERLCServerConfig>(
                @"UPDATE erlc_servers SET name = @2, ingame_players = @3 WHERE api_key = @1 RETURNING *;",
                [apiKey, serverInfo.name, serverInfo.currentPlayers]
            );
            UpdateServerFromAPIKey(apiKey, updatedServer);
        }

        private static void PostGetPlayers(PRC_Response response, string apiKey)
        {
            List<PRC_Player>? players = JsonConvert.DeserializeObject<List<PRC_Player>>(response.data?.ToString() ?? "[]");
            if (players is null) return;

            ERLCServerConfig? server = GetServerFromAPIKey(apiKey);

            if (server is null) return;
            if (server.ingame_players == players.Count) return;

            ERLCServerConfig? updatedServer = Postgres.SelectFirst<ERLCServerConfig>(
                @"UPDATE erlc_servers SET ingame_players = @2 WHERE api_key = @1 RETURNING *;",
                [apiKey, players.Count]
            );
            UpdateServerFromAPIKey(apiKey, updatedServer);
        }



        public static ERLCServerConfig? GetServerFromString(IEnumerable<ERLCServerConfig> servers, string str)
        {
            var server = servers.FirstOrDefault(s => s.name?.Contains(str, StringComparison.CurrentCultureIgnoreCase) ?? false);
            server ??= servers.FirstOrDefault(s => s.name is null);

            return server;
        }






        public class PRC_Response
        {
            public ErrorCode? code = ErrorCode.Unknown;
            public string? message = null;
            public object? data = null;
            public long? cachedAt = null;
        }

        //    Endpoint                     Path                      Method          Return Type                         Requires Server Key
        public static readonly Dictionary<Endpoint, (string, HttpMethod, Type?, bool)> endpoints = new() {
        { Endpoint.ServerCommand,     ("/v1/server/command",     HttpMethod.Post, null,                               true ) },
        { Endpoint.ServerInfo,        ("/v1/server",             HttpMethod.Get,  typeof(PRC_Server),                 true ) },
        { Endpoint.ServerPlayers,     ("/v1/server/players",     HttpMethod.Get,  typeof(List<PRC_Player>),           true ) },
        { Endpoint.ServerJoinlogs,    ("/v1/server/joinlogs",    HttpMethod.Get,  typeof(List<PRC_JoinLog>),          true ) },
        { Endpoint.ServerQueue,       ("/v1/server/queue",       HttpMethod.Get,  typeof(List<double>),               true ) },
        { Endpoint.ServerKilllogs,    ("/v1/server/killlogs",    HttpMethod.Get,  typeof(List<PRC_KillLog>),          true ) },
        { Endpoint.ServerCommandlogs, ("/v1/server/commandlogs", HttpMethod.Get,  typeof(List<PRC_CommandLog>),       true ) },
        { Endpoint.ServerModcalls,    ("/v1/server/modcalls",    HttpMethod.Get,  typeof(List<PRC_CallLog>),          true ) },
        { Endpoint.ServerBans,        ("/v1/server/bans",        HttpMethod.Get,  typeof(Dictionary<string, string>), true ) },
        { Endpoint.ServerVehicles,    ("/v1/server/vehicles",    HttpMethod.Get,  typeof(List<PRC_Vehicle>),          true ) },
        { Endpoint.ServerStaff,       ("/v1/server/staff",       HttpMethod.Get,  typeof(PRC_Staff),                  true ) },
        { Endpoint.ResetAPIKey,       ("/v1/api-key/reset",      HttpMethod.Get,  null,                               false) },
    };

        public enum Endpoint
        {
            ServerCommand,
            ServerInfo,
            ServerPlayers,
            ServerJoinlogs,
            ServerQueue,
            ServerKilllogs,
            ServerCommandlogs,
            ServerModcalls,
            ServerBans,
            ServerVehicles,
            ServerStaff,
            ResetAPIKey
        }

        public enum ErrorCode
        {
            Unknown = 0,

            Success = 200,
            Cached = 304,
            BadRequest = 400,
            Unauthorized = 401,
            TimedOut = 408,
            ServerError = 500,

            RobloxError = 1001,
            InternalError = 1002,
            KeyNotProvided = 2000,
            IncorrectKey = 2001,
            InvalidKey = 2002,
            InvalidGlobalKey = 2003,
            KeyBanned = 2004,
            InvalidCommand = 3001,
            ServerOffline = 3002,
            RateLimited = 4001,
            CommandRestricted = 4002,
            MessageProhibited = 4003,
            RescourseRestricted = 9998,
            OutOfDate = 9999
        }




        public class PRC_Server
        {
            /// <summary>
            /// The name of the ER:LC server.
            /// </summary>
            public string name = "Server Name";

            /// <summary>
            /// The Roblox user ID of the server owner.
            /// </summary>
            public double ownerId = 1;

            /// <summary>
            /// The Roblox user ID's of the server co-owners.
            /// </summary>
            public List<double> coOwnerIds = [];

            /// <summary>
            /// The number of players currently in the server.
            /// </summary>
            public byte currentPlayers = 0;

            /// <summary>
            /// The maximum number of players allowed in the server.
            /// </summary>
            public byte maxPlayers = 40;

            /// <summary>
            /// The key used to join the server.
            /// </summary>
            public string joinKey = "";

            /// <summary>
            /// The account verification requirement for joining the server.
            /// </summary>
            public string accVerifiedReq = "Disabled";

            /// <summary>
            /// Whether team balance is enabled for the server.
            /// </summary>
            public bool teamBalance = true;
        }

        public class PRC_Player
        {
            /// <summary>
            /// The player's name in the format "{Username}:{UserId}".
            /// </summary>
            public string player = "Roblox:1";

            /// <summary>
            /// The user's permission level in the server.
            /// Possible: Server Owner / Server Co-Owner / Server Administrator / Server Moderator / Normal
            /// </summary>
            public string permission = "Normal";

            /// <summary>
            /// The player's callsign, if applicable.
            /// </summary>
            public string? callsign = null;

            /// <summary>
            /// The team the player is currently on.
            /// </summary>
            public string team = "Civilian";
        }

        public class PRC_JoinLog
        {
            /// <summary>
            /// Whether this is a join, if false, is leave.
            /// </summary>
            public bool Join = true;

            /// <summary>
            /// The timestamp of the join/leave in seconds since the Unix epoch.
            /// </summary>
            public double Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            /// <summary>
            /// The player's name in the format "{Username}:{UserId}".
            /// </summary>
            public string Player = "Roblox:1";
        }

        public class PRC_KillLog
        {
            /// <summary>
            /// The person who was killed in the format "{Username}:{UserId}".
            /// </summary>
            public string Killed = "Roblox:1";

            /// <summary>
            /// The person who killed in the format "{Username}:{UserId}".
            /// </summary>
            public string Killer = "Roblox:1";

            /// <summary>
            /// The timestamp of the kill in seconds since the Unix epoch.
            /// </summary>
            public double Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public class PRC_CommandLog
        {
            /// <summary>
            /// The player's name in the format "{Username}:{UserId}".
            /// </summary>
            public string Player = "Roblox:1";

            /// <summary>
            /// The timestamp of the command in seconds since the Unix epoch.
            /// </summary>
            public double Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            /// <summary>
            /// The command that was executed.
            /// </summary>
            public string Command = ":h Error Loading Data :(";
        }

        public class PRC_CallLog
        {
            /// <summary>
            /// The caller's name in the format "{Username}:{UserId}".
            /// </summary>
            public string Caller = "Roblox:1";

            /// <summary>
            /// The moderator's name in the format "{Username}:{UserId}".
            /// </summary>
            public string Moderator = "Roblox:1";

            /// <summary>
            /// The timestamp of the call in seconds since the Unix epoch.
            /// </summary>
            public double Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public class PRC_Vehicle
        {
            /// <summary>
            /// The texture on the vehicle, if applicable.
            /// </summary>
            public string? Texture = null;

            /// <summary>
            /// The name of the vehicle in the format "{Year} {Name}".
            /// </summary>
            public string Name = "2035 Vroom Vroom Car";

            /// <summary>
            /// The Roblox username of the vehicle's owner.
            /// </summary>
            public string Owner = "Roblox";
        }

        public class PRC_Staff
        {
            /// <summary>
            /// The Roblox user IDs of the server co-owners.
            /// </summary>
            public List<double> CoOwners = [];

            /// <summary>
            /// A dictionary of server administrators with the key as their Roblox user ID and the value as their username.
            /// </summary>
            public Dictionary<string, string> Admins = [];

            /// <summary>
            /// A dictionary of server moderators with the key as their Roblox user ID and the value as their username.
            /// </summary>
            public Dictionary<string, string> Mods = [];
        }
    }
}
