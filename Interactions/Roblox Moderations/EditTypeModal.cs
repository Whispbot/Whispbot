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
    public class EditTypeModal : InteractionData
    {
        public override string CustomId => "rm_modal_edittype";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;

            var defer = ctx.DeferUpdate();

            string? newType = ctx.interaction.GetStringSelectField("type")?.FirstOrDefault();
            if (newType is null) return;

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);
            RobloxModerationType? selectedType = types?.Find(t => t.id.ToString() == newType);
            if (selectedType is null)
            {
                await defer;
                await ctx.SendFollowup("{emoji.cross} {string.errors.rmlog.dbfailed}", true);
                return;
            }

            RobloxModeration? updatedModeration = await Procedures.ChangeRMType(ctx.GuildId, ctx.UserId, selectedType, int.Parse(ctx.args[0]));

            if (updatedModeration is null)
            {
                await defer;
                await ctx.SendFollowup("{emoji.cross} {string.errors.rmlog.noeditperms}");
            }
        }
    }
}
