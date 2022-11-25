using System.Diagnostics;

namespace Squalr.Engine.Processes
{
    /// <summary>
    /// Defines an instance of an empty process. This can be displayed in GUIs.
    /// Attempting to attach to this process actually will cause a detach from the current target process.
    /// </summary>
    public class DetachProcess : Process
    {
        private static DetachProcess instance = new DetachProcess();

        /// <summary>
        /// Gets an instance of the detach process. Attempting to attach to this will actually cause a process detach.
        /// </summary>
        public static DetachProcess Instance
        {
            get
            {
                return instance;
            }
        }
    }
    //// End class
}
//// End namespace
