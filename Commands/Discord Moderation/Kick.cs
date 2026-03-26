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
        public override Module Module => Module.DiscordModeration;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["kick"];
        public override List<SlashCommandArg>? Arguments => [
            new ("user", "The user to kick.", CommandArgType.User),
            new ("reason", "The reason for the kick.", CommandArgType.String, optional: true)
        ];
        public override List<string> Schema => ["<user:user>", "<reason:string?>"];
        public override List<string> Aliases => ["kick"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await DiscordModeration.ModerateFromCommand(ctx, DiscordModerationType.Kick);
        }
    }
}
