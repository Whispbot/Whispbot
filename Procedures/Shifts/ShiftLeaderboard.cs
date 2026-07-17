using Discord;
using Discord.WebSocket;
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
using Whispbot.Tools.Disc;

namespace Whispbot
{
    public static partial class Procedures
    {
        public static readonly int max_persons_per_leaderboard_page = 20;

        /// <summary>
        /// Generate a shift leaderboard message for a guild
        /// </summary>
        /// <param name="guildId">The ID of the guild to fetch the data for</param>
        /// <param name="userId">The ID of the current user so that only they can interact with the buttons</param>
        /// <param name="page">The page of data to look at</param>
        /// <param name="typeId">View only a specific type, leave null for all</param>
        /// <returns>(<see cref="MessageBuilder?"/>, <see cref="string?"/>) where item1 is the message itself and item2 is the error message for when we fail to fetch the data</returns>
        public static async Task<(Embed?, MessageComponent?, string?)> GenerateShiftLeaderboard(ulong guildId, ulong userId, int page = 1, ulong? typeId = null)
        {
            int max = max_persons_per_leaderboard_page;

            // Fetch a list of moderators with their total shift time
            Task<List<LeaderboardEntry>?> leaderboardTask = Task.Run(() => Postgres.Select<LeaderboardEntry>(
                @$"SELECT moderator_id, SUM(EXTRACT(EPOCH FROM (end_time - start_time)) * 1000) AS total_time
                  FROM shifts
                  WHERE guild_id = @1 AND end_time IS NOT NULL {(typeId is not null ? "AND type = @4" : "")}
                  GROUP BY moderator_id
                  ORDER BY total_time DESC
                  LIMIT @2
                  OFFSET @3;",
                [guildId, max, (page - 1) * max, ..typeId is not null ? new ulong[] { typeId.Value } : []]
            ));

            // Fetch the total count of distinct moderators for pagination
            Task<PostgresCount?> countTask = Task.Run(() => Postgres.SelectFirst<PostgresCount>(
                @$"SELECT COUNT(DISTINCT moderator_id) AS count
                  FROM shifts
                  WHERE guild_id = @1 AND end_time IS NOT NULL {(typeId is not null ? "AND type = @2" : "")};",
                [guildId, ..typeId is not null ? new ulong[] { typeId.Value } : []]
            ));

            // Run both at the same time for speed since they are independent
            await Task.WhenAll(leaderboardTask, countTask);

            List<LeaderboardEntry>? leaderboard = leaderboardTask.Result;
            PostgresCount? count = countTask.Result;

            int maxPages = count is not null ? (int)Math.Ceiling(count.count / (double)max) : 1;

            if (leaderboard is null) return (null, null, "{string.errors.shiftleaderboard.dbfailed}");
            if (leaderboard.Count == 0) return (null, null, "{string.errors.shiftleaderboard.nodata}");

            SocketGuild? thisGuild = Config.client!.GetGuild(guildId);
            ShiftType? type = typeId is not null ? (await WhispCache.ShiftTypes.Get(guildId))?.Find(t => t.id == typeId.Value) : null;

            return (
                new EmbedBuilder()
                    .WithTitle("{string.title.shiftleaderboard}")
                    .WithAuthor(thisGuild.Name, thisGuild.IconUrl)
                    .WithDescription(leaderboard
                        .Select((entry) =>
                        {
                            return $"<@{entry.moderator_id}> - {Time.ConvertMillisecondsToString(entry.total_time, ", ", false, 60000)}";
                        })
                        .Join("\n"))
                    .WithFooter($"Type: {type?.name ?? "all"}")
                    .Build(),
                new ComponentBuilder()
                    .AddRow(
                        new ActionRowBuilder()
                            .WithButton(
                                customId: $"shift_leaderboard {userId} {page - 1} {type?.id}",
                                style: ButtonStyle.Secondary,
                                emote: Emojis.Get("left"),
                                disabled: page <= 1
                            )
                            .WithButton(
                                customId: "null",
                                style: ButtonStyle.Primary,
                                label: $"{page}/{maxPages}",
                                disabled: true
                            )
                            .WithButton(
                                customId: $"shift_leaderboard {userId} {page + 1} {type?.id}",
                                style: ButtonStyle.Secondary,
                                emote: Emojis.Get("right"),
                                disabled: page >= maxPages
                            )
                    )
                    .Build(),
                null
            );
        }

        private class LeaderboardEntry
        {
            public long moderator_id = 0;
            public long total_time = 0;
        }
    }
}
