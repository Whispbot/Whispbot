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
    public class Ban : Command
    {
        public override string Name => "Ban";
        public override string Description => "Remove a user from the server and prevent them from joining for a set amount of time";
        public override Module Module => Module.DiscordModeration;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["ban"];
        public override List<SlashCommandArg>? Arguments => [
            new ("user", "The user to ban.", CommandArgType.User),
            new ("duration", "The duration for the ban. If not provided, the default will be used.", CommandArgType.Duration, optional: true),
            new ("reason", "The reason for the ban.", CommandArgType.String, optional: true)
        ];
        public override List<string> Schema => ["<user:user>", "<duration:durationstring?>"];
        public override List<string> Aliases => ["ban"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await DiscordModeration.ModerateFromCommand(ctx, DiscordModerationType.Ban);
        }
    }
}
