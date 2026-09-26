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

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class RobloxEditType : InteractionCommandData
    {
        public override string CustomId => "rm_edittype";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1 || ctx.interaction is not IComponentInteraction component) return;
            var data = component.Data;
            if (await ctx.CheckAllowed()) return;

            await ctx.DeferResponse();

            string? typeid = data.Value;
            if (typeid is null) return;

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId.Value);
            RobloxModerationType? type = types?.FirstOrDefault(t => t.id.ToString() == typeid);

            if (type is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("rmod.case.errors.invalid_type")}");
                return;
            }

            int caseId = int.Parse(ctx.args[1]);

            RobloxModeration? moderation = await Procedures.ChangeRMType(ctx.GuildId.Value, ctx.UserId, type, caseId);

            if (moderation is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("rmod.case.errors.not_found")}");
                return;
            }

            await ctx.EditMessage(m =>
            {
                m.Content = $"{ctx.Emoji("tick")} {ctx.String("rmod.edit.success.updated", moderation.@case.ToString())}.";
                m.Components = MessageComponent.Empty;
            });
        }
    }
}
