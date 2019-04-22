// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.AspNetCore.ServiceReference.Tools
{
    public enum ReferenceType
    {
        GrPC,
        OpenAPI
    }

    internal class Program
    {
        private readonly IConsole _console;
        private readonly string _workingDirectory;

        private const int CriticalError = -1;
        private const int Success = 0;

        public static int Main(string[] args)
        {
            try
            {
                var app = new CommandLineApplication
                {
                    Name = "add"
                };

                app.Command("add", c =>
                {
                    c.Command("servicereference", c =>
                    {
                        var type = c.Option("-t|--type",
                            $"The type of Service Reference to add (between {nameof(ReferenceType.GrPC)} and {ReferenceType.OpenAPI})",
                            CommandOptionType.SingleValue);

                        var verbose = c.Option("-v|--verbose",
                            "Display more debug information.",
                            CommandOptionType.NoValue);

                        var quiet = c.Option("-q|--quiet",
                            "Display warnings and errors only.",
                            CommandOptionType.NoValue);

                        c.HelpOption("-h|--help");

                        c.OnExecute(() =>
                        {
                            var reporter = new ConsoleReporter(PhysicalConsole.Singleton, verbose.HasValue(), quiet.HasValue());
                        });
                    });
                });

                app.HelpOption("-h|--help");

                app.OnExecute(() =>
                {
                    app.ShowHelp();
                    return Success;
                });

                return app.Execute(args);
            }
            catch
            {
                return CriticalError;
            }
        }
    }
}
