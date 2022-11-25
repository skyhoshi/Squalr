namespace Squalr.Cli.CommandHandlers.Process
{
    using CommandLine;
    using System;

    [Verb("attach", HelpText = "Opens a running process. Alias for open.")]
    public class ProcessAttachOptions
    {
        public Int32 Handle()
        {
            ProcessOpenOptions Options = new ProcessOpenOptions();

            Options.NonInvasive = NonInvasive;
            Options.ProcessTerm = ProcessTerm;

            return Options.Handle();
        }

        [Option('n', "non-invasive", Required = false, HelpText = "Non-invasive attach")]
        public Boolean NonInvasive { get; set; }

        [Value(0, MetaName = "process term", HelpText = "A term to identify the process. This can be a pid, or a string in the process name.")]
        public String ProcessTerm { get; set; }
    }
    //// End class
}
//// End namespace
