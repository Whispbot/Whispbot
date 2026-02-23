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
using Whispbot.Commands.ERLCCommands;
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

        public static PRC_APIResponse? CheckCache(string? apiKey)
        {                
            using var _ = Tracer.Start($"ERLC.CheckCacheV2");

            string cacheKey = $"prcapiworker:ServerInfoV2:{(apiKey is not null ? HashString(apiKey) : "unauthenticated")}";

            var redis = Redis.GetDatabase();

            string? cacheValue = redis?.StringGet(cacheKey);
            if (cacheValue is null) return null;

            string? timestamp = redis!.StringGet($"{cacheKey}:timestamp");
            long? cachedAtMs = timestamp is not null ? long.Parse(timestamp) : null;

            var data = JsonConvert.DeserializeObject<PRC_Server>(cacheValue);
            if (data is null) return null;

            return new PRC_APIResponse
            {
                Data = data,
                CachedAt = cachedAtMs,
                Code = ErrorCode.Cached,
                Message = "Data cached"
            };
        }

        public static async Task<PRC_APIResponse?> GetServerV2(string apiKey)
        {
            using var _ = Tracer.Start($"ERLC.FetchDataV2");
            if (!_initialized) Init();

            HttpRequestMessage request = new(HttpMethod.Get, "/v2/server?Players=true&Staff=true&JoinLogs=true&Queue=true&KillLogs=true&CommandLogs=true&ModCalls=true&Vehicles=true");

            request.Headers.Add("Server-Key", apiKey);

            try
            {
                HttpResponseMessage response = await _client.SendAsync(request);

                PRC_APIResponse? data = JsonConvert.DeserializeObject<PRC_APIResponse>(await response.Content.ReadAsStringAsync());

                return data;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                Log.Error(ex, $"An error occured while making a request to the PRC API.\nAPI Key: {(apiKey is not null ? HashApiKey(apiKey) : "N/A")}");
                return null;
            }
        }

        public static async Task<PRC_APIResponse?> SendCommand(ERLCServerConfig server, string command)
        {
            using var _ = Tracer.Start("ERLC.SendCommand");
            if (!_initialized) Init();

            HttpRequestMessage request = new(HttpMethod.Post, "/v1/server/command")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { command }), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Server-Key", server.DecryptedApiKey);

            try
            {
                var response = await _client.SendAsync(request);

                PRC_APIResponse? data = JsonConvert.DeserializeObject<PRC_APIResponse>(await response.Content.ReadAsStringAsync());

                return data;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                Log.Error(ex, $"An error occured while making a request to the PRC API.\nAPI Key: {(server.api_key)}");
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

        public static bool ResponseHasError(PRC_APIResponse response, out MessageBuilder? errorMessage)
        {
            if (response.Data is not null)
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
                                new TextDisplayBuilder($"## {{string.title.erlcapierror}}\n> {{string.errors.erlcapi.{response.Code.ToString()?.ToLower() ?? "generic"}}}."),
                                new SeperatorBuilder(),
                                new TextDisplayBuilder($"{{string.content.erlcapierror}}.\n```\n[{response.Code.ToInt()}] {response.Message}\n```")
                            ],
                            accent = new Color(150, 0, 0)
                        }
                    ],
                    flags = MessageFlags.IsComponentsV2
                };
                return true;
            }
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

        public static async Task<PRC_APIResponse?> GetServerDataV2(CommandContext ctx, ERLCServerConfig server)
        {
            using var _ = Tracer.Start($"ERLC.GetServerV2");

            var response = CheckCache(server.DecryptedApiKey);

            if (response is null)
            {
                await ctx.Reply("{emoji.loading} {string.content.erlc.fetching}...");
                response = await GetServerV2(server.DecryptedApiKey);

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

            return response;
        }

        public static string GenerateFooter(PRC_APIResponse response) => $"{{string.content.erlcserver.updated}}: {(response.CachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.CachedAt) / 1000)}s ago" : "{string.content.erlcserver.justnow}")} | {{string.content.erlcserver.server}}: {response.Data?.JoinKey ?? "..."}";

        public class PRC_APIResponse
        {
            public PRC_Server? Data { get; init; }
            public string? Message { get; init; }
            public ErrorCode Code { get; init; } = default!;
            public long? CachedAt { get; init; }
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
            public string Name { get; init; } = default!;
            public long OwnerId { get; init; } = default!;
            public List<long> CoOwnerIds { get; init; } = [];
            public byte CurrentPlayers { get; init; } = default!;
            public byte MaxPlayers { get; init; } = default!;
            public string JoinKey { get; init; } = default!;
            public string AccVerifiedReq { get; init; } = default!;
            public bool TeamBalance { get; init; } = default!;
            public List<PRC_Player>? Players { get; init; }
            public PRC_Staff? Staff { get; init; }
            public List<PRC_JoinLog>? JoinLogs { get; init; }
            public List<long>? Queue { get; init; }
            public List<PRC_KillLog>? KillLogs { get; init; }
            public List<PRC_CommandLog>? CommandLogs { get; init; }
            public List<PRC_CallLog>? ModCalls { get; init; }
            public List<PRC_Vehicle>? Vehicles { get; init; }
        }

        public class PRC_Player
        {
            public string Team { get; init; } = default!;
            public string Player { get; init; } = default!;
            public string? Callsign { get; init; }
            public string Permission { get; init; } = default!;
            public float WantedStars { get; init; } = default!;
            public PRC_PlayerLocation Location { get; init; } = default!;
        }

        public class PRC_PlayerLocation
        {
            public float LocationX { get; init; } = -1;
            public float LocationZ { get; init; } = -1;
            public string PostalCode { get; init; } = default!;
            public string StreetName { get; init; } = default!;
            public string BuildingNumber { get; init; } = default!;
        }

        public class PRC_Staff
        {
            public Dictionary<string, string> Admins { get; init; } = [];
            public Dictionary<string, string> Mods { get; init; } = [];
            public Dictionary<string, string> Helpers { get; init; } = [];
        }

        public class PRC_JoinLog
        {
            public bool Join { get; init; } = default!;
            public long Timestamp { get; init; } = default!;
            public string Player { get; init; } = default!;
        }

        public class PRC_KillLog
        {
            public string Killer { get; init; } = default!;
            public string Killed { get; init; } = default!;
            public long Timestamp { get; init; } = default!;
        }

        public class PRC_CommandLog
        {
            public string Player { get; init; } = default!;
            public string Command { get; init; } = default!;
            public long Timestamp { get; init; } = default!;
        }

        public class PRC_CallLog
        {
            public string Caller { get; init; } = default!;
            public string? Moderator { get; init; }
            public long Timestamp { get; init; } = default!;
        }

        public class PRC_Vehicle
        {
            public string Name { get; init; } = default!;
            public string Owner { get; init; } = default!;
            public string? Texture { get; init; }
            public string ColorHex { get; init; } = default!;
            public string ColorName { get; init; } = default!;
        }
    }
}
