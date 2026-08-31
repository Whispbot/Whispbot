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
using Whispbot.Tools;

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class EditTypeModal : InteractionCommandData
    {
        public override string CustomId => "rm_modal_edittype";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1 || ctx.interaction is not IModalInteraction modal) return;
            var data = modal.Data;

            await ctx.DeferResponse();

            string? newType = data.Components.FirstOrDefault(c => c.CustomId == "type")?.Value;
            if (newType is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("rmod.log.errors.database")}");
                return;
            }

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId.Value);
            RobloxModerationType? selectedType = types?.Find(t => t.id.ToString() == newType);
            if (selectedType is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("rmod.log.errors.database")}");
                return;
            }

            RobloxModeration? updatedModeration = await Procedures.ChangeRMType(ctx.GuildId.Value, ctx.UserId, selectedType, int.Parse(ctx.args[0]));

            if (updatedModeration is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("rmod.log.errors.no_edit_permissions")}");
                return;
            }

            await ctx.DeleteResponse();
        }
    }
}
