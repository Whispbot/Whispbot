using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;

namespace Whispbot.Commands.Discord_Moderation
{
    public class Warn : Command
    {
        public override string Name => "Warn";
        public override string Description => "Send a user a warning message in their DMs.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["warn"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await DiscordModeration.ModerateFromCommand(ctx, DiscordModerationType.Warning);
        }
    }
}
