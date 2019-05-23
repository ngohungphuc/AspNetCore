// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.OpenApi.Tasks;

namespace Microsoft.DotNet.OpenApi.Commands
{
    internal abstract class BaseCommand : CommandLineApplication
    {
        protected string WorkingDirectory;

        protected const string SourceProjectArgName = "source-file";
        public const string OpenApiReference = "OpenApiReference";
        public const string OpenApiProjectReference = "OpenApiProjectReference";
        protected const string SourceUrlAttrName = "SourceUrl";
        
        public BaseCommand(CommandLineApplication parent, string name)
        {
            Parent = parent;
            Name = name;
            Out = parent.Out ?? Out;
            Error = parent.Error ?? Error;

            ProjectFileOption = Option("-p|--project", "The project to add a reference to", CommandOptionType.SingleValue);

            Help = HelpOption("-?|-h|--help");
            if(Parent is Application)
            {
                WorkingDirectory = ((Application)Parent).WorkingDirectory;
            }
            else
            {
                WorkingDirectory = ((Application)Parent.Parent).WorkingDirectory;
            }

            OnExecute(ExecuteAsync);
        }

        public CommandOption ProjectFileOption { get; }
                 
        internal CommandOption Help { get; }

        protected abstract Task<int> ExecuteCoreAsync();

        protected abstract bool ValidateArguments();

        private async Task<int> ExecuteAsync()
        {
            if (!ValidateArguments() || Help.HasValue())
            {
                ShowHelp();
                return 1;
            }

            return await ExecuteCoreAsync();
        }

        internal FileInfo ResolveProjectFile(CommandOption projectOption)
        {
            string project;
            if (projectOption.HasValue())
            {
                project = projectOption.Value();
                project = Path.Combine(WorkingDirectory, project);
                if (!File.Exists(project))
                {
                    Error.Write("The given project does not exist.");
                }
            }
            else
            {
                var projects = Directory.GetFiles(WorkingDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
                if (projects.Length == 0)
                {
                    Error.Write("No project files were found in the current directory. Either move to a new directory or provide the project explicitly");
                }
                if (projects.Length > 1)
                {
                    Error.Write("More than one project was found in this directory, either remove a duplicate or explicitly provide the project.");
                }

                project = projects[0];
            }

            return new FileInfo(project);
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

        internal bool IsUrl(string file)
        {
            return Uri.TryCreate(file, UriKind.Absolute, out var _) && file.StartsWith("http");
        }

        internal void AddServiceReference(
            string tagName,
            FileInfo projectFile,
            string sourceFile,
            string sourceUrl = null)
        {
            var project = LoadProject(projectFile);
            var items = project.GetItems(tagName);
            var item = items.SingleOrDefault(i => string.Equals(i.EvaluatedInclude, sourceFile));

            if (sourceUrl != null)
            {
                var urlMatch = items.SingleOrDefault(i => string.Equals(i.GetMetadataValue(SourceUrlAttrName), sourceUrl));
                if (urlMatch != null)
                {
                    Out.Write($"A reference to '{sourceUrl}' already exists in '{project.FullPath}'.");
                    return;
                }
            }

            if (item == null)
            {
                var metadata = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(sourceUrl))
                {
                    metadata[SourceUrlAttrName] = sourceUrl;
                }

                project.AddElementWithAttributes(tagName, sourceFile, metadata);
            }
            else
            {
                Out.Write($"A reference to '{sourceFile}' already exists in '{project.FullPath}'.");
            }
        }

        internal async Task DownloadToFileAsync(string url, string destinationPath, bool overwrite)
        {
            Application application;
            if(Parent is Application)
            {
                application = (Application)Parent;
            }
            else
            {
                application = (Application)Parent.Parent;
            }

            var content = await application.DownloadProvider(url);
            await WriteToFile(content, destinationPath, overwrite);
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

                if (!process.WaitForExit(5 * 1000))
                {
                    Error.Write($"Adding package `{packageId}` to `{projectFile.Directory}` took too long.");
                    throw new ArgumentException();
                }

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

        private async Task WriteToFile(Stream content, string destinationPath, bool overwrite)
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
                    await content.CopyToAsync(outStream);

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
