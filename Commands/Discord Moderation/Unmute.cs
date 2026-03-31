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
    public class Unmute : Command
    {
        public override string Name => "Unmute";
        public override string Description => "Remove the timeout from the user";
        public override Module Module => Module.DiscordModeration;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["unmute"];
        public override List<SlashCommandArg>? Arguments => [
            new ("user", "The user to unmute.", CommandArgType.User),
            new ("reason", "The reason for unmute.", CommandArgType.String, optional: true)
        ];
        public override List<string> Schema => ["<user:user>", "<reason:string?>"];
        public override List<string> Aliases => ["unmute", "untimeout", "removetimeout", "rto"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await DiscordModeration.ModerateFromCommand(ctx, DiscordModerationType.Unmute);
        }
    }
}
