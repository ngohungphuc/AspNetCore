// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Tools.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Add.ServiceReference.Tools.Tests
{
    public class ProgramTests : IDisposable
    {
        private readonly TestConsole _console;
        public ProgramTests(ITestOutputHelper output)
        {
            _console = new TestConsole(output);
        }

        [Fact]
        public void ConsoleCancelKey()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}
