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
    public class AdminAddTime : InteractionCommandData
    {
        public override string CustomId => "sa_addtime";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

            var modal = new ModalBuilder()
                .WithCustomId($"sa_addtime {ctx.args[0]} {ctx.args[1]}")
                .WithTitle("{string.button.shiftadmin.addtime}")
                .AddTextInput(
                    label: "Time To Add",
                    customId: "time",
                    required: true,
                    placeholder: "E.G. 1h, 30m"
                )
                .Build();

            await ctx.ShowModal(modal);
        }
    }
}
