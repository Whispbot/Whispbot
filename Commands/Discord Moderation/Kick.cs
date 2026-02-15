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
    public class Kick : Command
    {
        public override string Name => "Kick";
        public override string Description => "Remove a user from the server";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["kick"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await DiscordModeration.ModerateFromCommand(ctx, DiscordModerationType.Kick);
        }
    }
}
