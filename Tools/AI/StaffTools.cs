using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.AI
{
    public static class AIStaffTools
    {
        public static string GetGuildData(JsonDocument args)
        {
            if (args.RootElement.TryGetProperty("guildId", out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? guildId = value.GetString();
                if (string.IsNullOrEmpty(guildId))
                {
                    return "Guild ID is required.";
                }

                return JsonConvert.SerializeObject(DiscordCache.Guilds.Get(guildId));
            }
            else
            {
                return "Guild ID is required.";
            }
        }

        public static string GetUserData(JsonDocument args)
        {
            if (args.RootElement.TryGetProperty("userId", out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? userId = value.GetString();
                if (string.IsNullOrEmpty(userId))
                {
                    return "User ID is required.";
                }

                var user = DiscordCache.Users.Get(userId).WaitFor();
                if (user is null) return "User not found.";

                return JsonConvert.SerializeObject(user);
            }
            else
            {
                return "User ID is required.";
            }
        }

        public static string GetMemberData(JsonDocument args)
        {
            if (args.RootElement.TryGetProperty("guildId", out JsonElement guildValue) && guildValue.ValueKind == JsonValueKind.String &&
                args.RootElement.TryGetProperty("userId", out JsonElement userValue) && userValue.ValueKind == JsonValueKind.String)
            {
                string? guildId = guildValue.GetString();
                string? userId = userValue.GetString();
                if (string.IsNullOrEmpty(guildId) || string.IsNullOrEmpty(userId))
                {
                    return "Guild ID and User ID are required.";
                }

                var guild = DiscordCache.Guilds.Get(guildId).WaitFor();
                if (guild is null) return "Guild not found.";

                var member = guild.members.Get(userId).WaitFor();
                if (member is null) return "Member not found in the specified guild.";

                return JsonConvert.SerializeObject(member);
            }
            else
            {
                return "Guild ID and User ID are required.";
            }
        }

        public static string GetChannelData(JsonDocument args)
        {
            if (args.RootElement.TryGetProperty("channelId", out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? channelId = value.GetString();
                if (string.IsNullOrEmpty(channelId))
                {
                    return "Channel ID is required.";
                }

                var channel = DiscordCache.Channels.Get(channelId).WaitFor();
                if (channel is null) return "Channel not found.";

                return JsonConvert.SerializeObject(channel);
            }
            else
            {
                return "Channel ID is required.";
            }
        }

        public static string SearchInternet(JsonDocument args)
        {
            if (args.RootElement.TryGetProperty("query", out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string query = value.GetString() ?? "";
                int count = Math.Clamp(args.RootElement.TryGetProperty("count", out JsonElement countValue) && countValue.ValueKind == JsonValueKind.Number ? countValue.GetInt32() : 10, 1, 50);
                int start = args.RootElement.TryGetProperty("start", out JsonElement startValue) && startValue.ValueKind == JsonValueKind.Number ? startValue.GetInt32() : 1;

                var result = Tools.Google.Search(query, count, start).WaitFor();
                if (result is null) return "Failed to perform search.";
                return JsonConvert.SerializeObject(result.items);
            }
            else
            {
                return "Search query is required.";
            }
        }

        public static string SearchWhisp(JsonDocument args)
        { 
            if (args.RootElement.TryGetProperty("query", out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string query = value.GetString() ?? "";
                int count = Math.Clamp(args.RootElement.TryGetProperty("count", out JsonElement countValue) && countValue.ValueKind == JsonValueKind.Number ? countValue.GetInt32() : 10, 1, 50);
                int start = args.RootElement.TryGetProperty("start", out JsonElement startValue) && startValue.ValueKind == JsonValueKind.Number ? startValue.GetInt32() : 1;
                var result = Tools.Google.WhispSearch(query, count, start).WaitFor();
                if (result is null) return "Failed to perform search.";
                return JsonConvert.SerializeObject(result.items);
            }
            else
            {
                return "Search query is required.";
            }
        }
    }
}
