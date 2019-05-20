// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.OpenApi.Tests;
using Microsoft.Extensions.Tools.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.OpenApi.Add.Tests
{
    public class OpenApiAddTests : OpenApiTestBase
    {
        public OpenApiAddTests(ITestOutputHelper output) : base(output){}

        [Fact]
        public async Task OpenApi_Add_GlobbingOpenApi()
        {
            var project = CreateBasicProject(withSwagger: true);

            using (var refProj1 = project.Project.Dir().SubDir("refProj1"))
            using (var refProj2 = project.Project.Dir().SubDir("refProj2"))
            {
                var project1 = refProj1.WithCSharpProject("refProj");
                project1
                    .WithTargetFrameworks("netcoreapp3.0")
                    .Dir()
                    .Create();

                var project2 = refProj2.WithCSharpProject("refProj2");
                project2
                    .WithTargetFrameworks("netcoreapp3.0")
                    .Dir()
                    .Create();

                var app = GetApplication();

                var run = app.Execute(new[] { "add", "project", project1.Path, project2.Path});

                Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
                Assert.Equal(0, run);

                // csproj contents
                using (var csprojStream = new FileInfo(project.Project.Path).OpenRead())
                using (var reader = new StreamReader(csprojStream))
                {
                    var content = await reader.ReadToEndAsync();
                    Assert.Contains("<PackageReference Include=\"NSwag.MSBuild.CodeGeneration\" Version=\"", content);
                    Assert.Contains($"<OpenApiProjectReference Include=\"{project1.Path}\"", content);
                    Assert.Contains($"<OpenApiProjectReference Include=\"{project2.Path}\"", content);
                }
            }
        }

        [Fact]
        public async Task OpenAPi_Add_FromCsProj()
        {
            var project = CreateBasicProject(withSwagger: false);

            using (var refProj = new TemporaryDirectory())
            {
                var refProjName = "refProj";
                refProj
                    .WithCSharpProject(refProjName)
                    .WithTargetFrameworks("netcoreapp3.0")
                    .Dir()
                    .Create();

                var app = GetApplication();
                var refProjFile = Path.Join(refProj.Root, $"{refProjName}.csproj");
                var run = app.Execute(new[] { "add", "project", refProjFile });

                Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
                Assert.Equal(0, run);

                // csproj contents
                using(var csprojStream = new FileInfo(project.Project.Path).OpenRead())
                using(var reader = new StreamReader(csprojStream))
                {
                    var content = await reader.ReadToEndAsync();
                    Assert.Contains("<PackageReference Include=\"NSwag.MSBuild.CodeGeneration\" Version=\"", content);
                    Assert.Contains($"<OpenApiProjectReference Include=\"{refProjFile}\"", content);
                }
            }
        }
    }
}
