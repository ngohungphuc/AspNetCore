// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Extensions.OpenApi.Tasks
{
    /// <summary>
    /// Downloads a file.
    /// </summary>
    public static class DownloadFileExtensions
    {
        public static async Task<string> DownloadAsync(this HttpClient httpClient,
            string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                throw new ArgumentException("Uri parameter must not be null or empty.");
            }

            if (string.IsNullOrEmpty(uri))
            {
                throw new ArgumentException("DestinationPath parameter must not be null or empty.");
            }

            var builder = new UriBuilder(uri);
            if (!string.Equals(Uri.UriSchemeHttp, builder.Scheme, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Uri.UriSchemeHttps, builder.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{nameof(Uri)} parameter does not have scheme {Uri.UriSchemeHttp} or " +
                    $"{Uri.UriSchemeHttps}.");
            }

            // Timeout if the response has not begun within 1 minute
            httpClient.Timeout = TimeSpan.FromMinutes(1);

            using (var response = await httpClient.GetAsync(uri))
            {
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
        }

        public static byte[] GetHash(Stream stream)
        {
            SHA256 algorithm;
            try
            {
                algorithm = SHA256.Create();
            }
            catch (TargetInvocationException)
            {
                // SHA256.Create is documented to throw this exception on FIPS-compliant machines. See
                // https://msdn.microsoft.com/en-us/library/z08hz7ad Fall back to a FIPS-compliant SHA256 algorithm.
                algorithm = new SHA256CryptoServiceProvider();
            }

            using (algorithm)
            {
                return algorithm.ComputeHash(stream);
            }
        }

        public static byte[] GetHash(string str)
        {
            return GetHash(new MemoryStream(Encoding.UTF8.GetBytes(str)));
        }
    }
}
