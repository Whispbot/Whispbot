using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Staff
{
    public class UpdateLanguages : Command
    {
        public override string Name => "Update Languages";
        public override string Description => "Update the current languages.";

        public override Module Module => Module.Staff;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["updatelang", "updatelanguages", "langupdate", "lu"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            double start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Tools.Strings.GetLanguages();
            double duration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start;
            await ctx.Reply($"Reset {Tools.Strings.LanguageStrings.Count} languages with {Tools.Strings.LanguageStrings.Sum(d => d.Value.Count)} total strings in {duration}ms.");
        }
    }
}
