// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.OpenApi.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.OpenApi.Add.Tests
{
    public class OpenApiAddURLTests : OpenApiTestBase
    {
        public OpenApiAddURLTests(ITestOutputHelper output) : base(output){ }

        [Fact]
        public async Task OpenApi_Add_Url()
        {
            var project = CreateBasicProject(withOpenApi: false);

            var app = GetApplication();
            var run = app.Execute(new[] { "add", "url", FakeOpenApiUrl });

            Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
            Assert.Equal(0, run);

            var expectedJsonName = Path.Combine(_tempDir.Root, "openapi", "openapi.json");

            // csproj contents
            using (var csprojStream = new FileInfo(project.Project.Path).OpenRead())
            using (var reader = new StreamReader(csprojStream))
            {
                var content = await reader.ReadToEndAsync();
                Assert.Contains("<PackageReference Include=\"NSwag.ApiDescription.Client\" Version=\"", content);
                Assert.Contains(
    $@"<OpenApiReference Include=""{expectedJsonName}"" SourceUrl=""{FakeOpenApiUrl}"" />", content);
            }

            Assert.True(File.Exists(expectedJsonName));
            using (var jsonStream = new FileInfo(expectedJsonName).OpenRead())
            using (var reader = new StreamReader(jsonStream))
            {
                var content = await reader.ReadToEndAsync();
                Assert.Equal(Content, content);
            }
        }


        [Fact]
        public async Task OpenApi_Add_Url_OutputFile()
        {
            var project = CreateBasicProject(withOpenApi: false);

            var app = GetApplication();
            var run = app.Execute(new[] { "add", "url", FakeOpenApiUrl, "--output-file", Path.Combine("outputdir", "file.yaml") });

            Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
            Assert.Equal(0, run);

            var expectedJsonName = Path.Combine(_tempDir.Root, "outputdir", "file.yaml");

            // csproj contents
            using (var csprojStream = new FileInfo(project.Project.Path).OpenRead())
            using (var reader = new StreamReader(csprojStream))
            {
                var content = await reader.ReadToEndAsync();
                Assert.Contains("<PackageReference Include=\"NSwag.ApiDescription.Client\" Version=\"", content);
                Assert.Contains(
    $@"<OpenApiReference Include=""{expectedJsonName}"" SourceUrl=""{FakeOpenApiUrl}"" />", content);
            }

            Assert.True(File.Exists(expectedJsonName));
            using (var jsonStream = new FileInfo(expectedJsonName).OpenRead())
            using (var reader = new StreamReader(jsonStream))
            {
                var content = await reader.ReadToEndAsync();
                Assert.Equal(Content, content);
            }
        }

        [Fact]
        public void OpenApi_Add_URL_MultipleTimes_OnlyOneReference()
        {
            var project = CreateBasicProject(withOpenApi: false);

            var app = GetApplication();
            var run = app.Execute(new[] { "add", "url", FakeOpenApiUrl });

            Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
            Assert.Equal(0, run);

            app = GetApplication();
            run = app.Execute(new[] { "add", "url", "--output-file", "openapi.yaml", FakeOpenApiUrl });

            Assert.True(string.IsNullOrEmpty(_error.ToString()), $"Threw error: {_error.ToString()}");
            Assert.Equal(0, run);

            // csproj contents
            var csproj = new FileInfo(project.Project.Path);
            using (var csprojStream = csproj.OpenRead())
            using (var reader = new StreamReader(csprojStream))
            {
                var content = reader.ReadToEnd();
                var escapedPkgRef = Regex.Escape("<PackageReference Include=\"NSwag.ApiDescription.Client\" Version=\"");
                Assert.Single(Regex.Matches(content, escapedPkgRef));
                var escapedApiRef = Regex.Escape($"SourceUrl=\"{FakeOpenApiUrl}\"");
                Assert.Single(Regex.Matches(content, escapedApiRef));
            }
        }
    }
}
