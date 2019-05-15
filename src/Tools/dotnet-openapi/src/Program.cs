// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.OpenApi.Tasks;

namespace Microsoft.DotNet.OpenApi
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var outputWriter = new StringWriter();
            var errorWriter = new StringWriter();

            var application = new Application(
                Directory.GetCurrentDirectory(),
                DownloadAsync,
                outputWriter,
                errorWriter);

            var result = application.Execute(args);

            var output = outputWriter.ToString();
            var error = errorWriter.ToString();

            outputWriter.Dispose();
            errorWriter.Dispose();

            Console.Write(output);
            Console.Error.Write(error);

            return result;
        }

        public static async Task<string> DownloadAsync(string url)
        {
            using (var client = new HttpClient())
            {
                return await client.DownloadAsync(url);
            }
        }
    }
}
