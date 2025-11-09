using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Tools
{
    public static class Bloxlink
    {
        private static readonly string? apiKey = Environment.GetEnvironmentVariable("BLOXLINK_API_KEY");

        private static readonly HttpClient _client = new();

        private static bool _initialized = false;
        private static void Init()
        {
            _initialized = true;
            _client.DefaultRequestHeaders.Add("Authorization", apiKey ?? "");
            _client.BaseAddress = new Uri("https://api.blox.link");
        }

        public static async Task<BloxlinkRobloxUser?> RobloxFromDiscord(string discordId)
        {
            if (apiKey is null) return null;

            if (!_initialized) Init();

            HttpResponseMessage response = await _client.GetAsync($"/v4/public/discord-to-roblox/{discordId}");

            if (!response.IsSuccessStatusCode) return null;

            string content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<BloxlinkRobloxUser>(content);
        }



        public class BloxlinkRobloxUser
        {
            public string RobloxID = "";
            public BloxlinkRobloxUserResolved resolved = new();
        }
        public class BloxlinkRobloxUserResolved
        {

        }
    }
}
