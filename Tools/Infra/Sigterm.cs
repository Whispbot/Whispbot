using Serilog;
using System.Runtime.InteropServices;

namespace Whispbot.Tools.Infra
{
    public static class Sigterm
    {
        public static readonly TaskCompletionSource tsc = new();
        public static bool receivedSignal = false;

        public static Task WaitForSigterm()
        {
            // Ctrl + C
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Log.Warning("SIGINT received, gracefully shutting down...");
                receivedSignal = true;
                tsc.TrySetResult();
            };

            // SIGTERM
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                if (receivedSignal)
                {
                    Log.Warning("Received SIGTERM, ignoring due to signal already processing");
                }
                else
                {
                    Log.Warning("Received SIGTERM, gracefully shutting down...");
                    receivedSignal = true;
                    tsc.TrySetResult();
                }
            });

            return tsc.Task;
        }
    }
}
