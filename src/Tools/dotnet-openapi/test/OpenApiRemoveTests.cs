// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.OpenApi.Tests;
using Microsoft.DotNet.Tools;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.OpenApi.Remove.Tests
{
    public class OpenApiRemoveTests : OpenApiTestBase
    {
        public OpenApiRemoveTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task OpenApi_Remove_File()
        {
            var nswagJsonFile = "swagger.json";
            _tempDir
                .WithCSharpProject("testproj")
                .WithTargetFrameworks("netcoreapp3.0")
                .Dir()
                .WithContentFile(nswagJsonFile)
                .WithContentFile("Startup.cs")
                .Create(true);

            var add = new Program(_console, _tempDir.Root);
            var run = add.Run(new[] { "add", nswagJsonFile });

            Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
            Assert.Equal(0, run);

            // csproj contents
            var csproj = new FileInfo(Path.Join(_tempDir.Root, "testproj.csproj"));
            using (var csprojStream = csproj.OpenRead())
            using (var reader = new StreamReader(csprojStream))
            {
                var content = await reader.ReadToEndAsync();
                Assert.Contains("<PackageReference Include=\"NSwag.MSBuild.CodeGeneration\" Version=\"", content);
                Assert.Contains($"<OpenApiReference Include=\"{nswagJsonFile}\"", content);
            }

            var remove = new Program(_console, _tempDir.Root);
            var removeRun = remove.Run(new[] { "remove", nswagJsonFile });

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
                Assert.DoesNotContain($"<OpenApiReference Include=\"{nswagJsonFile}\"", content);
            }
            Assert.False(File.Exists(Path.Combine(_tempDir.Root, nswagJsonFile)));
        }

        [Fact]
        public async Task OpenApi_Remove_Project()
        {
            _tempDir
               .WithCSharpProject("testproj")
               .WithTargetFrameworks("netcoreapp3.0")
               .Dir()
               .WithContentFile("Startup.cs")
               .Create(true);

            using(var refProj = new TemporaryDirectory())
            {
                var refProjName = "refProj";
                refProj
                    .WithCSharpProject(refProjName)
                    .WithTargetFrameworks("netcoreapp3.0")
                    .Dir()
                    .Create();

                var app = new Program(_console, _tempDir.Root);
                var refProjFile = Path.Join(refProj.Root, $"{refProjName}.csproj");
                var run = app.Run(new[] { "add", refProjFile });

                Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
                Assert.Equal(0, run);

                // csproj contents
                using (var csprojStream = new FileInfo(Path.Join(_tempDir.Root, "testproj.csproj")).OpenRead())
                using (var reader = new StreamReader(csprojStream))
                {
                    var content = await reader.ReadToEndAsync();
                    Assert.Contains("<PackageReference Include=\"NSwag.MSBuild.CodeGeneration\" Version=\"", content);
                    Assert.Contains($"<OpenApiProjectReference Include=\"{refProjFile}\"", content);
                }

                var remove = new Program(_console, _tempDir.Root);
                run = app.Run(new[] { "remove", refProjFile });

                Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
                Assert.Equal(0, run);

                // csproj contents
                using (var csprojStream = new FileInfo(Path.Join(_tempDir.Root, "testproj.csproj")).OpenRead())
                using (var reader = new StreamReader(csprojStream))
                {
                    var content = await reader.ReadToEndAsync();
                    Assert.Contains("<PackageReference Include=\"NSwag.MSBuild.CodeGeneration\" Version=\"", content);
                    Assert.DoesNotContain($"<OpenApiProjectReference Include=\"{refProjFile}\"", content);
                }
            }
        }
    }
}
