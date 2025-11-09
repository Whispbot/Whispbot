using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;

namespace Whispbot.Tools
{
    public static class Roblox
    {
        private static readonly HttpClient _client = new();
        private static bool _initialized = false;

        public static readonly Collection<RobloxUser> Users = new(async (key, args) =>
        {
            return await GetUserById(key);
        });

        public static readonly Dictionary<string, string> userIds = [];
        public static readonly Dictionary<string, (double, string)> userAvatars = [];

        public static bool Init()
        {
            string? apiKey = Environment.GetEnvironmentVariable("ROBLOX_API_KEY");
            if (apiKey is null) return false;

            _client.DefaultRequestHeaders.Add("x-api-key", apiKey);

            _initialized = true;
            return true;
        }

        public static async Task<RobloxUser?> GetUserById(string id)
        {
            if (!_initialized) if (!Init()) return null;

            var cachedValue = Users.FromCache(id);
            if (cachedValue is not null) return cachedValue;

            var result = await _client.GetAsync($"https://apis.roblox.com/cloud/v2/users/{id}");

            if (!result.IsSuccessStatusCode) return null;

            string content = await result.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<RobloxUser>(content);

            if (data is not null) Users.Insert(id, data);

            return data;
        }
        public static async Task<RobloxUser?> GetUserById(long id)
        {
            return await GetUserById(id.ToString());
        }

        public static async Task<string?> GetUserIdByUsername(string username)
        {
            if (!_initialized) if (!Init()) return null;

            if (userIds.TryGetValue(username.ToLower(), out string? cachedValue) && cachedValue is not null) return cachedValue;

            var result = await _client.PostAsync("https://users.roblox.com/v1/usernames/users", new StringContent(JsonConvert.SerializeObject(new
            {
                usernames = new string[] { username },
                excludeBannedUsers = false
            })));

            if (!result.IsSuccessStatusCode) return null;

            string content = await result.Content.ReadAsStringAsync();
            FromUsername data = JsonConvert.DeserializeObject<FromUsername>(content) ?? new FromUsername();

            var user = data.data.FirstOrDefault();

            if (user is not null) userIds[username.ToLower()] = user.id;

            return user?.id;
        }

        public static async Task<List<RobloxUser>?> GetUserById(List<string> ids)
        {
            if (!_initialized) if (!Init()) return null;

            List<RobloxUser> cachedUsers = Users.FindMany((u, _) => ids.Contains(u.id));
            List<string> idsToFetch = [.. ids.Except(cachedUsers.Select(u => u.id))];
            if (idsToFetch.Count == 0) return cachedUsers;

            var result = await _client.PostAsync("https://users.roblox.com/v1/users", new StringContent(JsonConvert.SerializeObject(new
            {
                userIds = idsToFetch.Select(id => long.Parse(id)),
                excludeBannedUsers = false
            })));

            if (result.IsSuccessStatusCode)
            {
                var users = JsonConvert.DeserializeObject<FromUsername>(await result.Content.ReadAsStringAsync());
                if (users is null) return cachedUsers;

                foreach (var user in users.data) Users.Insert(user.id, user);

                return [.. cachedUsers, .. users.data];
            }
            else return cachedUsers;
        }

        public static async Task<List<RobloxUser>?> SearchUsers(string keyword, int limit = 10)
        {
            if (!_initialized) if (!Init()) return null;

            limit = Math.Clamp(limit, 1, 100);
            var result = await _client.GetAsync($"https://users.roblox.com/v1/users/search?keyword={keyword}&limit={limit}");

            if (!result.IsSuccessStatusCode) return null;

            var data = JsonConvert.DeserializeObject<FromUsername>(await result.Content.ReadAsStringAsync());
            return data?.data;
        }

        public static async Task<RobloxUser?> GetUserByUsername(string username)
        {
            return (await GetUserByUsername([username]))?.FirstOrDefault();
        }

        public static async Task<List<RobloxUser>?> GetUserByUsername(List<string> usernames)
        {
            if (!_initialized) if (!Init()) return null;

            var result = await _client.PostAsync("https://users.roblox.com/v1/usernames/users", new StringContent(JsonConvert.SerializeObject(new
            {
                usernames,
                excludeBannedUsers = true
            })));

            if (!result.IsSuccessStatusCode) return null;

            var data = JsonConvert.DeserializeObject<FromUsername>(await result.Content.ReadAsStringAsync());
            return data?.data;
        }

        public static async Task<string?> GetUserAvatar(string id, int size = 250)
        {
            var cachedAvatar = userAvatars.GetValueOrDefault(id, (-1, ""));
            if (cachedAvatar.Item1 != -1 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < cachedAvatar.Item1)
            {
                return cachedAvatar.Item2;
            }

            var result = await _client.GetAsync($"https://thumbnails.roblox.com/v1/users/avatar?userIds={id}&size={size}x{size}&format=Png&isCircular=false");

            if (!result.IsSuccessStatusCode) return null;

            PFPResult? data = JsonConvert.DeserializeObject<PFPResult>(await result.Content.ReadAsStringAsync());
            if (data?.data is null) return null;

            string? url = data.data.FirstOrDefault()?.imageUrl;
            if (url is null) return null;

            userAvatars[id] = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 300000, url);
            return url;
        }
        public static async Task<string?> GetUserAvatar(long id, int size = 250)
        {
            return await GetUserAvatar(id.ToString(), size);
        }

        public class RobloxUser
        {
            [JsonConverter(typeof(RobloxUserConverter))]
            public string id = "1";
            public string name = "";
            public string? displayName = null;
            public string? about = null;
            public DateTimeOffset? createTime = DateTimeOffset.MinValue;
            public string? locale = "";
            public bool? premium = false;
        }

        public class RobloxUserConverter: JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(string);
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                var token = JToken.Load(reader);

                return token.ToString();
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                writer.WriteValue(value);
            }
        }

        public class FromUsername
        {
            public List<RobloxUser> data = [];
        }

        private class PFPResult
        {
            public List<PFPResultSingle>? data = [];
        }

        private class PFPResultSingle
        {
            public double? targetId = 0;
            public string? state = "";
            public string? imageUrl = "";
            public string? version = "";
        }
    }
}
