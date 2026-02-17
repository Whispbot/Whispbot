using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Tools.Infra
{
    public static class Sigterm
    {
        public static readonly TaskCompletionSource tsc = new();
        public static bool recievedSigint = false;

        public static Task WaitForSigterm()
        {
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;

                Log.Warning("SIGINT recieved, gracefully shutting down...");

                recievedSigint = true;
                tsc.SetResult();
            };

            AppDomain.CurrentDomain.ProcessExit += (_, e) =>
            {
                if (recievedSigint)
                {
                    Log.Warning("Recieved SIGTERM, ignoring due to SIGINT already processing");
                }
                else
                {
                    Log.Warning("Recieved SIGTERM, gracefully shutting down...");
                    tsc.SetResult();
                }
            };

            return tsc.Task;
        }
    }
}
