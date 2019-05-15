// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.DotNet.OpenApi.Commands
{
    internal class AddCommand : BaseCommand
    {
        private const string CommandName = "add";

        public AddCommand(Application parent)
            : base(parent, CommandName)
        {
            Commands.Add(new AddFileCommand(this));
            Commands.Add(new AddProjectCommand(this));
        }

        internal new Application Parent => (Application)base.Parent;

        protected override Task<int> ExecuteCoreAsync()
        {
            ShowHelp();
            return Task.FromResult(1);
        }

        internal void EnsurePackagesInProject(FileInfo projectFile, CodeGenerator codeGenerator)
        {
            var packages = GetServicePackages(codeGenerator);
            foreach (var (packageId, version) in packages)
            {
                var args = new string[] {
                    "add",
                    "package",
                    packageId,
                    "--version",
                    version,
                    "--no-restore"
                };

                var startInfo = new ProcessStartInfo
                {
                    FileName = DotNetMuxer.MuxerPath,
                    Arguments = string.Join(" ", args),
                    WorkingDirectory = projectFile.Directory.FullName,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                var process = Process.Start(startInfo);
                process.WaitForExit(20 * 1000);

                if (process.ExitCode != 0)
                {
                    Error.Write(process.StandardError.ReadToEnd());
                    Error.Write(process.StandardOutput.ReadToEnd());
                    Error.Write($"Could not add package `{packageId}` to `{projectFile.Directory}`");
                    throw new ArgumentException();
                }
            }
        }

        private static IEnumerable<Tuple<string, string>> GetServicePackages(CodeGenerator type)
        {
            var name = Enum.GetName(typeof(CodeGenerator), type);
            var attributes = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            var attribute = attributes.Single(a => string.Equals(a.Key, name, StringComparison.OrdinalIgnoreCase));

            var packages = attribute.Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var result = new List<Tuple<string, string>>();
            foreach (var package in packages)
            {
                var tmp = package.Split(':', StringSplitOptions.RemoveEmptyEntries);
                Debug.Assert(tmp.Length == 2);
                result.Add(new Tuple<string, string>(tmp[0], tmp[1]));
            }

            return result;
        }

        internal void AddServiceReference(
            string tagName,
            FileInfo projectFile,
            string sourceFile,
            string className,
            CodeGenerator codeGenerator,
            string sourceUrl = null)
        {
            var project = LoadProject(projectFile);
            var items = project.GetItems(tagName);
            var item = items.SingleOrDefault(i => string.Equals(i.EvaluatedInclude, sourceFile));

            if (item == null)
            {
                var metadata = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(className))
                {
                    metadata.Add("ClassName", className);
                }

                if (!string.IsNullOrEmpty(sourceUrl))
                {
                    metadata[SourceUrlAttrName] = sourceUrl;
                }

                project.AddElementWithAttributes(tagName, sourceFile, metadata);
            }
        }
    }
}
