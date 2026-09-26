using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Commands.General
{
    public class Support: Command
    {
        public override string Name => "Support";
        public override string Description => "Get support for Whispbot.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["support"];
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => [];
        public override List<string> Aliases => ["support", "help", "discord"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await ctx.Reply(
                components: new ComponentBuilderV2()
                    .WithContainer(
                        new ContainerBuilder()
                            .WithTextDisplay("Need help with whispbot? The following solutions may help you out:")
                            .WithSeparator(SeparatorSpacingSize.Large, true)
                            .WithSection([new TextDisplayBuilder("Check out our documentation.")], new ButtonBuilder(label: "Documentation", style: ButtonStyle.Link, url: "https://docs.whisp.bot"))
                            .WithSection([new TextDisplayBuilder("Join our support server.")], new ButtonBuilder(label: "Support Server", style: ButtonStyle.Link, url: "https://whisp.bot/support"))
                    )
                    .Build(),
                flags: MessageFlags.ComponentsV2
            );
        }
    }
}

