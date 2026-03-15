using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using static System.Net.Http.HttpMethod;
using System.Diagnostics.CodeAnalysis;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Cache;
using Newtonsoft.Json;
using Serilog;
using System.ComponentModel.Design.Serialization;
using System.Net;

namespace Whispbot.API
{
    public class WhispbotAPI
    {
        public static void Start()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            string port = Environment.GetEnvironmentVariable("PORT") ?? "5000";

            //if (!Config.IsDev)
            builder.Logging.ClearProviders();

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Listen(IPAddress.IPv6Any, int.Parse(port), listenOptions => { });
            });

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.IncludeFields = true;
            });

            WebApplication app = builder.Build();

            foreach (var route in routes)
            {
                foreach (var method in route.Value)
                {
                    if (method.Key == Get)
                    {
                        app.MapGet($"/api{route.Key}", method.Value);
                    }
                    else if (method.Key == Post)
                    {
                        app.MapPost($"/api{route.Key}", method.Value);
                    }
                }
            }

            Log.Information($"API started on http://localhost:{port}.");
            app.Run();
        }

        [StringSyntax("Route")]
        public static Dictionary<string, Dictionary<HttpMethod, RequestDelegate>> routes = new() {
            { "/health", new() {
                { Get, async context => {
                    if (Config.shardingManager?.shards.All(s => s.client.ready) ?? false)
                    {
                        await context.Response.WriteAsJsonAsync(new {
                            status = "ready", 
                            shards = Config.shardingManager.shards.Select(s => new
                            {
                                s.id,
                                s.totalShards,
                                s.client.ready,
                                s.client.ping,
                                guilds = s.client.readyData?.guilds.Count ?? 0
                            })
                        });
                    }
                    else
                    {
                        context.Response.StatusCode = 503;
                        await context.Response.WriteAsJsonAsync(new {status = "not ready"});
                    }
                }
                }
            } },
            { "/commands", new()
            {
                { Get, async context => {
                    await context.Response.WriteAsJsonAsync(Config.commands?.commands ?? []);
                } }
            } }
        };
    }

    internal class MutualsFormat { public string userId = ""; public List<string> guildIds = []; }
}
