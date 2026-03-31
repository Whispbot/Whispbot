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
    public class Mute : Command
    {
        public override string Name => "Mute";
        public override string Description => "Give a timeout to a user";
        public override Module Module => Module.DiscordModeration;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["mute"];
        public override List<SlashCommandArg>? Arguments => [
            new ("user", "The user to mute.", CommandArgType.User),
            new ("duration", "The duration of the mute. If not provided, the default will be used.", CommandArgType.Duration, optional: true),
            new ("reason", "The reason for the mute.", CommandArgType.String, optional: true)
        ];
        public override List<string> Schema => ["<user:user>", "<duration:durationstring?>"];
        public override List<string> Aliases => ["mute", "timeout", "to"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await DiscordModeration.ModerateFromCommand(ctx, DiscordModerationType.Mute);
        }
    }
}
