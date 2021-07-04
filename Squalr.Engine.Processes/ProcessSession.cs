namespace Squalr.Engine.Processes
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Logging;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// A container for a process to open. This allows multiple systems to easily detect a changed process by sharing an instance of this class.
    /// </summary>
    public class ProcessSession
    {
        private Process openedProcess;

        public ProcessSession(Process processToOpen)
        {
            if (processToOpen != null)
            {
                Logger.Log(LogLevel.Info, "Attached to process: " + processToOpen.ProcessName + " (" + processToOpen.Id.ToString() + ")");
            }

            DetectedEmulator = EmulatorType.None;
            this.OpenedProcess = processToOpen;

            this.ListenForProcessDeath();
        }

        /// <summary>
        /// Gets a reference to the target process.
        /// </summary>
        public Process OpenedProcess
        {
            get
            {
                return openedProcess;
            }

            set
            {
                if (value == DetachProcess.Instance)
                {
                    openedProcess = null;
                }
                else
                {
                    openedProcess = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the detected emulator type. This is not automatically set, because the detection could have dependencies on scanning.
        /// It is up to the caller to store and reuse the detected emulator type here.
        /// </summary>
        public EmulatorType DetectedEmulator { get; set; }

        public void Destroy()
        {
        }

        /// <summary>
        /// Listens for process death and detaches from the process if it closes.
        /// </summary>
        private void ListenForProcessDeath()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (this.OpenedProcess?.HasExited ?? false)
                        {
                            this.OpenedProcess = null;
                        }
                    }
                    catch
                    {
                    }

                    await Task.Delay(50);
                }
            });
        }
    }
    //// End class
}
//// End namespace
