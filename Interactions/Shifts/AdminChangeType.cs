using Discord;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;

namespace Whispbot.Interactions.Shifts
{
    public class AdminChangeType : InteractionCommandData
    {
        public override string CustomId => "sa_changetype";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 2) return;
            if (await ctx.CheckAllowed()) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId.Value);
            if (types is null || types.Count == 0)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.dbfailed}");
                return;
            }

            await ctx.ShowModal(new ModalBuilder()
                .WithTitle("{string.button.shiftadmin.changetype}")
                .WithCustomId($"sa_changetype {ctx.args[0]} {ctx.args[1]}")
                .AddSelectMenu(
                    label: "Select new type",
                    customId: "new_type",
                    options: [.. types.Where(t => !t.is_deleted).Select(t => new SelectMenuOptionBuilder
                    {
                        Label = t.name,
                        Value = t.id.ToString()
                    })],
                    required: true
                )
                .Build()
            );
        }
    }
}
