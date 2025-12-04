using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands;
using Whispbot.Commands.ERLC.Commands;
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

        private static EnvironmentType? _envId = null;
        public static EnvironmentType EnvId
        {
            get
            {
                if (_envId is not null) return _envId.Value;

                if (IsDev) return EnvironmentType.Dev;

                string? versionEnv = Environment.GetEnvironmentVariable("WHISP_ENV_ID");

                if (string.IsNullOrEmpty(versionEnv))
                {
                    return EnvironmentType.Dev;
                }

                bool parsed = Enum.TryParse<EnvironmentType>(versionEnv, true, out var envType);

                if (!parsed) return EnvironmentType.Dev;

                _envId = envType;
                return envType;
            }
        }
        public static readonly string websiteUrl = !IsDev ? Environment.GetEnvironmentVariable("WHISP_WEBSITE_URL") ?? "https://whisp.bot" : "http://localhost:3001";
        public static readonly string prefix = Environment.GetEnvironmentVariable("WHISP_LEGACY_PREFIX") ?? "!";

        public static CommandManager? commands;
        public static InteractionManager? interactions;
        public static ERLCCommandManager? erlcCommands;
    }

    public class Auth
    {
        public Guid id;
        public string user_id = "";
        public string username = "";
    }

    public enum EnvironmentType
    {
        Prod = 0,
        Beta = 1,
        Dev = 2
    }
}
