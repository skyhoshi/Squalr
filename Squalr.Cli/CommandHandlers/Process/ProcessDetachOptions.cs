using CommandLine;
using System;

namespace Squalr.Cli.CommandHandlers.Process
{
    [Verb("exit", HelpText = "Detaches from a running process. Alias for close.")]
    public class ProcessExitOptions
    {
        public Int32 Handle()
        {
            return new ProcessCloseOptions().Handle();
        }
    }
    //// End class
}
//// End namespace
