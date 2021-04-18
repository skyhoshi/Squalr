namespace Squalr.Cli.CommandHandlers.Crash
{
    using CommandLine;
    using Squalr.Cli.CommandHandlers.Scan;
    using Squalr.Engine;
    using System;
    using System.Collections.Generic;

    public class CrashCommandHandler : ICommandHandler
    {
        public String GetCommandName()
        {
            return "crash";
        }

        public void TryHandle(ref Session session, Command command)
        {
            command.Handled = true;

            Parser.Default.ParseArguments<CrashCommandOptions>(command.Args)
                .MapResult(
                    (CrashCommandOptions options) => options.Handle(),
                    errs => 1
                );

            Console.WriteLine();
        }

        public IEnumerable<String> GetCommandAndAliases()
        {
            return new List<String>()
            {
            };
        }
    }
    //// End class
}
//// End namespace
