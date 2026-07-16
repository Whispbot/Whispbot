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

namespace Whispbot.Interactions.Shifts
{
    public class AdminDeleteShift : InteractionCommandData
    {
        public override string CustomId => "sa_delete";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 3) return;
            if (await ctx.CheckAllowed()) return;

            await ctx.EditMessage(m =>
            {
                m.Components = new ComponentBuilderV2()
                    .WithContainer(
                        new ContainerBuilder()
                            .WithTextDisplay("{string.content.shiftadmin.deletewarning}")
                            .WithAccentColor(new Color(150, 0, 0))
                    )
                    .WithActionRow(
                        new ActionRowBuilder()
                            .WithButton(
                                "{string.buttons.shiftadmin.deletecancel}",
                                $"sa_modifyshift {ctx.args[0]} {ctx.args[1]} {ctx.args[2]} {ctx.args[3]}",
                                ButtonStyle.Secondary
                            )
                            .WithButton(
                                "{string.buttons.shiftadmin.deleteconfirm}",
                                $"sa_deleteshift {ctx.args[0]} {ctx.args[2]} {ctx.args[3]}",
                                ButtonStyle.Danger
                            )
                    )
                    .Build();
            });
        }
    }
}
