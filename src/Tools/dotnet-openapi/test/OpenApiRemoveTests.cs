// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
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
        public void OpenApi_Remove_File()
        {
            throw new NotImplementedException();
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
