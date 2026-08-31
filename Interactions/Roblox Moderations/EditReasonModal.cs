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
using Whispbot.Tools;

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class EditReasonModal : InteractionCommandData
    {
        public override string CustomId => "rm_modal_editreason";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1 || ctx.interaction is not IModalInteraction modal) return;
            var data = modal.Data;

            await ctx.DeferResponse();

            string? newReason = data.Components.FirstOrDefault(c => c.CustomId == "reason")?.Value;
            if (newReason is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("rmod.log.errors.no_edit_permissions")}");
                return;
            }

            RobloxModeration? updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId.Value, ctx.UserId, newReason, int.Parse(ctx.args[0]));

            if (updatedModeration is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("rmod.log.errors.no_edit_permissions")}");
                return;
            }

            await ctx.DeleteResponse();
        }
    }
}
