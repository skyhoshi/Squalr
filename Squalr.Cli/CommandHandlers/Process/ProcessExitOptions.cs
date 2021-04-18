using CommandLine;
using System;

namespace Squalr.Cli.CommandHandlers.Process
{
    [Verb("detach", HelpText = "Detaches from a running process. Alias for close.")]
    public class ProcessDetachOptions
    {
        public Int32 Handle()
        {
            return new ProcessCloseOptions().Handle();
        }
    }
    //// End class
}
//// End namespace
