using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Staff
{
    public class SQL : Command
    {
        public override string Name => "SQL";
        public override string Description => "Run an SQL query.";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["sql"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.message.author.id != "531414889923608595")
            {
                await ctx.Reply("How about... no?");
                return;
            }

            List<object>? results = null;
            double start = DateTimeOffset.UtcNow.Ticks;
            double duration = 0;
            try
            {
                results = Postgres.Select(ctx.args.Join(" "));
            }
            catch (Exception ex)
            {
                await ctx.Reply($"Error ({(DateTimeOffset.UtcNow.Ticks - start)/TimeSpan.TicksPerMillisecond}ms): {ex.Message}");
                return;
            }
            finally
            {
                duration = (DateTimeOffset.UtcNow.Ticks - start) / TimeSpan.TicksPerMillisecond;
            }

            if (results is null)
            {
                await ctx.Reply("An error occurred while executing the SQL query.");
                return;
            }

            if (results.Count == 0)
            {
                await ctx.Reply($"No results found ({duration}ms).");
                return;
            }

            await ctx.Reply(
                new MessageBuilder
                {
                    components =
                    [
                        new TextDisplayBuilder($"{results.Count} results{(results.Count > 5 ? $" showing 1-5" : "")} ({duration}ms):"),
                        ..results.Take(5).ToList().ConvertAll((o) => new ContainerBuilder(new TextDisplayBuilder($"```json\n{o.ToJson(true)}\n```")))
                    ],
                    flags = MessageFlags.IsComponentsV2
                }
            );
        }
    }
}