namespace Squalr.Cli.CommandHandlers.Process
{
    using CommandLine;
    using System;

    [Verb("quit", HelpText = "Detaches from a running process. Alias for close.")]
    public class ProcessQuitOptions
    {
        public Int32 Handle()
        {
            return new ProcessCloseOptions().Handle();
        }
    }
    //// End class
}
//// End namespace
