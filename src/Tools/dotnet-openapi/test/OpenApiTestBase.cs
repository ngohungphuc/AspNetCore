// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.Tools.Internal;
using Xunit.Abstractions;

namespace Microsoft.DotNet.OpenApi.Tests
{
    public class OpenApiTestBase : IDisposable
    {
        protected readonly TemporaryDirectory _tempDir;
        protected readonly TestConsole _console;
        private readonly StringBuilder _output = new StringBuilder();
        protected readonly StringBuilder _error = new StringBuilder();
        protected readonly ITestOutputHelper _outputHelper;

        // TODO: Use a more permanent URL
        protected const string SwaggerJsonUrl = "https://raw.githubusercontent.com/glennc/clientgen/master/ConsoleClient/Server.v1.json";

        public OpenApiTestBase(ITestOutputHelper output)
        {
            _tempDir = new TemporaryDirectory();
            _outputHelper = output;
            _console = new TestConsole(output)
            {
                Error = new StringWriter(_error),
                Out = new StringWriter(_output),
            };
        }

        public TemporaryNSwagProject CreateBasicProject(bool withSwagger)
        {
            var nswagJsonFile = "swagger.json";
            var project = _tempDir
                .WithCSharpProject("testproj")
                .WithTargetFrameworks("netcoreapp3.0");
            var tmp = project.Dir();

            if(withSwagger)
            {
                tmp = tmp.WithContentFile(nswagJsonFile);
            }
                
            tmp.WithContentFile("Startup.cs")
                .Create(true);

            return new TemporaryNSwagProject(project, nswagJsonFile);
        }

        public void Dispose()
        {
            _tempDir.Dispose();
        }
    }

    public class TemporaryNSwagProject
    {
        public TemporaryNSwagProject(TemporaryCSharpProject project, string jsonFile)
        {
            Project = project;
            NSwagJsonFile = jsonFile;
        }

        public TemporaryCSharpProject Project { get; set; }
        public string NSwagJsonFile { get; set; }
    }
}
