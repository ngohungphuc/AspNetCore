// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;
using System.Diagnostics;
using System.Xml;
using System.Net.Http;
using Microsoft.Extensions.OpenApi.Tasks;
using System.Threading.Tasks;

namespace Microsoft.DotNet.OpenApi
{
    public enum CodeGenerator
    {
        NSwagCSharp
    }

    public class Program : IDisposable
    {
        private readonly IConsole _console;
        private readonly string _workingDir;
        private readonly CancellationTokenSource _cts;
        private IReporter _reporter;

        private const int Success = 0;
        private const int Failure = 1;

        public Program(IConsole console, string workingDir)
        {
            Ensure.NotNull(console, nameof(console));
            Ensure.NotNullOrEmpty(workingDir, nameof(workingDir));

            _console = console;
            _reporter = CreateReporter(verbose: false, quiet: false);
            _workingDir = workingDir;
            _cts = new CancellationTokenSource();
            _console.CancelKeyPress += OnCancelKeyPress;
        }

        public static int Main(string[] args)
        {
            return new Program(PhysicalConsole.Singleton, Directory.GetCurrentDirectory()).Run(args);
        }

        private const string SourceFileArgName = "source-file";
        private const string DefaultClassName = "MyClient";
        public const string OpenApiReference = "OpenApiReference";
        private const string OpenApiProjectReference = "OpenApiProjectReference";
        private const string IncludeAttrName = "Include";
        private const string DefaultSwaggerFile = "swagger.v1.json";
        private const string SourceUrlAttrName = "SourceUrl";

        public int Run(string[] args)
        {
            try
            {
                var app = new CommandLineApplication
                {
                    Name = "dotnet openapi"
                };

                var optProjects = app.Option("-p|--project", "The project to add a reference to", CommandOptionType.SingleValue);

                var verbose = app.Option("-v|--verbose",
                    "Display more debug information.",
                    CommandOptionType.NoValue);

                app.Command("add", c => {
                    AddCommand(c, verbose, optProjects);
                });

                app.Command("remove", c =>
                {
                    RemoveCommand(c, verbose, optProjects);
                });

                app.Command("refresh", c =>
                {
                    RefreshCommand(c, verbose, optProjects);
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
                return Failure;
            }
        }

        private void RefreshCommand(CommandLineApplication c, CommandOption verbose, CommandOption optProjects)
        {
            _reporter = CreateReporter(verbose.HasValue(), quiet: false);

            var sourceFileArg = SourceFileArgument(c, "refresh");
            c.OnExecute(async () =>
            {
                var projectFile = ResolveProjectFile(optProjects);

                var sourceFile = Ensure.NotNullOrEmpty(sourceFileArg.Value, SourceFileArgName);

                if (IsUrl(sourceFile))
                {
                    var destination = FindReferenceFromUrl(projectFile, sourceFile);
                    using (var client = new HttpClient())
                    {
                        await client.DownloadFileAsync(sourceFile, destination, _reporter, overwrite: true);
                    }
                }
                else
                {
                    _reporter.Error($"'dotnet openapi refresh' must be given a url");
                    throw new ArgumentException();
                }
            });
        }

        private void RemoveCommand(CommandLineApplication c, CommandOption verbose, CommandOption optProjects)
        {
            _reporter = CreateReporter(verbose.HasValue(), quiet: false);

            var sourceFileArg = SourceFileArgument(c, "remove");

            c.OnExecute(() =>
            {
                var projectFile = ResolveProjectFile(optProjects);

                var sourceFile = Ensure.NotNullOrEmpty(sourceFileArg.Value, SourceFileArgName);

                if(IsProjectFile(sourceFile))
                {
                    RemoveServiceReference(OpenApiProjectReference, projectFile, sourceFile);
                }
                else
                {
                    RemoveServiceReference(OpenApiReference, projectFile, sourceFile);

                    if(!Path.IsPathRooted(sourceFile))
                    {
                        sourceFile = Path.Combine(_workingDir, sourceFile);
                    }
                    File.Delete(sourceFile);
                }
            });
        }

        private void AddCommand(CommandLineApplication c, CommandOption verbose, CommandOption optProjects)
        {
            _reporter = CreateReporter(verbose.HasValue(), quiet: false);

            var sourceFileArg = SourceFileArgument(c, "add");
            var classNameOpt = c.Option("-c|--class-name", "The name of the class to be generated", CommandOptionType.SingleValue);
            var outputFileOpt = c.Option("-o|--output-file", "The name of the file to output the swagger file to", CommandOptionType.SingleValue);

            c.OnExecute(async () =>
            {
                var className = classNameOpt.HasValue() ? classNameOpt.Value() : DefaultClassName;
                var outputFile = outputFileOpt.HasValue() ? outputFileOpt.Value() : DefaultSwaggerFile;

                var projectFile = ResolveProjectFile(optProjects);

                var sourceFile = Ensure.NotNullOrEmpty(sourceFileArg.Value, SourceFileArgName);
                var codeGenerator = CodeGenerator.NSwagCSharp;
                EnsurePackagesInProject(projectFile, codeGenerator);
                if (IsProjectFile(sourceFile))
                {
                    AddServiceReference(OpenApiProjectReference, projectFile, sourceFile, className, codeGenerator);
                }
                else if (IsLocalFile(sourceFile))
                {
                    AddServiceReference(OpenApiReference, projectFile, sourceFile, className, codeGenerator);
                }
                else if (IsUrl(sourceFile))
                {
                    var destination = Path.Combine(_workingDir, outputFile);
                    // We have to download the file from that url, save it to a local file, then create a AddServiceLocalReference
                    // Use this task https://github.com/aspnet/AspNetCore/commit/91dcbd44c10af893374cfb36dc7a009caa4818d0#diff-ea7515a116529b85ad5aa8e06e4acc8e
                    using (var client = new HttpClient())
                    {
                        await client.DownloadFileAsync(sourceFile, destination, _reporter, overwrite: false);
                    }
                    AddServiceReference(OpenApiReference, projectFile, destination, className, codeGenerator, sourceFile);
                }
                else
                {
                    _reporter.Error($"{SourceFileArgName} of '{sourceFile}' was not valid. Valid values are: a JSON file, a Project File or a Url");
                    throw new ArgumentException();
                }
            });
        }

        private string FindReferenceFromUrl(FileInfo projectFile, string url)
        {
            LoadProject(projectFile, out var projNode);
            var openApiReferenceNodes = projNode.GetElementsByTagName(OpenApiReference);

            foreach (XmlElement node in openApiReferenceNodes)
            {
                var attrUrl = node.GetAttribute(SourceUrlAttrName);
                if (string.Equals(attrUrl, url, StringComparison.Ordinal))
                {
                    return node.GetAttribute(IncludeAttrName);
                }
            }

            _reporter.Error("There was no openapi reference to refresh with the given url.");
            throw new ArgumentException();
        }

        private void RemoveServiceReference(string tagName, FileInfo projectFile, string sourceFile)
        {
            var projXml = LoadProject(projectFile, out var projNode);
            var openApiReferenceNodes = projNode.GetElementsByTagName(tagName);

            foreach(XmlElement refNode in openApiReferenceNodes)
            {
                var includeAttr = refNode.GetAttribute(IncludeAttrName);
                if(string.Equals(includeAttr, sourceFile, StringComparison.Ordinal))
                {
                    refNode.ParentNode.RemoveChild(refNode);
                    projXml.Save(projectFile.FullName);
                    return;
                }
            }

            _reporter.Warn("No openapi reference was found with the given source file");
        }

        private CommandArgument SourceFileArgument(CommandLineApplication c, string action)
        {
            return c.Argument(SourceFileArgName, $"The openapi file to {action}. This can be a path to a local openapi file, " +
                "a URI to a remote openapi file or a path to a *.csproj file containing openapi endpoints");
        }

        private FileInfo ResolveProjectFile(CommandOption optProjects)
        {
            string csproj;
            if(optProjects.HasValue())
            {
                csproj = optProjects.Value();
                csproj = Path.Combine(_workingDir, csproj);
                if(!File.Exists(csproj))
                {
                    _reporter.Error("The given csproj does not exist.");
                }
            }
            else
            {
                var csprojs = Directory.GetFiles(_workingDir, "*.csproj", SearchOption.TopDirectoryOnly);
                if(csprojs.Length == 0)
                {
                    _reporter.Error("No csproj files were found in the current directory. Either move to a new directory or provide the project explicitly");
                }
                if(csprojs.Length > 1)
                {
                    _reporter.Error("More than one csproj was found in this directory, either remove a duplicate or explicitly provide the project.");
                }

                csproj = csprojs.Single();
            }

            return new FileInfo(csproj);
        }

        private void EnsurePackagesInProject(FileInfo projectFile, CodeGenerator codeGenerator)
        {
            var packages = GetServicePackages(codeGenerator);
            foreach (var (packageId, version) in packages)
            {
                var args = new string[] {
                    "add",
                    "package",
                    packageId,
                    "--version",
                    version
                };

                var startInfo = new ProcessStartInfo
                {
                    FileName = DotNetMuxer.MuxerPath,
                    Arguments = string.Join(" ", args),
                    WorkingDirectory = projectFile.Directory.FullName,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                var tcs = new TaskCompletionSource<int>();

                var process = Process.Start(startInfo);
                process.WaitForExit(20 * 1000);

                if (process.ExitCode != 0)
                {
                    _reporter.Error(process.StandardError.ReadToEnd());
                    _reporter.Error($"Could not add package `{packageId}` to `{projectFile.Directory}`");
                    throw new ArgumentException();
                }
            }
        }

        private XmlDocument LoadProject(FileInfo projectFile, out XmlElement projNode)
        {
            var projXml = new XmlDocument();
            projXml.Load(projectFile.FullName);

            var projNodes = projXml.GetElementsByTagName("Project");
            if (projNodes.Count != 1)
            {
                _reporter.Error("There must be exactly one Project element in your csproj file");
                throw new ArgumentException();
            }
            projNode = (XmlElement)projNodes[0];

            return projXml;
        }

        private void AddServiceReference(
            string tagName,
            FileInfo projectFile,
            string sourceFile,
            string className,
            CodeGenerator codeGenerator,
            string sourceUrl = null)
        {
            var projXml = LoadProject(projectFile, out var projNode);
            EnsureServiceReference(tagName, projNode, sourceFile, className, sourceUrl);

            projXml.Save(projectFile.FullName);
        }

        private void EnsureServiceReference(
            string tagName,
            XmlElement projNode,
            string sourceFile,
            string className,
            string sourceUrl)
        {
            var openApiReferenceNodes = projNode.GetElementsByTagName(tagName);

            XmlElement itemGroup;
            if(openApiReferenceNodes.Count > 0)
            {
                itemGroup = (XmlElement)openApiReferenceNodes[0].ParentNode;
                foreach (XmlElement refNode in openApiReferenceNodes)
                {
                    var includeAttr = refNode.GetAttribute(IncludeAttrName);
                    if (string.Equals(includeAttr, sourceFile, StringComparison.Ordinal))
                    {
                        // The reference already exists, nothing more to do here.
                        return;
                    }
                }
            }
            else
            {
                itemGroup = projNode.OwnerDocument.CreateElement("ItemGroup");
            }

            projNode.AppendChild(itemGroup);
            var reference = projNode.OwnerDocument.CreateElement(tagName);

            reference.SetAttribute(IncludeAttrName, sourceFile);
            reference.SetAttribute("ClassName", className);
            if(!string.IsNullOrEmpty(sourceUrl))
            {
                reference.SetAttribute(SourceUrlAttrName, sourceUrl);
            }
            itemGroup.AppendChild(reference);
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

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            // suppress CTRL+C on the first press
            args.Cancel = !_cts.IsCancellationRequested;

            if (args.Cancel)
            {
                _reporter.Output("Shutdown requested. Press Ctrl+C again to force exit.");
            }

            _cts.Cancel();
        }

        private IReporter CreateReporter(bool verbose, bool quiet)
            => new PrefixConsoleReporter("openapi : ", _console, verbose || CliContext.IsGlobalVerbose(), quiet);

        private bool IsProjectFile(string file)
        {
            return File.Exists(file) && file.EndsWith(".csproj");
        }

        private bool IsLocalFile(string file)
        {
            var fullPath = Path.Join(_workingDir, file);
            return File.Exists(fullPath) && file.EndsWith(".json");
        }

        private bool IsUrl(string file)
        {
            return Uri.TryCreate(file, UriKind.Absolute, out var _);
        }

        public void Dispose()
        {
            _console.CancelKeyPress -= OnCancelKeyPress;
            _cts.Dispose();
        }
    }
}
