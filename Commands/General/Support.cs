using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Core;

namespace Whispbot.Commands.General
{
    public class Support: Command
    {
        public override string Name => "Support";
        public override string Description => "Get support for Whispbot.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["support", "help", "discord"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await ctx.Reply(
                new MessageBuilder
                {
                    components = [
                        new ContainerBuilder
                        {
                            components = [
                                new TextDisplayBuilder("Need help with whispbot? The following solutions may help you out:"),
                                new SeperatorBuilder(true, SeperatorSpacing.Large),
                                new SectionBuilder
                                {
                                    components = [ new TextDisplayBuilder("Check out our documentation.") ],
                                    accessory = new ButtonBuilder { label = "Documentation", url = "https://docs.whisp.bot" }
                                },
                                new SectionBuilder
                                {
                                    components = [ new TextDisplayBuilder("Join our support server.") ],
                                    accessory = new ButtonBuilder { label = "Support Server", url = "https://whisp.bot/support" }
                                }
                            ]
                        }
                    ],
                    flags = MessageFlags.IsComponentsV2
                }
            );
        }
    }
}
