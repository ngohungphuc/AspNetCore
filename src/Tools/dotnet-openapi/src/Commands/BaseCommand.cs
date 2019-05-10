// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.OpenApi.Tasks;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.OpenApi.Commands
{
    internal abstract class BaseCommand : CommandLineApplication
    {
        protected string WorkingDir;

        protected const string SourceFileArgName = "source-file";
        protected const string DefaultSwaggerFile = "swagger.v1.json";
        public const string OpenApiReference = "OpenApiReference";
        public const string OpenApiProjectReference = "OpenApiProjectReference";
        protected const string SourceUrlAttrName = "SourceUrl";
        
        public BaseCommand(Application parent, string name)
        {
            base.Parent = parent;
            Name = name;
            Out = parent.Out ?? Out;
            Error = parent.Error ?? Error;

            SourceFileArg = Argument(SourceFileArgName, $"The openapi file to {name}. This can be a path to a local openapi file, " +
               "a URI to a remote openapi file or a path to a *.csproj file containing openapi endpoints", multipleValues: true);
            
            Help = HelpOption("-?|-h|--help");
            WorkingDir = Parent.WorkingDir;
            OnExecute(ExecuteAsync);
        }

        protected new Application Parent => (Application)base.Parent;

        public CommandArgument SourceFileArg { get; }
         
        internal CommandOption Help { get; }

        protected abstract Task<int> ExecuteCoreAsync();

        protected virtual bool ValidateArguments()
        {
            Ensure.NotNullOrEmpty(SourceFileArg.Value, SourceFileArgName);
            return true;
        }

        private async Task<int> ExecuteAsync()
        {
            if (!ValidateArguments())
            {
                ShowHelp();
                return 1;
            }

            return await ExecuteCoreAsync();
        }

        internal IEnumerable<string> ResolveSourceFiles(CommandArgument sourceArg)
        {
            var result = new List<string>();
            foreach (var sourceFile in sourceArg.Values)
            {
                if (sourceFile.Contains("*"))
                {
                    result.AddRange(Directory.EnumerateFiles(WorkingDir, sourceFile));
                }
                else
                {
                    if (File.Exists(sourceFile))
                    {
                        result.Add(sourceFile);
                    }
                }
            }

            return result;
        }

        internal FileInfo ResolveProjectFile(CommandArgument projectArg)
        {
            string csproj;
            if (!string.IsNullOrEmpty(projectArg.Value))
            {
                csproj = projectArg.Value;
                csproj = Path.Combine(WorkingDir, csproj);
                if (!File.Exists(csproj))
                {
                    Error.Write("The given csproj does not exist.");
                }
            }
            else
            {
                var csprojs = Directory.GetFiles(WorkingDir, "*.csproj", SearchOption.TopDirectoryOnly);
                if (csprojs.Length == 0)
                {
                    Error.Write("No csproj files were found in the current directory. Either move to a new directory or provide the project explicitly");
                }
                if (csprojs.Length > 1)
                {
                    Error.Write("More than one csproj was found in this directory, either remove a duplicate or explicitly provide the project.");
                }

                csproj = csprojs.Single();
            }

            return new FileInfo(csproj);
        }

        protected Project LoadProject(FileInfo projectFile)
        {
            var project = ProjectCollection.GlobalProjectCollection.LoadProject(
                projectFile.FullName,
                globalProperties: null,
                toolsVersion: null);
            project.ReevaluateIfNecessary();
            return project;
        }

        internal bool IsProjectFile(string file)
        {
            return File.Exists(file) && file.EndsWith(".csproj");
        }

        internal bool IsLocalFile(string file)
        {
            var fullPath = Path.Join(WorkingDir, file);
            return File.Exists(fullPath) && file.EndsWith(".json");
        }

        internal bool IsUrl(string file)
        {
            return Uri.TryCreate(file, UriKind.Absolute, out var _);
        }

        public async Task DownloadAndOverwriteAsync(string sourceFile, string destinationPath, bool overwrite)
        {
            var content = await Parent.DownloadProvider(sourceFile);
            await WriteToFile(content, destinationPath, overwrite);
        }

        private async Task WriteToFile(string content, string destinationPath, bool overwrite)
        {
            var destinationExists = File.Exists(destinationPath);
            if (destinationExists && !overwrite)
            {
                await Out.WriteAsync($"Not overwriting existing file '{destinationPath}'.");
                return;
            }

            await Out.WriteAsync($"Downloading to '{destinationPath}'.");
            var reachedCopy = false;
            try
            {
                if (destinationExists)
                {
                    // Check hashes before using the downloaded information.
                    var downloadHash = DownloadFileExtensions.GetHash(content);

                    byte[] destinationHash;
                    using (var destinationStream = File.OpenRead(destinationPath))
                    {
                        destinationHash = DownloadFileExtensions.GetHash(destinationStream);
                    }

                    var sameHashes = downloadHash.Length == destinationHash.Length;
                    for (var i = 0; sameHashes && i < downloadHash.Length; i++)
                    {
                        sameHashes = downloadHash[i] == destinationHash[i];
                    }

                    if (sameHashes)
                    {
                        await Out.WriteAsync($"Not overwriting existing and matching file '{destinationPath}'.");
                        return;
                    }
                }
                else
                {
                    // May need to create directory to hold the file.
                    var destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }
                }

                // Create or overwrite the destination file.
                reachedCopy = true;
                using (var outStream = File.Create(destinationPath))
                {
                    outStream.Write((Encoding.UTF8.GetBytes(content)));

                    await outStream.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                await Error.WriteAsync($"Downloading failed.");
                await Error.WriteAsync(ex.ToString());
                if (reachedCopy)
                {
                    File.Delete(destinationPath);
                }
            }
        }
    }
}
