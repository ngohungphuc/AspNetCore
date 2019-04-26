// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.ServiceReference.Tools;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.Tools.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Add.ServiceReference.Tools.Tests
{
    public class ServiceReferenceTests : IDisposable
    {
        private readonly TemporaryDirectory _tempDir;
        private readonly TestConsole _console;
        private readonly StringBuilder _output = new StringBuilder();
        private readonly StringBuilder _error = new StringBuilder();

        public ServiceReferenceTests(ITestOutputHelper output)
        {
            _tempDir = new TemporaryDirectory();
            _console = new TestConsole(output)
            {
                Error = new StringWriter(_error),
                Out = new StringWriter(_output),
            };
        }

        [Fact]
        public async Task Grpc_BasicTest()
        {
            _tempDir
                .WithCSharpProject("testproj")
                .WithTargetFrameworks("netcoreapp3.0")
                .Dir()
                .WithFile("Startup.cs")
                .Create();

            var app = new Program(_console, _tempDir.Root);
            var run = app.Run(new[] { "servicereference", "grpc" });

            var err = _error.ToString();
            Assert.Equal(0, run);

            Assert.True(string.IsNullOrEmpty(_error.ToString()));

            var startupFile = new FileInfo(Path.Join(_tempDir.Root, "Startup.cs"));
            using (var startupStream = startupFile.OpenRead())
            using (var reader = new StreamReader(startupStream))
            {
                var content = await reader.ReadToEndAsync();
                Assert.Contains("services.AddGrpc()", content);
            }

            // Build project and make sure it compiles
            throw new NotImplementedException();

            // Run project and make sure it doesn't crash
            throw new NotImplementedException();

            // Hit the endpoint and make sure it works
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _tempDir.Dispose();
        }
    }
}
