// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.DotNet.OpenApi.Commands;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.DotNet.OpenApi
{
    internal class Application : CommandLineApplication
    {
        static Application()
        {
            MSBuildLocator.RegisterDefaults();
        }

        public Application(
            CancellationToken cancellationToken,
            string workingDir,
            Func<string, Task<string>> downloadProvider,
            TextWriter output = null,
            TextWriter error = null)
        {
            CancellationToken = cancellationToken;
            DownloadProvider = downloadProvider;
            Out = output ?? Out;
            Error = error ?? Error;

            WorkingDir = workingDir;

            Name = "openapi";
            FullName = "OpenApi reference management tool";
            Description = "OpenApi reference management operations.";
            ShortVersionGetter = GetInformationalVersion;

            ProjectFileArg = Argument("project", "The project to add a reference to");

            HelpOption("-?|-h|--help");

            Commands.Add(new AddCommand(this));
            Commands.Add(new RemoveCommand(this));
            Commands.Add(new RefreshCommand(this));
        }

        public CommandArgument ProjectFileArg{ get; }

        public CancellationToken CancellationToken { get; }

        public Func<string, Task<string>> DownloadProvider { get; }

        public string WorkingDir { get; }

        public new int Execute(params string[] args)
        {
            try
            {
                return base.Execute(args);
            }
            catch (AggregateException ex) when (ex.InnerException != null)
            {
                foreach (var innerException in ex.Flatten().InnerExceptions)
                {
                    Error.WriteLine(innerException.Message);
                    Error.WriteLine(innerException.StackTrace);
                }
                return 1;
            }
            catch (CommandParsingException ex)
            {
                // Don't show a call stack when we have unneeded arguments, just print the error message.
                // The code that throws this exception will print help, so no need to do it here.
                Error.WriteLine(ex.Message);
                return 1;
            }
            catch (OperationCanceledException)
            {
                // This is a cancellation, not a failure.
                Error.WriteLine("Cancelled");
                return 1;
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex.Message);
                Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private string GetInformationalVersion()
        {
            var assembly = typeof(Application).GetTypeInfo().Assembly;
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attribute.InformationalVersion;
        }
    }
}
