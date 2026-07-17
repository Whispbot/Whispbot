using Discord;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using Whispbot.Tools;
using Whispbot.Tools.Disc;

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class DeleteButton : InteractionCommandData
    {
        public override string CustomId => "rm_log_delete";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1) return;

            await ctx.Respond(
                components: 
                    new ComponentBuilderV2()
                        .WithContainer(
                            new ContainerBuilder()
                                .WithSection(
                                    new SectionBuilder()
                                        .AddComponent(
                                            new TextDisplayBuilder("{emoji.warning} {string.content.rm.confirmdelete}")
                                        )
                                        .WithAccessory(
                                            new ButtonBuilder()
                                                .WithCustomId($"rm_log_delete_confirm {ctx.args[0]}")
                                                .WithStyle(ButtonStyle.Danger)
                                                .WithLabel("{string.button.general.confirm}")
                                                .WithEmote(Emojis.Get("tick"))
                                        )
                                )
                                .WithAccentColor(new Color(150, 0, 0))
                        )
                        .Build(),
                flags: MessageFlags.ComponentsV2,
                ephemeral: true
            );
        }
    }
}
