// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.OpenApi.Commands
{
    internal class RefreshCommand : BaseCommand
    {
        private const string CommandName = "refresh";

        public RefreshCommand(Application parent) : base(parent, CommandName)
        { }

        protected override async Task<int> ExecuteCoreAsync()
        {
            var projectFile = ResolveProjectFile(ProjectFileOption);

            var sourceFile = Ensure.NotNullOrEmpty(SourceFileArg.Value, SourceFileArgName);

            if (IsUrl(sourceFile))
            {
                var destination = FindReferenceFromUrl(projectFile, sourceFile);
                using (var client = new HttpClient())
                {
                    await DownloadAndOverwriteAsync(sourceFile, destination, overwrite: true);
                }
            }
            else
            {
                Error.Write($"'dotnet openapi refresh' must be given a url");
                throw new ArgumentException();
            }

            return 0;
        }


        private string FindReferenceFromUrl(FileInfo projectFile, string url)
        {
            var project = LoadProject(projectFile);
            var openApiReferenceItems = project.GetItems(OpenApiReference);

            foreach (ProjectItem item in openApiReferenceItems)
            {
                var attrUrl = item.GetMetadataValue(SourceUrlAttrName);
                if (string.Equals(attrUrl, url, StringComparison.Ordinal))
                {
                    return item.EvaluatedInclude;
                }
            }

            Error.Write("There was no openapi reference to refresh with the given url.");
            throw new ArgumentException();
        }
    }
}
