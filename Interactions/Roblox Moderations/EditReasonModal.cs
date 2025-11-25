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
    public class EditReasonModal : InteractionData
    {
        public override string CustomId => "rm_modal_editreason";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;

            var defer = ctx.DeferUpdate();

            string? newReason = ctx.interaction.GetStringField("reason");
            if (newReason is null) return;

            RobloxModeration? updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId, ctx.UserId, newReason, int.Parse(ctx.args[0]));

            if (updatedModeration is null)
            {
                await defer;
                await ctx.SendFollowup("{emoji.cross} {string.errors.rmlog.noeditperms}");
            }
        }
    }
}
