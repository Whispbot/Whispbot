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
    public class AdminAddTime : InteractionData
    {
        public override string CustomId => "sa_addtime";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

            ModalBuilder modal = new()
            {
                custom_id = $"sa_addtime {ctx.args[0]} {ctx.args[1]}",
                title = "{strings.button.shiftadmin.addtime}",
                components = [
                    new ActionRowBuilder(
                        new TextInputBuilder("Time To Add")
                        {
                            custom_id = "time",
                            required = true,
                            placeholder = "E.G. 1h, 30m"
                        }
                    )
                ]
            };

            await ctx.ShowModal(modal);
        }
    }
}
