// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.OpenApi.Commands
{
    internal class AddFileCommand : BaseCommand
    {
        private const string CommandName = "file";

        public AddFileCommand(AddCommand parent)
            : base(parent, CommandName)
        {
            _sourceFileArg = Argument(SourceProjectArgName, $"The openapi file to add. This must be a path to local openapi file(s)", multipleValues: true);
        }

        internal readonly CommandArgument _sourceFileArg;

        protected override Task<int> ExecuteCoreAsync()
        {
            var projectFilePath = ResolveProjectFile(ProjectFileOption);

            Ensure.NotNullOrEmpty(_sourceFileArg.Value, SourceProjectArgName);

            foreach (var sourceFile in _sourceFileArg.Values)
            {
                var codeGenerator = CodeGenerator.NSwagCSharp;
                EnsurePackagesInProject(projectFilePath, codeGenerator);
                if (IsLocalFile(sourceFile))
                {
                    AddServiceReference(OpenApiReference, projectFilePath, sourceFile);
                }
                else
                {
                    Error.Write($"{SourceProjectArgName} of '{sourceFile}' was not valid. Valid values are a JSON file or a YAML file");
                    throw new ArgumentException();
                }
            }

            return Task.FromResult(0);
        }

        private bool IsLocalFile(string file)
        {
            var fullPath = Path.Join(WorkingDirectory, file);
            return File.Exists(fullPath) && (file.EndsWith(".json") || file.EndsWith(".yaml"));
        }

        protected override bool ValidateArguments()
        {
            Ensure.NotNullOrEmpty(_sourceFileArg.Value, SourceProjectArgName);
            return true;
        }
    }
}
