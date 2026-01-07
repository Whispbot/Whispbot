using Microsoft.AspNetCore.Components.Web;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace Whispbot.Tools
{
    public static class ERLC
    {
        private static HttpClient _client = new();
        private static bool _initialized = false;
        public static string? apiUrl = Config.IsDev ? "https://prc.whisp.bot" : Environment.GetEnvironmentVariable("PRC_API_URL");
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
            var (_, method, _, _) = endpoints[endpoint];
            if (method != HttpMethod.Get) return null;

            using var _ = Tracer.Start($"ERLC.CheckCache: {endpoint}");

            string cacheKey = $"prcapiworker:{endpoint}:{(apiKey is not null ? HashString(apiKey) : "unauthenticated")}";

            var redis = Redis.GetDatabase();

            string? cacheValue = redis?.StringGet(cacheKey);
            if (cacheValue is null) return null;

            string? timestamp = redis!.StringGet($"{cacheKey}:timestamp");
            long? cachedAtMs = timestamp is not null ? long.Parse(timestamp) : null;

            return new PRC_Response { code = ErrorCode.Cached, message = "Item Cached", data = cacheValue, cachedAt = cachedAtMs };
        }

        public static async Task<PRC_Response?> Request(Endpoint endpoint, string? apiKey = null, StringContent? content = null)
        {
            using var _ = Tracer.Start($"ERKC.FetchData: {endpoint}");
            if (!_initialized) Init();

            var (url, method, _, requiresKey) = endpoints[endpoint];

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

            try
            {
                HttpResponseMessage response = await _client.SendAsync(request);

                PRC_Response? data = JsonConvert.DeserializeObject<PRC_Response>(await response.Content.ReadAsStringAsync());

                return data;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                Log.Error(ex, $"An error occured while making a request to the PRC API.\nEndpoint: {endpoint}\nAPI Key: {(apiKey is not null ? HashApiKey(apiKey) : "N/A")}");
                return null;
            }
        }

        public static string HashString(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);

            StringBuilder sb = new();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
        
        private static string EncryptionKey
        {
            get
            {
                string? key = Environment.GetEnvironmentVariable("PRC_ENCRYPTION_KEY");
                if (string.IsNullOrEmpty(key))
                {
                    throw new InvalidOperationException("PRC_ENCRYPTION_KEY environment variable is not set.");
                }
                return key;
            }
        }

        public static string EncryptApiKey(string apiKey)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(EncryptionKey);
            aes.GenerateIV();
            var iv = aes.IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            using var ms = new MemoryStream();
            ms.Write(iv, 0, iv.Length);
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(apiKey);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        public static string DecryptApiKey(string encryptedApiKey)
        {
            var fullCipher = Convert.FromBase64String(encryptedApiKey);

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(EncryptionKey);
            var iv = new byte[aes.BlockSize / 8];
            Array.Copy(fullCipher, iv, iv.Length);

            using var decryptor = aes.CreateDecryptor(aes.Key, iv);
            using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        public static string HashApiKey(string apiKey)
        {
            string salt = Environment.GetEnvironmentVariable("PRC_HASH_SALT") ?? "DefaultSalt_ChangeInProduction";
            return HashString(salt + apiKey);
        }

        public static bool ResponseHasError(PRC_Response response, out MessageBuilder? errorMessage)
        {
            if (response.code == ErrorCode.Success || response.code == ErrorCode.Cached)
            {
                errorMessage = null;
                return false;
            }
            else
            {
                errorMessage = new MessageBuilder
                {
                    components = [
                        new ContainerBuilder
                        {
                            components = [
                                new TextDisplayBuilder($"## {{string.title.erlcapierror}}\n> {{string.errors.erlcapi.{response.code.ToString()?.ToLower() ?? "generic"}}}."),
                                new SeperatorBuilder(),
                                new TextDisplayBuilder($"{{string.content.erlcapierror}}.\n```\n[{response.code?.ToInt() ?? -1}] {response.message}\n```")
                            ],
                            accent = new Color(150, 0, 0)
                        }
                    ],
                    flags = MessageFlags.IsComponentsV2
                };
                return true;
            }
        }

        public static async Task<PRC_Response?> GetServerInfo(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerInfo, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> GetPlayers(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerPlayers, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> GetJoins(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerJoinlogs, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> GetQueue(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerQueue, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> GetKills(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerKilllogs, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> GetCommands(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerCommandlogs, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> GetModcalls(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerModcalls, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> GetBans(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerBans, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> GetVehicles(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerVehicles, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> GetStaff(ERLCServerConfig server)
        {
            return await Request(Endpoint.ServerStaff, server.DecryptedApiKey);
        }

        public static async Task<PRC_Response?> SendCommand(ERLCServerConfig server, string command)
        {
            return await Request(Endpoint.ServerCommand, server.DecryptedApiKey, new StringContent(JsonConvert.SerializeObject(new { command }), Encoding.UTF8, "application/json"));
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

        public static ERLCServerConfig? GetServerFromString(IEnumerable<ERLCServerConfig> servers, string str)
        {
            var server = servers.FirstOrDefault(s => s.name?.Contains(str, StringComparison.CurrentCultureIgnoreCase) ?? false);
            server ??= servers.FirstOrDefault(s => s.code?.Contains(str, StringComparison.CurrentCultureIgnoreCase) ?? false);

            return server;
        }

        public static async Task<ERLCServerConfig?> TryGetServer(CommandContext ctx)
        {
            if (ctx.GuildId is null) return null;

            List<ERLCServerConfig>? servers = await WhispCache.ERLCServerConfigs.Get(ctx.GuildId);

            if (servers is null || servers.Count == 0)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}.");
                return null;
            }

            string searchString = ctx.args.Count > 0 ? ctx.args.Join(" ") : "";

            if (String.IsNullOrWhiteSpace(searchString))
            {
                ERLCServerConfig? defaultServer = servers.Count == 1 ? servers[0] : servers.FirstOrDefault(s => s.is_default);
                if (defaultServer is not null)
                {
                    return defaultServer;
                }
                else
                {
                    await ctx.Reply("{emoji.cross} {string.errors.erlcserver.nodefault}");
                    return null;
                }
            }

            ERLCServerConfig? server = GetServerFromString(servers, searchString);

            if (server is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}.");
                return null;
            }

            if (server.api_key is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.nokey}");
                return null;
            }

            return server;
        }

        public static async Task<PRC_DeserializedResponse<T>?> GetEndpointData<T>(CommandContext ctx, ERLCServerConfig server, Endpoint endpoint) where T : class
        {
            using var _ = Tracer.Start($"ERLC.GetEndpoint: {endpoint}");

            var response = CheckCache(endpoint, server.DecryptedApiKey);

            if (response is null)
            {
                await ctx.Reply("{emoji.loading} {string.content.erlc.fetching}...");
                response = await Request(endpoint, server.DecryptedApiKey);

                if (response is null)
                {
                    await ctx.EditResponse("{emoji.cross} {string.errors.erlcserver.apierror}");
                    return null;
                }
            }

            if (ResponseHasError(response, out var errorMessage))
            {
                await ctx.EditResponse(errorMessage!);
                return null;
            }

            try
            {
                T? data = JsonConvert.DeserializeObject<T>(response.data?.ToString() ?? "[]");
                return new PRC_DeserializedResponse<T>
                {
                    code = response.code,
                    message = response.message,
                    data = data,
                    server = server,
                    cachedAt = response.cachedAt
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Parsing ERLC data failed.");
                return null;
            }
        }

        public static async Task<string> GenerateFooter<T>(PRC_DeserializedResponse<T> response)
        {
            List<ERLCServerConfig>? servers = response.server?.guild_id is not null ? await WhispCache.ERLCServerConfigs.Get(response.server.guild_id.ToString()) : null;
            string serverName = "";
            if ((servers?.Count ?? 0) > 0)
            {
                serverName = $" | {{string.content.erlcserver.server}}: {response.server!.code ?? "..."}";
            }

            return $"{{string.content.erlcserver.updated}}: {(response.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt) / 1000)}s ago" : "{string.content.erlcserver.justnow}")}{serverName}";
        }

        public static class ERLC_Commands
        {
            public static readonly Dictionary<string, (int, string)> modCommands = new() {
                { "hint",           new (1, "[message]") },
                { "h",              new (1, "[message]") },
                { "m",              new (1, "[message]") },
                { "message",        new (1, "[message]") },
                { "pm",             new (2, "[user] [message]") },
                { "privatemessage", new (2, "[user] [message]") },
                { "kick",           new (1, "[user] (reason)") },
                { "kill",           new (1, "[user]") },
                { "down",           new (1, "[user]") },
                { "refresh",        new (1, "[user]") },
                { "heal",           new (1, "[user]") },
                { "startfire",      new (0, "(location)") },
                { "unwanted",       new (1, "[user]") },
                { "unjail",         new (1, "[user]") },
                { "free",           new (1, "[user]") },
                { "jail",           new (1, "[user]") },
                { "arrest",         new (1, "[user]") },
                { "prty",           new (1, "[length]") },
                { "priority",       new (1, "[length]") },
                { "wanted",         new (1, "[user]") },
                { "time",           new (1, "[time (0-24)]") },
                { "stopfire",       new (0, "") },
                { "respawn",        new (1, "[user]") },
                { "load",           new (1, "[user]") },
                { "pt",             new (1, "[length]") },
                { "peacetime",      new (1, "[length]") },
            };
            public static readonly Dictionary<string, (int, string)> adminCommands = new() {
                { "weather",         new (1, "[weather]") },
                { "mod",             new (1, "[user/id]") },
                { "unmod",           new (1, "[user/id]") },
                { "ban",             new (1, "[user/id]") },
                { "unban",           new (1, "[user/id]") },
                { "loadlayout",      new (1, "[layout]") },
                { "unloadlayout",    new (1, "[layout]") },
                { "shutdown",        new (0, "") }
            };
            public static readonly Dictionary<string, (int, string)> ownerCommands = new() {
                { "admin",             new (1, "[user/id]") },
                { "unadmin",           new (1, "[user/id]") },
            };
        }

        public class PRC_Response
        {
            public ErrorCode? code = ErrorCode.Unknown;
            public string? message = null;
            public object? data = null;
            public long? cachedAt = null;
            public ERLCServerConfig? server = null;
        }

        public class PRC_DeserializedResponse<T>: PRC_Response
        {
            public new T? data = default;
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
            public string? Moderator = "Roblox:1";

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
