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
    public class AdminChangeType : InteractionData
    {
        public override string CustomId => "sa_changetype";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 2) return;
            if (await ctx.CheckAllowed()) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId);
            if (types is null || types.Count == 0)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.dbfailed}");
                return;
            }

            await ctx.ShowModal(new ModalBuilder
            {
                title = "{string.button.shiftadmin.changetype}",
                custom_id = $"sa_changetype {ctx.args[0]} {ctx.args[1]}",
                components = [
                    new LabelBuilder("Select new type", new StringSelectBuilder("new_type")
                    {
                        options = [..types.Where(t => !t.is_deleted).Select(t => new StringSelectOption
                        {
                            label = t.name,
                            value = t.id.ToString()
                        })],
                        required = true,
                        min_values = 1,
                        max_values = 1
                    })
                ]
            });
        }
    }
}
