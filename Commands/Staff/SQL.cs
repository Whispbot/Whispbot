using Discord;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;

namespace Whispbot.Commands.Staff
{
    public class SQL : Command
    {
        public override string Name => "SQL";
        public override string Description => "Run an SQL query.";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => null;
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => ["<query:string>"];
        public override List<string> Aliases => ["sql"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.UserId != 531414889923608595L)
            {
                await ctx.Reply("How about... no?");
                return;
            }

            string? query = ctx.args.Get("query")?.GetString();
            if (String.IsNullOrWhiteSpace(query))
            {
                await ctx.Reply("You forgot the query idiot");
                return;
            }

            List<object>? results = null;
            double start = DateTimeOffset.UtcNow.Ticks;
            double duration = 0;
            try
            {
                results = Postgres.Select(query);
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

            var components = new ComponentBuilderV2()
                .WithTextDisplay($"{results.Count} results{(results.Count > 5 ? $" showing 1-5" : "")} ({duration}ms):");

            foreach (var result in results.Take(5))
            {
                components.WithContainer(new ContainerBuilder().WithTextDisplay($"```json\n{JsonConvert.SerializeObject(result, Formatting.Indented)}\n```"));
            }

            await ctx.Reply(
                components: components.Build(),
                flags: MessageFlags.ComponentsV2
            );
        }
    }
}
