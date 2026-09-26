using Discord;
using Discord.Rest;
using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Disc
{
    public static class Emojis
    {
        private static Dictionary<string, Emote> _emojis = [];

        public static async Task GetEmojis()
        {
            string? token = Environment.GetEnvironmentVariable("CLIENT_TOKEN");
            if (token is null) return;

            var client = new DiscordRestClient();
            await client.LoginAsync(TokenType.Bot, token);

            var emotes = await client.GetApplicationEmotesAsync();

            _emojis = emotes.ToDictionary(e => e.Name.ToLower(), e => e);
        }

        public static readonly Emoji FALLBACK_EMOJI = new("\u274c"); // ❌
        public static IEmote Get(string name)
        {
            var emoji = _emojis.GetValueOrDefault(name.ToLower());

            if (emoji is not null) return emoji;
            else return FALLBACK_EMOJI; // ❌ if not found
        }
    }
}
