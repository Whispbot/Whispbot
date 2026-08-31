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
    public class AdminSetTime : InteractionCommandData
    {
        public override string CustomId => "sa_settime";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

            var modal = new ModalBuilder()
                .WithCustomId($"sa_settime {ctx.args[0]} {ctx.args[1]}")
                .WithTitle($"{ctx.String("shifts.admin.button.set_time")}")
                .AddTextInput(
                    label: "Time To Set",
                    customId: "time",
                    placeholder: "E.G. 1h, 30m",
                    required: true
                )
                .Build();

            await ctx.ShowModal(modal);
        }
    }
}
