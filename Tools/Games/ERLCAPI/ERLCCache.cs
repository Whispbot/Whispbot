using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Commands.ERLC;
using Whispbot.Databases;
using Whispbot.Tools.Games.ERLCAPI.Classes;

namespace Whispbot.Tools.Games.ERLCAPI
{
    public static class ERLCCache
    {
        public static readonly Dictionary<string, (DateTimeOffset, PRCResponse)> localCache = [];

        public static string GetCacheKey(PRCRequest request) => $"ERLC:{request.endpoint}:{request.serverId ?? "global"}";

        public static long GetCacheDuration(PRCResponse response) => response.success ? 60 : 10;

        public static async Task<PRCResponse?> GetCache(PRCRequest request)
        {
            if (request.method != "GET") return null;

            string key = GetCacheKey(request);
            if (localCache.TryGetValue(key, out var data))
            {
                var (expires, response) = data;

                if (expires > DateTimeOffset.UtcNow)
                {
                    return response;
                }
                else
                {
                    localCache.Remove(key);
                }
            }

            var redis = Redis.GetDatabase();
            if (redis is null) return null;

            var cachedValue = await redis.StringGetAsync(key);
            var cachedResponse = cachedValue.HasValue ? JsonConvert.DeserializeObject<PRCResponse>(cachedValue.ToString()) : null;

            if (cachedResponse is not null) localCache.Add(key, (DateTimeOffset.FromUnixTimeMilliseconds(cachedResponse.cachedAtMs).AddSeconds(GetCacheDuration(cachedResponse)), cachedResponse));

            return cachedResponse;
        }

        public static string GenerateFooter(PRCResponse response, ERLCServer? server = null) => $"{{string.content.erlcserver.updated}}: {(response.CachedAt < DateTimeOffset.UtcNow.AddSeconds(-2) ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAtMs) / 1000)}s ago" : "{string.content.erlcserver.justnow}")} | {{string.content.erlcserver.server}}: {server?.JoinKey ?? response.Server?.JoinKey ?? "..."}";
    }
}
