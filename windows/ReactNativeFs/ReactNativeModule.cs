using Microsoft.ReactNative.Managed;
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
    [ReactModule("ReactNativeFS")]
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

        private readonly HttpClient _httpClient = new HttpClient();

        // TODO: fix error throwing stuff from IPromise
        [ReactMethod]
        public async void writeFile(string filepath, string base64Content, Object options)
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
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
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
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
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
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
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
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async Task<string> readFile(string filepath)
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
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async Task<string> read(string filepath, int length, int position)
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
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }
        }

        [ReactMethod("hash")]
        public async Task<string> Hash(string filepath, string algorithm)
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

                return await Task.Run(() =>
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
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public bool moveFile(string filepath, string destPath, Object options)
        {
            try
            {
                // TODO: move file on background thread?
                File.Move(filepath, destPath);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async void copyFile(string filepath, string destPath, Object options)
        {
            try
            {
                await Task.Run(() => File.Copy(filepath, destPath)).ConfigureAwait(false);


            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async Task<Array> readDir(string directory)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var info = new DirectoryInfo(directory);
                    if (!info.Exists)
                    {
                        throw new FileNotFoundException("Could not find " + directory);
                    }

                    var fileMaps = new List<Object>();
                    foreach (var item in info.EnumerateFileSystemInfos())
                    {
                        

                        var fileItem = item as FileInfo;
                        if (fileItem != null)
                        {
                            var fileMap = new
                            {
                                mtime = ConvertToUnixTimestamp(item.LastWriteTime),
                                name = item.Name,
                                path = item.FullName,
                                type = FileType,
                                size = fileItem.Length
                            };
                            fileMaps.Add(fileMap);
                        }
                        else
                        {
                            var fileMap = new
                            {
                                mtime = ConvertToUnixTimestamp(item.LastWriteTime),
                                name = item.Name,
                                path = item.FullName,
                                type = DirectoryType,
                                size = 0
                            };
                            fileMaps.Add(fileMap);
                        }
                    }
                    return fileMaps.ToArray();
                });
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.ToString() + "when working with directory " + directory);
            }
        }

        [ReactMethod]
        public Object stat(string filepath)
        {
            try
            {
                FileSystemInfo fileSystemInfo = new FileInfo(filepath);
                if (!fileSystemInfo.Exists)
                {
                    fileSystemInfo = new DirectoryInfo(filepath);
                    if (!fileSystemInfo.Exists)
                    {
                        throw new FileNotFoundException("Could not find " + filepath);
                    }
                }

                var fileInfo = fileSystemInfo as FileInfo;
                var statMap = new
                {
                    ctime = ConvertToUnixTimestamp(fileSystemInfo.CreationTime),
                    mtime = ConvertToUnixTimestamp(fileSystemInfo.LastWriteTime),
                    type = fileInfo != null ? FileType : DirectoryType,
                    size = fileInfo?.Length ?? 0
                };
                return statMap;
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
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
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }
        }

        [ReactMethod]
        public async void mkdir(string filepath, Object options)
        {
            try
            {
                await Task.Run(() => Directory.CreateDirectory(filepath)).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }
        }

        // OBSELETE
        [ReactMethod]
        public async void downloadFile(Object options)
        {
           /* var filepath = options.Value<string>("toFile");

            try
            {
                var url = new Uri(options.Value<string>("fromUrl"));
                var jobId = options.Value<int>("jobId");
                var headers = (Object)options["headers"];
                var progressDivider = options.Value<int>("progressDivider");

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value.Value<string>());
                }
            }
            catch (Exception ex)
            {
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }*/
        }
        // OBSELETE
        [ReactMethod]
        public void stopDownload(int jobId)
        {
            //_tasks.Cancel(jobId);
        }

        [ReactMethod]
        public async Task<Object> getFSInfo()
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

                return new { 
                    freeSpace =  (ulong)properties["System.FreeSpace"],
                    totalSpace = (ulong)properties["System.Capacity"]
                };
            }
            catch (Exception)
            {
                throw new Exception("getFSInfo is not available");
            }
        }

        [ReactMethod]
        public async Task<string> touch(string filepath, double mtime, double ctime)
        {
            try
            {
                return await Task.Run(() =>
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
                throw new Exception("got exception " + ex.ToString() + " for filepath " + filepath);
            }
        }

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
