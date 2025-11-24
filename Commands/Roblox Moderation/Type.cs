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
    public class RobloxType : Command
    {
        public override string Name => "Edit Roblox Type";
        public override string Description => "Update the type for a Roblox moderation";
        public override Module Module => Module.RobloxModeration;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["rcase type", "rtype", "rmcase type", "rmoderation type"];
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
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseRobloxModerations)) return;

            if (ctx.args.Count < 1)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.missingargs}.");
                return;
            }

            int caseId = 0;
            if (ctx.args[0].Equals("last", StringComparison.InvariantCultureIgnoreCase))
            {
                caseId = -1;
            }
            else if (new List<string>() { "slast", "server-last", "serverlast" }.Contains(ctx.args[0].ToLower()))
            {
                ctx.args.RemoveAt(0);
                caseId = -2;
            }
            else
            {
                bool isNum = int.TryParse(ctx.args[0], out caseId);

                if (!isNum)
                {
                    await ctx.Reply("{emoji.cross} {string.errors.rmcase.invalidid}");
                    return;
                }

                if (caseId <= 0 || caseId >= 100_000)
                {
                    await ctx.Reply("{emoji.cross} {string.errors.rmcase.invalidid}");
                    return;
                }
            }

            List<RobloxModerationType> types = [..(await WhispCache.RobloxModerationTypes.Get(ctx.GuildId))?.Where(t => !t.is_deleted) ?? []];
            if (types.Count == 0)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.notypes}.");
                return;
            }

            await ctx.Reply(
                new MessageBuilder
                {
                    components = [
                        new ActionRowBuilder
                        {
                            components = [
                                new StringSelectBuilder($"rm_edittype {ctx.UserId} {caseId}")
                                {
                                    placeholder = "Select new type",
                                    options = [..types.Select(t => 
                                        new StringSelectOption
                                        {
                                            label = t.name,
                                            value = t.id.ToString(),
                                            description = t.triggers.Count > 0 ? t.triggers.Join() : null
                                        }
                                    )]
                                }
                            ]
                        }
                    ]
                }
            );
        }
    }
}
