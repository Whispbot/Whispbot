using Discord;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;

namespace Whispbot.Commands.Roblox_Moderation
{
    public class RobloxType : Command
    {
        public override string Name => "Edit Roblox Type";
        public override string Description => "Update the type for a Roblox moderation";
        public override Module Module => Module.RobloxModeration;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["roblox", "case", "type"];
        public override List<SlashCommandArg>? Arguments => [
            new ("case", "The Roblox moderation case to edit.", CommandArgType.RobloxCase),
            new ("type", "The new moderation type.", CommandArgType.RobloxType)
        ];
        public override List<string> Schema => ["<case:rcase>", "<type:rtype>"];
        public override List<string> Aliases => ["rcase type", "rtype", "rmcase type", "rmoderation type"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.RobloxModeration)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseRobloxModerations)) return;

            string? caseId = ctx.args.Get("case")?.GetString();

            if (String.IsNullOrWhiteSpace(caseId))
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("rmod.case.errors.missing_arguments")}.");
                return;
            }

            int intCaseId = 0;
            if (caseId.Equals("last", StringComparison.InvariantCultureIgnoreCase))
            {
                intCaseId = -1;
            }
            else if (new List<string>() { "slast", "server-last", "serverlast" }.Contains(caseId.ToLower()))
            {
                intCaseId = -2;
            }
            else
            {
                bool isNum = int.TryParse(caseId, out intCaseId);

                if (!isNum || intCaseId <= 0 || intCaseId >= 100_000)
                {
                    await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("rmod.case.errors.invalid_id")}");
                    return;
                }
            }

            List<RobloxModerationType> types = [..(await WhispCache.RobloxModerationTypes.Get(ctx.GuildId))?.Where(t => !t.is_deleted) ?? []];
            if (types.Count == 0)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("rmod.case.errors.no_types")}.");
                return;
            }

            await ctx.Reply(
                components: new ComponentBuilder()
                    .WithSelectMenu(
                        customId: $"rm_edittype {ctx.UserId} {intCaseId}",
                        placeholder: "Select new type",
                        options: [..types.Select(t =>
                            new SelectMenuOptionBuilder()
                                .WithLabel(t.name)
                                .WithValue(t.id.ToString())
                                .WithDescription(t.triggers.Count > 0 ? t.triggers.Join(", ") : null)
                        )]
                    )
                    .Build()
            );
        }
    }
}

