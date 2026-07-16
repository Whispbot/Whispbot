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
    public class AdminWipeShifts : InteractionCommandData
    {
        public override string CustomId => "sa_wipe";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 2) return;
            if (await ctx.CheckAllowed()) return;

            string? type_id = ctx.args.Count >= 3 ? ctx.args[2] : null;

            await ctx.EditMessage(m =>
            {
                m.Components = new ComponentBuilderV2()
                    .WithContainer(
                        new ContainerBuilder()
                            .WithTextDisplay("{string.content.shiftadmin.wipewarning}")
                            .WithAccentColor(new Color(150, 0, 0))
                    )
                    .WithActionRow(
                        new ActionRowBuilder()
                            .WithButton(
                                label: "{string.buttons.shiftadmin.deletecancel}",
                                customId: $"sa_main {ctx.args[0]} {ctx.args[1]} {type_id}",
                                style: ButtonStyle.Secondary
                            )
                            .WithButton(
                                label: "{string.buttons.shiftadmin.deleteconfirm}",
                                customId: $"sa_wipeconfirm {ctx.args[0]} {ctx.args[1]} {type_id}",
                                style: ButtonStyle.Danger
                            )
                    )
                    .Build();
            });
        }
    }
}
