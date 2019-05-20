// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.OpenApi.Commands
{
    internal class AddProjectCommand :  BaseCommand
    {
        private const string CommandName = "project";

        public AddProjectCommand(BaseCommand parent)
            : base(parent, CommandName)
        {
            _classNameOpt = Option("-c|--class-name", "The name of the class to be generated", CommandOptionType.SingleValue);
            _sourceProjectArg = Argument(SourceProjectArgName, $"The openapi project to add. This must be a path to *.csproj file containing openapi endpoints", multipleValues: true);
        }

        internal readonly CommandOption _classNameOpt;

        internal readonly CommandArgument _sourceProjectArg;

        private new AddCommand Parent => (AddCommand)base.Parent;

        protected override Task<int> ExecuteCoreAsync()
        {
            var className = _classNameOpt.Value();

            var projectFilePath = ResolveProjectFile(ProjectFileOption);

            foreach (var sourceFile in _sourceProjectArg.Values)
            {
                var codeGenerator = CodeGenerator.NSwagCSharp;
                Parent.EnsurePackagesInProject(projectFilePath, codeGenerator);
                if (IsProjectFile(sourceFile))
                {
                    Parent.AddServiceReference(OpenApiProjectReference, projectFilePath, sourceFile, className);
                }
                else
                {
                    Error.Write($"{SourceProjectArgName} of '{sourceFile}' was not valid. Valid values are: a JSON file, a Project File or a Url");
                    throw new ArgumentException();
                }
            }

            return Task.FromResult(0);
        }

        protected override bool ValidateArguments()
        {
            Ensure.NotNullOrEmpty(_sourceProjectArg.Value, SourceProjectArgName);
            return true;
        }
    }
}
