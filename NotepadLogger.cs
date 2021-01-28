using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using WindowsInput;

namespace NotepadLogger
{
    // Some notes:
    // Sending inputs to non-focused windows on Windows is dicey at best. I did experiment a little with this using
    // PostMessage + WM_CHAR which works great right up until you want to, you know, save the file. Oops.
    // This makes watching this thing log absolutely hilarious.
    public sealed class NotepadLogger : IDisposable
    {
        private object writeLock = new();
        private bool disposed;

        public string DestinationFile { get; init; }

        private sealed class NotepadInstance : IDisposable
        {
            private const int KeystrokeDelayMS = 5; // how long to snooze to allow keystrokes to happen, determined using observational science

            private bool disposed;
            private readonly InputSimulator inputSimulator = new();

            public Process? NotepadProcess { get; private set; }

            public NotepadInstance(string filename)
            {
                this.NotepadProcess = Process.Start(
                    new ProcessStartInfo 
                    {
                        FileName = "notepad.exe",
                        Arguments = filename,
                    });
                this.NotepadProcess.WaitForInputIdle();
            }

            public void Write(string message)
            {
                if (this.NotepadProcess == null) return;
                this.inputSimulator.Keyboard.TextEntry(message);
                // flush but dumb
                this.inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.CONTROL, WindowsInput.Native.VirtualKeyCode.VK_S);
                Thread.Sleep(KeystrokeDelayMS * (message.Length + 1));
            }

            private void Dispose(bool disposing)
            {
                if (!this.disposed)
                {
                    if (disposing)
                    {
                        if (this.NotepadProcess != null)
                        {
                            this.NotepadProcess.CloseMainWindow();
                            this.NotepadProcess.WaitForExit();

                            this.NotepadProcess.Dispose();
                            this.NotepadProcess = null;
                        }
                    }

                    this.disposed = true;
                }
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        public NotepadLogger(string destinationFile)
        {
            this.DestinationFile = Path.GetFullPath(destinationFile);

            if (!File.Exists(this.DestinationFile))
            {
                // So this is a hack because I'm lazy and doing it all in notepad was going to mean 
                // handling dialogs and all sorts of stuff and it sounded too hard idk 🤷
                using var fp = File.Open(this.DestinationFile, FileMode.Create, FileAccess.Write);
                var bytes = Encoding.UTF8.GetBytes(".LOG\r\n");
                fp.Write(bytes, 0, bytes.Length);
            }
        }

        public void Write(string message)
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(NotepadLogger));
            }

            lock (this.writeLock)
            {
                using (var notepad = new NotepadInstance(this.DestinationFile))
                {
                    notepad.Write(message);
                }
            }
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    // acquiring the write lock ensures no pending writes while we shut down
                    lock (this.writeLock)
                    {
                        this.disposed = true;
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}