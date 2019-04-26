// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.AspNetCore.ServiceReference.Tools
{
    public enum ReferenceType
    {
        GrPC,
        OpenAPI
    }

    public class Program
    {
        private const int CriticalError = -1;
        private const int Success = 0;

        private readonly IConsole _console;
        private readonly string _workingDir;
        private IReporter _reporter;

        public Program(IConsole console, string workingDir)
        {
            Ensure.NotNull(console, nameof(console));
            Ensure.NotNullOrEmpty(workingDir, nameof(workingDir));

            _console = console;
            _workingDir = workingDir;
            _reporter = CreateReporter(verbose: true, quiet: false, console: _console);
        }

        public static int Main(string[] args)
        {
            try
            {
                DebugHelper.HandleDebugSwitch(ref args);
                var program = new Program(PhysicalConsole.Singleton, Directory.GetCurrentDirectory());

                return program.Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unexpected error:");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private const string DefaultServiceName = "Greeter";

        public int Run(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "dotnet mvc"
            };
            app.HelpOption();

            app.Command("servicereference", c =>
            {
                var verbose = c.Option("-v|--verbose",
                    "Display more debug information.",
                    CommandOptionType.NoValue);

                var quiet = c.Option("-q|--quiet",
                    "Display warnings and errors only.",
                    CommandOptionType.NoValue);

                var type = c.Argument("[type]",
                    "The type of service reference to add.");
                var serviceNameOption = c.Argument("[service-name]",
                    "The name of the service to reference.");

                c.HelpOption();

                c.OnExecute(async () =>
                {
                    if (type.Value == null)
                    {
                        _reporter.Error("type is a required argument");
                        return CriticalError;
                    }

                    if (!Enum.TryParse<ReferenceType>(type.Value, ignoreCase: true, out var enumType))
                    {
                        _reporter.Error($"`{type.Value}` is not a supported service reference");
                        return CriticalError;
                    }

                    var serviceName = serviceNameOption.Value != null ? serviceNameOption.Value : DefaultServiceName;

                    try
                    {
                        await AddServiceReferenceAsync(enumType, serviceName, _workingDir, _reporter);
                    }
                    catch(Exception ex)
                    {
                        _reporter.Error(ex.Message);
                        return CriticalError;
                    }

                    return Success;
                });
            });

            app.HelpOption("-h|--help");

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return CriticalError;
            });

            return app.Execute(args);
        }

        private static async Task AddServiceReferenceAsync(ReferenceType type, string serviceName, string workingDirectory, IReporter reporter)
        {
            // Find the project file
            var projectFile = GetProjectFileFromDirectory(workingDirectory, reporter);

            // Ensure we have the packages we'll need
            AddPackagesToProject(projectFile, type, reporter);

            var startupPath = Path.Combine(projectFile.Directory.FullName, StartupCs);
            if (!File.Exists(startupPath))
            {
                reporter.Error($"{startupPath} doesn't exist.");
                throw new ArgumentException();
            }

            // Add service references to Startup.cs
            var content = await File.ReadAllTextAsync(startupPath);

            var root = CSharpSyntaxTree.ParseText(content).GetRoot();
            var baseNamespace = root.DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>().Single().Name.ToString();

            root = AddUseEndpointsToStartup(root, type, reporter);
            root = AddServiceToConfigureService(root, type, reporter);

            using (var writer = new StreamWriter(File.OpenWrite(startupPath)))
            {
                root.WriteTo(writer);
            }

            // Create the service we're referencing
            switch(type)
            {
                case ReferenceType.GrPC:
                    await CreateGrpcServiceAsync(projectFile, serviceName, baseNamespace, reporter);
                    break;
                case ReferenceType.OpenAPI:
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }

        private static SyntaxNode AddServiceToConfigureService(SyntaxNode root, ReferenceType type, IReporter reporter)
        {
            var startupClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .SingleOrDefault(c => c.Identifier.ValueText == "Startup");
            if (startupClass == null)
            {
                reporter.Error($"There's no Startup class to edit!");
                throw new ArgumentException();
            }

            var configServMethod = FindMethod(startupClass, "ConfigureServices");
            if (configServMethod == null)
            {
                // TODO: We don't have a ConfigureServices method, make it!
                throw new NotImplementedException();
            }
            var servicesParam = configServMethod.ParameterList.Parameters.SingleOrDefault(p => p.Type.ToString() == "IServiceCollection");
            if (servicesParam == null)
            {
                // TODO: There's no services Param, make it!
                throw new NotImplementedException();
            }

            string serviceContent;
            switch (type)
            {
                case ReferenceType.GrPC:
                    serviceContent = GetServiceGrpcContent(servicesParam.Identifier.ValueText);
                    break;
                case ReferenceType.OpenAPI:
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }

            if (!configServMethod.Body.ToFullString().Contains(serviceContent))
            {
                var serviceStmt = SyntaxFactory.ParseStatement(serviceContent);
                var newConfigServMethod = configServMethod.AddBodyStatements(serviceStmt);
                return root.ReplaceNode(configServMethod, newConfigServMethod);
            }

            return root;
        }

        private static string GetServiceGrpcContent(string serviceName)
        {
            return $@"{serviceName}.AddGrpc();";
        }

        private static void AddPackagesToProject(FileInfo projectFile, ReferenceType type, IReporter reporter)
        {
            var packages = GetServicePackages(type);
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
                    RedirectStandardOutput = true
                };

                var process = Process.Start(startInfo);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    reporter.Error(process.StandardError.ReadToEnd());
                    reporter.Error($"Could not add package `{packageId}` to `{projectFile.Directory}`");
                    throw new ArgumentException();
                }
            }
        }

        private const string StartupCs = "Startup.cs";

        private static SyntaxNode AddUseEndpointsToStartup(SyntaxNode root, ReferenceType type, IReporter reporter)
        {
            var startupClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .SingleOrDefault(c => c.Identifier.ValueText == "Startup");
            if (startupClass == null)
            {
                reporter.Error($"There's no Startup class to edit!");
                throw new ArgumentException();
            }

            var configureMethod = FindMethod(startupClass, "Configure");
            if (configureMethod == null)
            {
                // TODO: We don't have a configure method, make it!
                throw new NotImplementedException();
            }
            var appParam = configureMethod.ParameterList.Parameters.SingleOrDefault(p => p.Type.ToString() == "IApplicationBuilder");
            if (appParam == null)
            {
                // TODO: There's no app Param, make it!
                throw new NotImplementedException();
            }

            var serviceName = "GreeterService";
            string endpointsContent;
            switch (type) {
                case ReferenceType.GrPC:
                    endpointsContent = GetEndpointsGrpcContent(appParam.Identifier.ValueText, serviceName);
                    break;
                case ReferenceType.OpenAPI:
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
            var useEndPoints = SyntaxFactory.ParseStatement(endpointsContent);

            var newConfigureMethod = configureMethod.AddBodyStatements(useEndPoints);
            // TODO: format this to be pretty
            return root.ReplaceNode(configureMethod, newConfigureMethod);
        }

        private static string GetEndpointsGrpcContent(string appName, string serviceName)
        {
            return $@"{appName}.UseEndpoints(endpoints =>
{{
    // Communication with gRPC endpoints must be made through a gRPC client.
    // To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909
    endpoints.MapGrpcService<{serviceName}>();
}}";
        }

        private static MethodDeclarationSyntax FindMethod(SyntaxNode root, string methodName)
        {
            return root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .SingleOrDefault(m => m.Identifier.ValueText == methodName);
        }

        private static async Task CreateGrpcServiceAsync(FileInfo projectFile, string serviceName, string projNamespace, IReporter reporter)
        {
            var servicesDir = Path.Combine(projectFile.Directory.FullName, "Services");
            var protosDir = Path.Combine(projectFile.Directory.FullName, "Protos");

            if (!Directory.Exists(servicesDir))
            {
                Directory.CreateDirectory(servicesDir);
            }

            var servicesFile = Path.Combine(servicesDir, $"{serviceName}Service.cs");
            if (!File.Exists(servicesFile))
            {
                var classContent = $@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using {serviceName};
using Grpc.Core;

namespace {projNamespace}
{{
    public class {serviceName}Service : {serviceName}.{serviceName}Base
    {{
        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {{
            return Task.FromResult(new HelloReply
            {{
                Message = ""Hello "" + request.Name
            }});
        }}
    }}
}}";
                await File.WriteAllTextAsync(servicesFile, classContent);
            }

            if (!Directory.Exists(protosDir))
            {
                Directory.CreateDirectory(protosDir);
            }

            var protosFile = Path.Combine(protosDir, $"{serviceName.ToLower()}.proto");
            if (!File.Exists(protosFile))
            {
                var protoContent = $@"syntax = ""proto3"";

service {serviceName} {{
    // Sends a greeting
    rpc SayHello (Hellorequest) returns (HelloReply) {{}}
}}

// The request message containing the user's name.
message HelloRequest {{
  string name = 1;
}}

// The response message containing the greetings.
message HelloReply {{
  string message = 1;
}}
";
                await File.WriteAllTextAsync(protosFile, protoContent);
            }
        }

        public static FileInfo GetProjectFileFromDirectory(string workingDirectory, IReporter reporter)
        {
            DirectoryInfo dir;
            try
            {
                dir = new DirectoryInfo(workingDirectory);
            }
            catch (ArgumentException ex)
            {
                reporter.Error($"Could not find directory `{workingDirectory}`");
                throw ex;
            }

            if (!dir.Exists)
            {
                reporter.Error($"Could not find directory `{workingDirectory}`");
                throw new ArgumentException();
            }

            FileInfo[] files = dir.GetFiles("*proj");
            if (files.Length == 0)
            {
                reporter.Error($"Could not find any project in directory `{workingDirectory}`");
                throw new ArgumentException();
            }

            if (files.Length > 1)
            {
                reporter.Error($"There was more than one project in directory `{workingDirectory}`");
                throw new ArgumentException();
            }

            return files.First();
        }

        private static IReporter CreateReporter(bool verbose, bool quiet, IConsole console)
            => new ConsoleReporter(console, verbose || CliContext.IsGlobalVerbose(), quiet);

        private static IEnumerable<Tuple<string, string>> GetServicePackages(ReferenceType type)
        {
            var name = Enum.GetName(typeof(ReferenceType), type);
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
    }
}
