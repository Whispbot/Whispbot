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
    public class RobloxCase : Command
    {
        public override string Name => "Roblox Moderation Case";
        public override string Description => "View information about a Roblox moderation";
        public override Module Module => Module.RobloxModeration;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["rcase", "rmoderation case", "rmcase"];
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

            RobloxModeration? moderation = null;

            if (ctx.args[0].Equals("last", StringComparison.InvariantCultureIgnoreCase))
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 AND moderator_id = @2 ORDER BY \"case\" DESC LIMIT 1",
                    [long.Parse(ctx.GuildId), long.Parse(ctx.UserId)]
                );
            }
            else if (new List<string>() { "slast", "server-last", "serverlast" }.Contains(ctx.args[0].ToLower()))
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 ORDER BY \"case\" DESC LIMIT 1",
                    [long.Parse(ctx.GuildId)]
                );
            }
            else
            {
                bool isNum = int.TryParse(ctx.args[0], out int caseId);

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

                moderation = Postgres.SelectFirst<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 AND \"case\" = @2",
                    [long.Parse(ctx.GuildId), caseId]
                );
            }

            if (moderation is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.notfound}");
                return;
            }

            User? moderator = await DiscordCache.Users.Get(moderation.moderator_id.ToString());
            Roblox.RobloxUser? target = await Roblox.GetUser(moderation.target_id.ToString());

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);
            RobloxModerationType? type = types?.Find(t => t.id == moderation.type);

            await ctx.Reply(
                new MessageBuilder
                {
                    embeds = [
                        new EmbedBuilder
                        {
                            author = new EmbedAuthor
                            {
                                name = $"{(moderator?.global_name is not null ? moderator.global_name : $"@{moderator?.username ?? moderation.moderator_id.ToString()}")}",
                                icon_url = moderator?.avatar_url
                            },
                            title = $"{{string.title.rmcase:case={moderation.@case}}}",
                            thumbnail = new EmbedThumbnail
                            {
                                url = await Roblox.GetUserAvatar(moderation.target_id, 250)
                            },
                            fields = [
                                new EmbedField
                                {
                                    name = "{string.title.rmlog.user}",
                                    value = $"{{emoji.user}} {target?.name}\n{(!string.IsNullOrWhiteSpace(target?.displayName) && target.displayName != target.name ? $"{{emoji.chat}} {target?.displayName}\n" : "")}{{emoji.folder}} {target?.id}\n{{emoji.clock}} <t:{target?.createTime?.ToUnixTimeSeconds()}:d> (<t:{target?.createTime?.ToUnixTimeSeconds()}:R>)"
                                },
                                new EmbedField
                                {
                                    name = "{string.title.rmlog.type}",
                                    value = $"{{emoji.folder}} {type?.name ?? "Unknown Type"}{(type?.is_deleted == true ? " ({string.content.rmcase.typedeleted})" : "")}"
                                },
                                new EmbedField
                                {
                                    name = "{string.title.rmlog.reason}",
                                    value = $"{{emoji.alignment}} {moderation.reason}"
                                }
                            ]
                        }
                    ]
                }
            );
        }
    }
}
