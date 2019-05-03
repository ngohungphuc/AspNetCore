// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.Extensions.OpenApi.Tasks
{
    /// <summary>
    /// Downloads a file.
    /// </summary>
    public static class DownloadFileExtensions
    {

        public static async Task DownloadFileAsync(this HttpClient client, string uri, string destination, IReporter reporter, bool overwrite = false, int timeoutSeconds = 60 * 2)
        {
            if (string.IsNullOrEmpty(uri))
            {
                reporter.Error("Uri parameter must not be null or empty.");
                throw new ArgumentException();
            }

            if (string.IsNullOrEmpty(uri))
            {
                reporter.Error("DestinationPath parameter must not be null or empty.");
                throw new ArgumentException();
            }

            var builder = new UriBuilder(uri);
            if (!string.Equals(Uri.UriSchemeHttp, builder.Scheme, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Uri.UriSchemeHttps, builder.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                reporter.Error($"{nameof(Uri)} parameter does not have scheme {Uri.UriSchemeHttp} or " +
                    $"{Uri.UriSchemeHttps}.");
                throw new ArgumentException();
            }

            await DownloadAsync(client, uri, destination, overwrite, timeoutSeconds, reporter);
        }

        private static async Task DownloadAsync(
            HttpClient client,
            string uri,
            string destinationPath,
            bool overwrite,
            int timeoutSeconds,
            IReporter reporter)
        {
            var destinationExists = File.Exists(destinationPath);
            if (destinationExists && !overwrite)
            {
                reporter.Output($"Not downloading '{uri}' to overwrite existing file '{destinationPath}'.");
                return;
            }

            reporter.Output($"Downloading '{uri}' to '{destinationPath}'.");

            using (client)
            {
                await DownloadAsync(uri, destinationPath, client, reporter, timeoutSeconds);
            }
        }

        public static async Task DownloadAsync(
            string uri,
            string destinationPath,
            HttpClient httpClient,
            IReporter reporter,
            int timeoutSeconds)
        {
            // Timeout if the response has not begun within 1 minute
            httpClient.Timeout = TimeSpan.FromMinutes(1);

            var destinationExists = File.Exists(destinationPath);
            var reachedCopy = false;
            try
            {
                using (var response = await httpClient.GetAsync(uri))
                {
                    response.EnsureSuccessStatusCode();

                    using (var responseStreamTask = response.Content.ReadAsStreamAsync())
                    {
                        var finished = await Task.WhenAny(
                            responseStreamTask,
                            Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));

                        if (!ReferenceEquals(responseStreamTask, finished))
                        {
                            throw new TimeoutException($"Download failed to complete in {timeoutSeconds} seconds.");
                        }

                        using (var responseStream = await responseStreamTask)
                        {
                            if (destinationExists)
                            {
                                // Check hashes before using the downloaded information.
                                var downloadHash = GetHash(responseStream);
                                responseStream.Position = 0L;

                                byte[] destinationHash;
                                using (var destinationStream = File.OpenRead(destinationPath))
                                {
                                    destinationHash = GetHash(destinationStream);
                                }

                                var sameHashes = downloadHash.Length == destinationHash.Length;
                                for (var i = 0; sameHashes && i < downloadHash.Length; i++)
                                {
                                    sameHashes = downloadHash[i] == destinationHash[i];
                                }

                                if (sameHashes)
                                {
                                    reporter.Output($"Not overwriting existing and matching file '{destinationPath}'.");
                                    return;
                                }
                            }
                            else
                            {
                                // May need to create directory to hold the file.
                                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                                if (!string.IsNullOrEmpty(destinationDirectory))
                                {
                                    Directory.CreateDirectory(destinationDirectory);
                                }
                            }

                            // Create or overwrite the destination file.
                            reachedCopy = true;
                            using (var outStream = File.Create(destinationPath))
                            {
                                await responseStream.CopyToAsync(outStream);

                                await outStream.FlushAsync();
                            }
                        }
                    }
                }
            }
            catch (HttpRequestException ex) when (destinationExists)
            {
                if (ex.InnerException is SocketException socketException)
                {
                    reporter.Warn($"Unable to download {uri}, socket error code '{socketException.SocketErrorCode}'.");
                }
                else
                {
                    reporter.Warn($"Unable to download {uri}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                reporter.Error($"Downloading '{uri}' failed.");
                reporter.Error(ex.ToString());
                if (reachedCopy)
                {
                    File.Delete(destinationPath);
                }
            }
        }

        private static byte[] GetHash(Stream stream)
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
    }
}
