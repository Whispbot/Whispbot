using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Shifts
{
    public class ShiftActivity : Command
    {
        public override string Name => "Shift Activity";
        public override string Description => "View whether users have reached their shift activity goals.";
        public override Module Module => Module.Shifts;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["shift activity"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            // !shift activity [duration] [requirement] [type]

            if (ctx.UserId is null) return;

            if (ctx.GuildId is null || ctx.Guild is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.guildonly}.");
                return;
            }

            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.Shifts)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.ManageShifts)) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId); // Fetch shift types from cache

            if (types is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockin.dbfailed}."); // Database failed (does not mean no shift types)
                return;
            }

            if (ctx.args.Count < 1)
            {
                await ctx.Reply("{emoji.warning} {strings.errors.shiftactivity.noduration}");
                return;
            }
            if (ctx.args.Count < 2)
            {
                await ctx.Reply("{emoji.warning} {strings.errors.shiftactivity.norequirement}");
                return;
            }

            double duration = Time.ConvertStringToMilliseconds(ctx.args[0]);
            double requirement = Time.ConvertStringToMilliseconds(ctx.args[1]);

            ShiftType? type = ctx.args.Count > 2 ? types.Find(t => t.triggers.Contains(ctx.args[2])) : null;

            if (ctx.args.Count > 2 && type is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.clockin.typenotfound}.");
                return;
            }

            List<ShiftUserActivity>? userActivities = Postgres.Select<ShiftUserActivity>(
                @$"
                WITH moderators AS (
                    SELECT DISTINCT moderator_id
                    FROM shifts
                    WHERE guild_id = @1
                    {(type is not null ? "AND type = @3" : "")}
                ),
                agg AS (
                    SELECT
                        moderator_id,
                        SUM(EXTRACT(EPOCH FROM (end_time - start_time)) * 1000) AS duration,
                        COUNT(*) AS shifts
                    FROM shifts
                    WHERE guild_id = @1
                      AND end_time IS NOT NULL
                      {(type is not null ? "AND type = @3" : "")}
                      AND start_time >= NOW() - (@2 * INTERVAL '1 milliseconds')
                    GROUP BY moderator_id
                )
                SELECT
                    m.moderator_id,
                    COALESCE(a.duration, 0) AS duration,
                    COALESCE(a.shifts, 0)   AS shifts
                FROM moderators m
                LEFT JOIN agg a ON a.moderator_id = m.moderator_id;
                ",
                [
                    long.Parse(ctx.GuildId),
                    duration,
                    .. (type is not null ? new List<long> { type.id } : [])
                ]
            );

            if (userActivities is null)
            {
                await ctx.Reply("{emoji.warning} {strings.errors.shiftactivity.dbfailed}");
                return;
            }
            if (userActivities.Count == 0)
            {
                await ctx.Reply("{emoji.warning} {strings.errors.shiftactivity.nodata}");
                return;
            }

            List<string> roles = [];

            if (type is not null && type.required_roles is not null)
            {
                roles = type.required_roles;
            }
            else
            {
                List<PermissionRole>? permissionRoles = await WhispPermissions.permissionRoles.Get(ctx.GuildId);

                if (permissionRoles is null)
                {
                    await ctx.Reply("{emoji.warning} {strings.errors.shiftactivity.dbfailed}");
                    return;
                }

                roles = [
                    ..permissionRoles
                    .Where(r => (r.permissions & (long)BotPermissions.UseShifts) == (long)BotPermissions.UseShifts)
                    .SelectMany(r => r.roles)
                ];
            }

            List<Member> members = await ctx.Guild.GetMembers(ctx.client, [.. userActivities.Select(ua => ua.moderator_id.ToString())], TimeSpan.FromSeconds(1));

            List<Member> eligibleMembers = [..members
                .Where(m => m.roles is not null && roles.Any(r => m.roles.Contains(r)))
            ];

            List<string> metRequirement = [];
            List<string> notMetRequirement = [];

            List<ShiftUserActivity> activities = [.. userActivities.Where(u => eligibleMembers.Any(m => m.user?.id == u.moderator_id.ToString()))];

            List<Embed> embeds = [
                new EmbedBuilder
                {
                    title = "{string.title.shiftactivity}",
                    description = $"**{{string.title.shiftactivity.totalshifts}}**: {
                        activities.Sum(u => u.shifts)
                    }\n**{{string.title.shiftactivity.totalduration}}**: {
                        Time.ConvertMillisecondsToString(activities.Sum(u => u.duration), ", ", false, 60000)
                    }",
                    footer = new EmbedFooter
                    {
                        text = $"{{string.title.shiftactivity.duration}}: {Time.ConvertMillisecondsToString(duration, ", ", true)} | {{string.title.shiftactivity.requirement}}: {Time.ConvertMillisecondsToString(requirement, ", ", true)} | {{string.title.shiftactivity.type}}: {type?.name ?? "all"}"
                    }
                }
            ];

            int metRequirementLength = 0;
            int notMetRequirementLength = 0;

            activities.Sort((a, b) => b.duration.CompareTo(a.duration));
            foreach (ShiftUserActivity activity in activities)
            {
                string line = $"<@{activity.moderator_id}> - {Time.ConvertMillisecondsToString(activity.duration, ", ", true, 60000)}";

                if (activity.duration >= requirement)
                {
                    metRequirementLength += line.Length + 1;

                    if (metRequirement.Count < Math.Ceiling(metRequirementLength / 4000d))
                    {
                        metRequirement.Add("");
                    }

                    metRequirement[^1] += line + "\n";
                }
                else
                {
                    notMetRequirementLength += line.Length + 1;

                    if (notMetRequirement.Count < Math.Ceiling(notMetRequirementLength / 4000d))
                    {
                        notMetRequirement.Add("");
                    }

                    notMetRequirement[^1] += line + "\n";
                }
            }

            if (metRequirement.Count == 0) metRequirement.Add("*{string.content.shiftactivity.nobody}.*");
            if (notMetRequirement.Count == 0) notMetRequirement.Add("*{string.content.shiftactivity.nobody}.*");

            embeds = [
                ..embeds,
                ..metRequirement.Select((v) => new EmbedBuilder
                {
                    description = v,
                    color = new Color(0, 150, 0).ToInt(),
                }),
                ..notMetRequirement.Select((v) => new EmbedBuilder
                {
                    description = v,
                    color = new Color(150, 0, 0).ToInt(),
                })
            ];

            await ctx.Reply(new MessageBuilder
            {
                embeds = embeds
            });
        }
    }

    public class ShiftUserActivity
    {
        public long moderator_id;
        public double duration;
        public int shifts;
    }
}
