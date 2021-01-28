using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NotepadLogger
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var shutdown = false;

            Console.CancelKeyPress += (_, _) => shutdown = true;

            using (var logger = new NotepadLogger(args[0]))
            {
                var listener = new EventLog("System");
                listener.EntryWritten += (_, ev) => {
                    Console.WriteLine(ev.Entry.Message);
                    logger.Write(ev.Entry.Message);
                };
                listener.EnableRaisingEvents = true;

                while (!shutdown)
                {
                    await Task.Delay(100);
                }
            }
        }
    }
}
