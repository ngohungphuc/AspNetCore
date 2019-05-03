// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.Tools.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.OpenApi.Remove.Tests
{
    public class OpenApiRemoveTests : IDisposable
    {
        private readonly TemporaryDirectory _tempDir;
        private readonly TestConsole _console;
        private readonly StringBuilder _output = new StringBuilder();
        private readonly StringBuilder _error = new StringBuilder();
        private readonly ITestOutputHelper _outputHelper;

        public OpenApiRemoveTests(ITestOutputHelper output)
        {
            _tempDir = new TemporaryDirectory();
            _outputHelper = output;
            _console = new TestConsole(output)
            {
                Error = new StringWriter(_error),
                Out = new StringWriter(_output),
            };
        }

        [Fact]
        public async Task OpenApi_Remove_File()
        {
            var nswagJsonFIle = "swagger.json";
            _tempDir
                .WithCSharpProject("testproj")
                .WithTargetFrameworks("netcoreapp3.0")
                .Dir()
                .WithContentFile(nswagJsonFIle)
                .WithContentFile("Startup.cs")
                .Create(true);

            var add = new Program(_console, _tempDir.Root);
            var run = add.Run(new[] { "add", nswagJsonFIle });

            Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
            Assert.Equal(0, run);

            // csproj contents
            var csproj = new FileInfo(Path.Join(_tempDir.Root, "testproj.csproj"));
            using (var csprojStream = csproj.OpenRead())
            using (var reader = new StreamReader(csprojStream))
            {
                var content = await reader.ReadToEndAsync();
                Assert.Contains("<PackageReference Include=\"NSwag.MSBuild.CodeGeneration\" Version=\"", content);
                Assert.Contains($"<OpenApiReference Include=\"{nswagJsonFIle}\"", content);
            }

            var remove = new Program(_console, _tempDir.Root);
            var removeRun = remove.Run(new[] { "remove", nswagJsonFIle });

            Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
            Assert.Equal(0, removeRun);

            // csproj contents
            csproj = new FileInfo(Path.Join(_tempDir.Root, "testproj.csproj"));
            using (var csprojStream = csproj.OpenRead())
            using (var reader = new StreamReader(csprojStream))
            {
                var content = await reader.ReadToEndAsync();
                // Don't remove the package reference, they might have taken other dependencies on it
                Assert.Contains("<PackageReference Include=\"NSwag.MSBuild.CodeGeneration\" Version=\"", content);
                Assert.DoesNotContain($"<OpenApiReference Include=\"{nswagJsonFIle}\"", content);
            }
        }

        [Fact]
        public void OpenApi_Remove_Project()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _tempDir.Dispose();
        }
    }
}
