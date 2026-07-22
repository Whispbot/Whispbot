using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Languages;

namespace Whispbot.Commands.Staff
{
    public class ViewLanguages : Command
    {
        public override string Name => "Languages";
        public override string Description => "View the available languages.";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => null;
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => [];
        public override List<string> Aliases => ["languages"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            Dictionary<Language, (string, int)> languages = [];

            foreach (var (key, data) in Translator.LanguageInfo)
            {
                var strings = Translator.GetLanguageStrings(key);
                languages.Add(key, (data.Item2, strings?.Count ?? 0));
            }

            var max = languages.Max(x => x.Value.Item2);

            var sb = new StringBuilder();
            foreach (var (key, (name, strings)) in languages)
            {
                var percentage = Math.Round((double)strings / max * 100);

                var emoji = "clockedout";
                if (percentage >= 100) emoji = "clockedin";
                else if (percentage > 50) emoji = "break";

                sb.AppendLine($"{ctx.Emoji(emoji)} {name} ({percentage}%)");
            }

            await ctx.Reply($"Translations ({max}):\n{sb}");
        }
    }
}
