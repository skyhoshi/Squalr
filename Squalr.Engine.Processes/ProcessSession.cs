namespace Squalr.Engine.Processes
{
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
                openedProcess = value;
            }
        }

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
