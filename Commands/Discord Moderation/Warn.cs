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
        public override Module Module => Module.DiscordModeration;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["warn"];
        public override List<SlashCommandArg>? Arguments => [
            new ("user", "The user to warn.", SlashCommandArgType.User),
            new ("reason", "The reason for the warning.", SlashCommandArgType.String)
        ];
        public override List<string> Schema => ["<user:user>", "<reason:string>"];
        public override List<string> Aliases => ["warn"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await DiscordModeration.ModerateFromCommand(ctx, DiscordModerationType.Warning);
        }
    }
}
