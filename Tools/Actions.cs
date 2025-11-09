using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Tools
{
    public static class Actions
    {
        public static long? ModerateUser(long userId, long moderatorId, string reason, ActionType type, DateTimeOffset? end = null, List<ProofInsert>? proofs = null)
        {
            ModerationInsert? insert = Postgres.SelectFirst<ModerationInsert>(@"INSERT INTO actions (target, moderator, type, reason, ends_at) VALUES (@1, @2, @3, @4, @5) RETURNING id;", [userId, moderatorId, type, reason, end]);
            if (insert is null) return null;

            proofs ??= [];

            Postgres.Execute(@"INSERT INTO actions_proof (action_id, proof_type, proof_id, proof_content, reason) VALUES " +
                string.Join(", ", proofs.Select((_, i) => $"(@1, @{i * 3 + 2}, @{i * 3 + 3}, @{i * 3 + 4}, @{i * 3 + 5})")) +
                ";",
                [insert.id, .. proofs.SelectMany<ProofInsert, object>(p => [(int)p.type, p.id, p.content ?? "", p.reason ?? ""])]
            );

            Postgres.Execute(@"UPDATE user_config SET ack_required = TRUE WHERE id = @1", [userId]);

            return insert.id;
        }

        public static long? BanUser(long userId, long moderatorId, string reason, DateTimeOffset? end = null, List<ProofInsert>? proofs = null)
        {
            return ModerateUser(userId, moderatorId, reason, ActionType.Ban, end, proofs);
        }

        public static long? SilenceUser(long userId, long moderatorId, string reason, DateTimeOffset? end = null, List<ProofInsert>? proofs = null)
        {
            return ModerateUser(userId, moderatorId, reason, ActionType.Silence, end, proofs);
        }

        public static long? WarnUser(long userId, long moderatorId, string reason, List<ProofInsert>? proofs = null)
        {
            return ModerateUser(userId, moderatorId, reason, ActionType.Warning, null, proofs);
        }

        public static void AcknowledgeUser(long userId)
        {
            Postgres.Execute(@"UPDATE user_config SET ack_required = FALSE WHERE id = @1;", [userId]);
        }

        public static void AcknowledgeAction(string actionId)
        {
            Postgres.Execute(@"UPDATE actions SET acknowledged = TRUE WHERE id = @1", [actionId]);
        }

        public static void AcknowledgeAll(long userId)
        {
            Postgres.Execute(@"UPDATE actions SET acknowledged = TRUE WHERE target = @1", [userId]);
        }

        public static (ActionData?, List<Proof>) GetAction(long actionId)
        {
            ActionData? action = Postgres.SelectFirst<ActionData>(@"SELECT * FROM actions WHERE id = @1", [actionId]);
            if (action is null) return (null, []);
            List<Proof>? proofs = Postgres.Select<Proof>(@"SELECT * FROM actions_proof WHERE action_id = @1", [actionId]);
            return (action, proofs ?? []);
        }

        public static (ActionData?, List<Proof>) GetUserAction(long userId)
        {
            ActionData? action = Postgres.SelectFirst<ActionData>(@"SELECT * FROM actions WHERE target = @1 AND acknowledged = FALSE");
            if (action is null) return (null, []);
            List<Proof>? proofs = Postgres.Select<Proof>(@"SELECT * FROM actions_proof WHERE action_id = @1", [action.id]);
            return (action, proofs ?? []);
        }

        public static List<(ActionData?, List<Proof>)> GetAllActiveActions(long userId)
        {
            List<ActionData>? actions = Postgres.Select<ActionData>(@"SELECT * FROM actions WHERE target = @1 AND acknowledged = FALSE", [userId]);
            if (actions is null || actions.Count == 0) return [];
            return [.. actions.Select(a => (a, Postgres.Select<Proof>(@"SELECT * FROM actions_proof WHERE action_id = @1", [a.id]) ?? []))];
        }

        public static MessageBuilder GenerateAcknowledgeMessage(long userId)
        {
            List<(ActionData?, List<Proof>)> actions = GetAllActiveActions(userId);

            if (actions.Count == 0) {
                Postgres.Execute(@"UPDATE user_config SET ack_required = FALSE WHERE id = @1", [userId]);

                return new MessageBuilder { content = "This shouldn't have happened... whoops!" }; 
            }

            var first = actions.First();

            if (first.Item1 is null) return new MessageBuilder { content = "This shouldn't have happened... whoops!" };

            int totalProofs = first.Item2.Count;
            bool canAck = first.Item1.ends_at is null || first.Item1.ends_at < DateTimeOffset.UtcNow;

            return new MessageBuilder
            {
                embeds = [
                    new EmbedBuilder
                    {
                        title = canAck ? $"Moderation Actions (1/{actions.Count})" : $"Account Restricted",
                        description = $"You have recieved a moderation action, {(canAck ? "please review the details below and press the acknowledge button to continue use of Whisp" : $"you cannot use Whisp until <t:{first.Item1.ends_at?.ToUnixTimeSeconds() ?? 0}:f>")}.\n\nAction: {first.Item1?.type.ToString() ?? "Error"}\n```\n{first.Item1?.reason ?? "No reason provided."}\n```",
                        fields = [..first.Item2[..(Math.Min(totalProofs, 5))].Select((p, i) => new EmbedField
                        {
                            name = $"Proof {i + 1}/{totalProofs}",
                            value = $"{p.proof_type} - {p.reason}\n```\n{p.proof_content}\n```"
                        })],
                        footer = new EmbedFooter { text = $"ID: {first.Item1?.id.ToString() ?? "..."}" },
                        Color = new(150, 0, 0)
                    }
                ],
                components = [
                    new ActionRowBuilder(new ButtonBuilder($"ack_action {first.Item1?.id}") { disabled = !canAck }.SetStyle(ButtonStyle.Primary).SetLabel("Acknowledge"))
                ]
            };
        }

        public static long Acknowledge(long userId, long actionId)
        {
            long remaining = Postgres.SelectFirst<PostgresCount>(@"UPDATE actions SET acknowledged = TRUE WHERE id = @1 RETURNING (SELECT count(*) FROM actions WHERE target = @2 AND acknowledged = FALSE);", [actionId, userId])?.count ?? 0;

            if (remaining > 0) return remaining;

            AcknowledgeUser(userId);

            return 0;
        }

        public class ProofInsert
        {
            public ProofType type;
            public string id = "";
            public string? content;
            public string? reason;
        }

        private class ModerationInsert
        {
            public long id;
        }

        public class ActionData
        {
            public long id;
            public long target;
            public long moderator;
            public ActionType type;
            public string reason = "";
            public bool acknowledged = false;
            public DateTimeOffset? ends_at;
        }

        public class Proof
        {
            public long id;
            public long action_id;
            public string proof_id = "";
            public ProofType proof_type;
            public string? reason;
            public string? proof_content;
        }

        public enum ActionType
        {
            Warning = 1,
            Silence = 2,
            Ban = 3
        }

        public enum ProofType
        {
            /// <summary>
            /// ID formatted as guild:channel:message
            /// </summary>
            DiscordMessage = 1
        }
    }
}
