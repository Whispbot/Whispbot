using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Interactions;

namespace Whispbot
{
    public static class Config
    {
        public static readonly string Version = "1.0.0";
        public static bool IsDev => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static Replica? replica = null;
        public static int cluster = -1;
        public static List<string> replicas = [];
        public static string? replicaId = Environment.GetEnvironmentVariable("RAILWAY_REPLICA_ID");
        public static string deploymentId = Environment.GetEnvironmentVariable("RAILWAY_DEPLOYMENT_ID") ?? "dev";
        public static string serviceId = Environment.GetEnvironmentVariable("RAILWAY_SERVICE_ID") ?? "dev";
        public static string environmentId = Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT_ID") ?? "dev";
        public static string staffPrefix = Config.IsDev ? ">>>" : ">>";
        public static string mainGuild = "1096509172784300174";

        public static CommandManager? commands;
        public static InteractionManager? interactions;
    }

    public class Auth
    {
        public Guid id;
        public string user_id = "";
        public string username = "";
    }
}
