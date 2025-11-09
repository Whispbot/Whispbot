using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions.Shifts
{
    public class AdminDeleteShift : InteractionData
    {
        public override string CustomId => "sa_delete";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count <= 3) return;
            if (await ctx.CheckAllowed()) return;

            await ctx.EditMessage(new MessageBuilder
            {
                components = [
                    new ContainerBuilder
                    {
                        components = [new TextDisplayBuilder("{string.content.shiftadmin.deletewarning}")],
                        accent_color = (int)new Color(150, 0, 0)
                    },
                    new ActionRowBuilder
                    {
                        components = [
                            new ButtonBuilder
                            {
                                label = "{string.buttons.shiftadmin.deletecancel}",
                                style = ButtonStyle.Secondary,
                                custom_id = $"sa_modifyshift {ctx.args[0]} {ctx.args[1]} {ctx.args[2]} {ctx.args[3]}"
                            },
                            new ButtonBuilder
                            {
                                label = "{string.buttons.shiftadmin.deleteconfirm}",
                                style = ButtonStyle.Danger,
                                custom_id = $"sa_deleteshift {ctx.args[0]} {ctx.args[2]} {ctx.args[3]}"
                            }
                        ]
                    }
                ],
                flags = MessageFlags.IsComponentsV2
            });
        }
    }
}
