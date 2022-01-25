using Newtonsoft.Json.Linq;
using ReactNative.Bridge;
using ReactNative.Modules.Core;
using ReactNative.Modules.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace RNFS
{
    [ReactModule]
    class ReactNativeModule
    {
        private const int FileType = 0;
        private const int DirectoryType = 1;

        private static readonly IReadOnlyDictionary<string, Func<HashAlgorithm>> s_hashAlgorithms =
            new Dictionary<string, Func<HashAlgorithm>>
            {
                { "md5", () => MD5.Create() },
                { "sha1", () => SHA1.Create() },
                { "sha256", () => SHA256.Create() },
                { "sha384", () => SHA384.Create() },
                { "sha512", () => SHA512.Create() },
            };

        private readonly TaskCancellationManager<int> _tasks = new TaskCancellationManager<int>();
        private readonly HttpClient _httpClient = new HttpClient();

        [Obsolete]
        public override IReadOnlyDictionary<string, object> Constants
        {
            get
            {
                var constants = new Dictionary<string, object>
                {
                    { "RNFSMainBundlePath", Package.Current.InstalledLocation.Path },
                    { "RNFSCachesDirectoryPath", ApplicationData.Current.LocalCacheFolder.Path },
                    { "RNFSRoamingDirectoryPath", ApplicationData.Current.RoamingFolder.Path },
                    { "RNFSDocumentDirectoryPath", ApplicationData.Current.LocalFolder.Path },
                    { "RNFSTemporaryDirectoryPath", ApplicationData.Current.TemporaryFolder.Path },
                    { "RNFSFileTypeRegular", 0 },
                    { "RNFSFileTypeDirectory", 1 },
                };

                var external = GetFolderPathSafe(() => KnownFolders.RemovableDevices);
                if (external != null)
                {
                    var externalItems = KnownFolders.RemovableDevices.GetItemsAsync().AsTask().Result;
                    if (externalItems.Count > 0)
                    {
                        constants.Add("RNFSExternalDirectoryPath", externalItems[0].Path);
                    }
                    constants.Add("RNFSExternalDirectoryPaths", externalItems.Select(i => i.Path).ToArray());
                }

                var pictures = GetFolderPathSafe(() => KnownFolders.PicturesLibrary);
                if (pictures != null)
                {
                    constants.Add("RNFSPicturesDirectoryPath", pictures);
                }
                
                return constants;
            }
        }

        // TODO: fix error throwing stuff from IPromise
        [ReactMethod]
        public async void writeFile(string filepath, string base64Content, JObject options)
        {
            try
            {
                // TODO: open file on background thread?
                using (var file = File.OpenWrite(filepath))
                {
                    var data = Convert.FromBase64String(base64Content);
                    await file.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                }

                
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async void appendFile(string filepath, string base64Content)
        {
            try
            {
                // TODO: open file on background thread?
                using (var file = File.Open(filepath, FileMode.Append))
                {
                    var data = Convert.FromBase64String(base64Content);
                    await file.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                }

                
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async void write(string filepath, string base64Content, int position)
        {
            try
            {
                // TODO: open file on background thread?
                using (var file = File.OpenWrite(filepath))
                {
                    if (position >= 0)
                    {
                        file.Position = position;
                    }

                    var data = Convert.FromBase64String(base64Content);
                    await file.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                }

                
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public bool exists(string filepath)
        {
            try
            {
                return File.Exists(filepath) || Directory.Exists(filepath);
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async string readFile(string filepath)
        {
            try
            {
                if (!File.Exists(filepath))
                {
                    throw new FileNotFoundException("Could not find " + filepath);
                }

                // TODO: open file on background thread?
                string base64Content;
                using (var file = File.OpenRead(filepath))
                {
                    var length = (int)file.Length;
                    var buffer = new byte[length];
                    await file.ReadAsync(buffer, 0, length).ConfigureAwait(false);
                    base64Content = Convert.ToBase64String(buffer);
                }

                return base64Content;
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath); 
            }
        }

        [ReactMethod]
        public async string read(string filepath, int length, int position)
        {
            try
            {
                if (!File.Exists(filepath))
                {
                    throw new FileNotFoundException("Could not find " + filepath);
                }

                // TODO: open file on background thread?
                string base64Content;
                using (var file = File.OpenRead(filepath))
                {
                    file.Position = position;
                    var buffer = new byte[length];
                    await file.ReadAsync(buffer, 0, length).ConfigureAwait(false);
                    base64Content = Convert.ToBase64String(buffer);
                }

                return base64Content;
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async string hash(string filepath, string algorithm)
        {
            var hashAlgorithmFactory = default(Func<HashAlgorithm>);
            if (!s_hashAlgorithms.TryGetValue(algorithm, out hashAlgorithmFactory))
            {
                throw new Exception("Invalid hash algorithm.");
            }

            try
            {
                if (!File.Exists(filepath))
                {
                    throw new FileNotFoundException("Could not find " + filepath);
                }

                await Task.Run(() =>
                {
                    var hexBuilder = new StringBuilder();
                    using (var hashAlgorithm = hashAlgorithmFactory())
                    {
                        hashAlgorithm.Initialize();
                        var hash = default(byte[]);
                        using (var file = File.OpenRead(filepath))
                        {
                            hash = hashAlgorithm.ComputeHash(file);
                        }

                        foreach (var b in hash)
                        {
                            hexBuilder.Append(string.Format("{0:x2}", b));
                        }
                    }

                    return hexBuilder.ToString();
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public bool moveFile(string filepath, string destPath, JObject options)
        {
            try
            {
                // TODO: move file on background thread?
                File.Move(filepath, destPath);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async void copyFile(string filepath, string destPath, JObject options)
        {
            try
            {
                await Task.Run(() => File.Copy(filepath, destPath)).ConfigureAwait(false);
                

            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async JArray readDir(string directory)
        {
            try
            {
                await Task.Run(() =>
                {
                    var info = new DirectoryInfo(directory);
                    if (!info.Exists(filepath))
                    {
                        throw new FileNotFoundException("Could not find " + directory);
                    }

                    var fileMaps = new JArray();
                    foreach (var item in info.EnumerateFileSystemInfos())
                    {
                        var fileMap = new JObject
                        {
                            { "mtime", ConvertToUnixTimestamp(item.LastWriteTime) },
                            { "name", item.Name },
                            { "path", item.FullName },
                        };

                        var fileItem = item as FileInfo;
                        if (fileItem != null)
                        {
                            fileMap.Add("type", FileType);
                            fileMap.Add("size", fileItem.Length);
                        }
                        else
                        {
                            fileMap.Add("type", DirectoryType);
                            fileMap.Add("size", 0);
                        }

                        fileMaps.Add(fileMap);
                    }

                    return fileMaps;
                });
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.ToString() + "when working with directory " +  directory);
            }
        }

        [ReactMethod]
        public JObject stat(string filepath)
        {
            try
            {
                FileSystemInfo fileSystemInfo = new FileInfo(filepath);
                if (!fileSystemInfo.Exists)
                {
                    fileSystemInfo = new DirectoryInfo(filepath);
                    if (!fileSystemInfoExists())
                    {
                        throw new FileNotFoundException("Could not find " + filepath);
                    }
                }

                var fileInfo = fileSystemInfo as FileInfo;
                var statMap = new JObject
                {
                    { "ctime", ConvertToUnixTimestamp(fileSystemInfo.CreationTime) },
                    { "mtime", ConvertToUnixTimestamp(fileSystemInfo.LastWriteTime) },
                    { "size", fileInfo?.Length ?? 0 },
                    { "type", fileInfo != null ? FileType: DirectoryType },
                };

                return statMap;
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async void unlink(string filepath)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(filepath);
                var fileInfo = default(FileInfo);
                if (directoryInfo.Exists)
                {
                    await Task.Run(() => Directory.Delete(filepath, true)).ConfigureAwait(false);
                }
                else if ((fileInfo = new FileInfo(filepath)).Exists)
                {
                    await Task.Run(() => File.Delete(filepath)).ConfigureAwait(false);
                }
                else
                {
                    throw new FileNotFoundException("Could not find " + filepath);
                }

                
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async void mkdir(string filepath, JObject options)
        {
            try
            {
                await Task.Run(() => Directory.CreateDirectory(filepath)).ConfigureAwait(false);
                
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async void downloadFile(JObject options)
        {
            var filepath = options.Value<string>("toFile");

            try
            {
                var url = new Uri(options.Value<string>("fromUrl"));
                var jobId = options.Value<int>("jobId");
                var headers = (JObject)options["headers"];
                var progressDivider = options.Value<int>("progressDivider");

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value.Value<string>());
                }

                await _tasks.AddAndInvokeAsync(jobId, token => 
                    ProcessRequestAsync(promise, request, filepath, jobId, progressDivider, token));
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public void stopDownload(int jobId)
        {
            _tasks.Cancel(jobId);
        }

        [ReactMethod]
        public async JObject getFSInfo()
        {
            try
            {
                var properties = await ApplicationData.Current.LocalFolder.Properties.RetrievePropertiesAsync(
                    new[] 
                    {
                        "System.FreeSpace",
                        "System.Capacity",
                    })
                    .AsTask()
                    .ConfigureAwait(false);

                return new JObject
                {
                    { "freeSpace", (ulong)properties["System.FreeSpace"] },
                    { "totalSpace", (ulong)properties["System.Capacity"] },
                });
            }
            catch (Exception)
            {
                promise.Reject(null, "getFSInfo is not available");
            }
        }

        [ReactMethod]
        public async string touch(string filepath, double mtime, double ctime)
        {
            try
            {
                await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filepath);
                    if (!fileInfo.Exists)
                    {
                        using (File.Create(filepath)) { }
                    }

                    fileInfo.CreationTimeUtc = ConvertFromUnixTimestamp(ctime);
                    fileInfo.LastWriteTimeUtc = ConvertFromUnixTimestamp(mtime);

                    return fileInfo.FullName;
                });
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.toString() + " for filepath " + filepath);
            }
        }

        public override void OnReactInstanceDispose()
        {
            _tasks.CancelAllTasks();
            _httpClient.Dispose();
        }

        private async JObject ProcessRequestAsync(IPromise promise, HttpRequestMessage request, string filepath, int jobId, int progressIncrement, CancellationToken token)
        {
            try
            {
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    var headersMap = new JObject();
                    foreach (var header in response.Headers)
                    {
                        headersMap.Add(header.Key, string.Join(",", header.Value));
                    }

                    var contentLength = response.Content.Headers.ContentLength;
                    /*
                    // Commented out because we have disabled event sending
                    SendEvent($"DownloadBegin-{jobId}", new JObject
                    {
                        { "jobId", jobId },
                        { "statusCode", (int)response.StatusCode },
                        { "contentLength", contentLength },
                        { "headers", headersMap },
                    });
                    */
                    // TODO: open file on background thread?
                    long totalRead = 0;
                    using (var fileStream = File.OpenWrite(filepath))
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        var contentLengthForProgress = contentLength ?? -1;
                        var nextProgressIncrement = progressIncrement;
                        var buffer = new byte[8 * 1024];
                        var read = 0;
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            token.ThrowIfCancellationRequested();

                            await fileStream.WriteAsync(buffer, 0, read);
                            if (contentLengthForProgress >= 0)
                            {
                                totalRead += read;
                                if (totalRead * 100 / contentLengthForProgress >= nextProgressIncrement ||
                                    totalRead == contentLengthForProgress)
                                {/*
                                  // Commented out because we have disbaled event sending, hopefully this doesn't break anything
                                    SendEvent("DownloadProgress-" + jobId, new JObject
                                    {
                                        { "jobId", jobId },
                                        { "contentLength", contentLength },
                                        { "bytesWritten", totalRead },
                                    });
                                    */
                                    nextProgressIncrement += progressIncrement;
                                }
                            }
                        }
                    }

                    return new JObject
                    {
                        { "jobId", jobId },
                        { "statusCode", (int)response.StatusCode },
                        { "bytesWritten", totalRead },
                    };
                }
            }
            catch (OperationCanceledException ex)
            {
                throw ex;
            }
            finally
            {
                request.Dispose();
            }
        }
        /*
        private void SendEvent(string eventName, JObject eventData)
        {
            Emitter.emit(eventName, eventData);
        }*/

        private static string GetFolderPathSafe(Func<StorageFolder> getFolder)
        {
            try
            {
                return getFolder().Path;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static double ConvertToUnixTimestamp(DateTime date)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            var diff = date.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalSeconds);
        }

        public static DateTime ConvertFromUnixTimestamp(double timestamp)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            var diff = TimeSpan.FromSeconds(timestamp);
            var dateTimeUtc = origin + diff;
            return dateTimeUtc.ToLocalTime();
        }
    }
}
