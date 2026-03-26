using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Roblox_Moderation
{
    public class RobloxVoid : Command
    {
        public override string Name => "Void Roblox Moderation";
        public override string Description => "Delete a Roblox moderation";
        public override Module Module => Module.RobloxModeration;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["roblox", "case", "void"];
        public override List<SlashCommandArg>? Arguments => [
            new ("case", "The Roblox moderation case to void.", CommandArgType.RobloxCase)
        ];
        public override List<string> Schema => ["<case:rcase>"];
        public override List<string> Aliases => ["rcase void", "rvoid", "rmcase void", "rmoderation void"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.UserId is null) return;

            if (ctx.GuildId is null || ctx.Guild is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.guildonly}.");
                return;
            }

            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.RobloxModeration)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseRobloxModerations | BotPermissions.ManageRobloxModerations)) return;

            string? caseId = ctx.args.Get("case")?.GetString();

            if (String.IsNullOrWhiteSpace(caseId))
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.missingargs}.");
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
                    await ctx.Reply("{emoji.cross} {string.errors.rmcase.invalidid}");
                    return;
                }
            }

            RobloxModeration? moderation = await Procedures.DeleteRM(ctx.GuildId, ctx.UserId, intCaseId);

            if (moderation is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.notfound}");
                return;
            }

            await ctx.Reply($"{{emoji.tick}} {{string.success.rmvoid:case={moderation.@case}}}");
        }
    }
}

