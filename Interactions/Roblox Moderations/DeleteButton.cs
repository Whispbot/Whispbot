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
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class DeleteButton : InteractionData
    {
        public override string CustomId => "rm_log_delete";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;

            await ctx.Respond(
                new MessageBuilder
                {
                    components = [
                        new ContainerBuilder
                        {
                            components = [
                                new SectionBuilder
                                {
                                    components = [
                                        new TextDisplayBuilder("{emoji.warning} {string.content.rm.confirmdelete}")
                                    ],
                                    accessory = new ButtonBuilder
                                    {
                                        custom_id = $"rm_log_delete_confirm {ctx.args[0]}",
                                        style = ButtonStyle.Danger,
                                        label = "{string.button.general.confirm}",
                                        emoji = Strings.GetEmoji("tick")
                                    }
                                }
                            ],
                            accent = new Color(150, 0, 0)
                        }
                    ],
                    flags = MessageFlags.IsComponentsV2
                },
                true
            );
        }
    }
}
