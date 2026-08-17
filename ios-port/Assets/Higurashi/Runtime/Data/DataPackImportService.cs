using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Higurashi.IOS.Data;
using UnityEngine;

namespace Higurashi.IOS.Runtime.Data
{
    public sealed class DataPackImportService
    {
        public static string DataPackFileName => HigurashiActiveChapter.Profile.DataPackFileName;
        public const string InstalledFolderName = "GameData";
        private const string ManifestEntryName = "manifest.json";
        private const int SupportedFormatVersion = 1;

        private readonly object _stateLock = new object();
        private string _status = "请选择数据包";
        private float _progress;
        private bool _isRunning;
        private string _currentFile = string.Empty;
        private int _currentFileIndex;
        private int _totalFiles;

        public string Status
        {
            get
            {
                lock (_stateLock)
                {
                    return _status;
                }
            }
        }

        public float Progress
        {
            get
            {
                lock (_stateLock)
                {
                    return _progress;
                }
            }
        }

        public bool IsRunning
        {
            get
            {
                lock (_stateLock)
                {
                    return _isRunning;
                }
            }
        }

        public string CurrentFile
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentFile;
                }
            }
        }

        public int CurrentFileIndex
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentFileIndex;
                }
            }
        }

        public int TotalFiles
        {
            get
            {
                lock (_stateLock)
                {
                    return _totalFiles;
                }
            }
        }

        public static string GetPackPath(string persistentDataPath)
        {
            return Path.Combine(persistentDataPath, DataPackFileName);
        }

        public static string GetIncomingPackPath(string persistentDataPath)
        {
            return Path.Combine(persistentDataPath, ".higurashi-data-pack.incoming.zip");
        }

        public static string GetInstallPath(string persistentDataPath)
        {
            return Path.Combine(persistentDataPath, InstalledFolderName);
        }

        public static bool IsInstalled(string persistentDataPath)
        {
            var manifestPath = Path.Combine(GetInstallPath(persistentDataPath), ManifestEntryName);
            return File.Exists(manifestPath);
        }

        public bool BeginImport(string persistentDataPath)
        {
            return BeginImport(persistentDataPath, GetPackPath(persistentDataPath), false);
        }

        public bool BeginSelectedImport(string persistentDataPath, string selectedPackPath)
        {
            return BeginImport(persistentDataPath, selectedPackPath, true);
        }

        public void SetWaitingStatus(string status)
        {
            lock (_stateLock)
            {
                if (!_isRunning)
                {
                    _status = status;
                    _progress = 0f;
                    _currentFile = string.Empty;
                    _currentFileIndex = 0;
                    _totalFiles = 0;
                }
            }
        }

        private bool BeginImport(
            string persistentDataPath,
            string packPath,
            bool deleteSourceWhenFinished)
        {
            lock (_stateLock)
            {
                if (_isRunning)
                {
                    return false;
                }

                _isRunning = true;
                _progress = 0;
                _status = "正在验证数据包…";
                _currentFile = string.Empty;
                _currentFileIndex = 0;
                _totalFiles = 0;
            }

            if (string.IsNullOrWhiteSpace(packPath) || !File.Exists(packPath))
            {
                SetFinished("未找到所选数据包，请重新选择。");
                return false;
            }

            var installPath = GetInstallPath(persistentDataPath);
            Task.Run(() => ImportWorker(packPath, installPath, deleteSourceWhenFinished));
            return true;
        }

        private void ImportWorker(
            string packPath,
            string installPath,
            bool deleteSourceWhenFinished)
        {
            var stagingPath = installPath + ".staging";
            var backupPath = installPath + ".backup";

            try
            {
                ValidateArchiveFingerprint(packPath);

                DataPackManifest manifest;
                string manifestJson;
                using (var archive = ZipFile.OpenRead(packPath))
                {
                    var manifestEntry = archive.GetEntry(ManifestEntryName)
                        ?? throw new InvalidDataException("数据包缺少 manifest.json。");
                    using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8, true))
                    {
                        manifestJson = reader.ReadToEnd();
                    }
                }

                manifest = JsonUtility.FromJson<DataPackManifest>(manifestJson);
                ValidateManifest(manifest);

                DeleteDirectoryIfPresent(stagingPath);
                Directory.CreateDirectory(stagingPath);

                using (var archive = ZipFile.OpenRead(packPath))
                {
                    var archiveEntries = BuildArchiveEntryMap(archive, manifest);
                    var totalBytes = GetTotalBytes(manifest);
                    long completedBytes = 0;

                    for (var i = 0; i < manifest.files.Length; i++)
                    {
                        var file = manifest.files[i];
                        var archivePath = "data/" + NormalizeArchivePath(file.path);
                        var archiveEntry = archiveEntries[archivePath];
                        var destination = SafePath.ResolveUnderRoot(stagingPath, file.path);
                        var parent = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            Directory.CreateDirectory(parent);
                        }

                        SetProgress(
                            totalBytes == 0 ? 0.1f : 0.1f + 0.9f * completedBytes / totalBytes,
                            "正在解压并校验… " + (i + 1) + " / " + manifest.files.Length,
                            file.path,
                            i + 1,
                            manifest.files.Length);

                        using (var source = archiveEntry.Open())
                        using (var target = new FileStream(
                                   destination,
                                   FileMode.CreateNew,
                                   FileAccess.Write,
                                   FileShare.None,
                                   1024 * 1024,
                                   FileOptions.SequentialScan))
                        {
                            source.CopyTo(target, 1024 * 1024);
                        }

                        var actualLength = new FileInfo(destination).Length;
                        if (actualLength != file.size)
                        {
                            throw new InvalidDataException("Size mismatch: " + file.path);
                        }

                        var actualHash = ComputeSha256(destination);
                        if (!string.Equals(actualHash, file.sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException("SHA-256 mismatch: " + file.path);
                        }

                        completedBytes += actualLength;
                    }
                }

                File.WriteAllText(
                    Path.Combine(stagingPath, ManifestEntryName),
                    manifestJson,
                    new UTF8Encoding(false));

                DeleteDirectoryIfPresent(backupPath);
                if (Directory.Exists(installPath))
                {
                    Directory.Move(installPath, backupPath);
                }

                try
                {
                    Directory.Move(stagingPath, installPath);
                    DeleteDirectoryIfPresent(backupPath);
                }
                catch
                {
                    if (!Directory.Exists(installPath) && Directory.Exists(backupPath))
                    {
                        Directory.Move(backupPath, installPath);
                    }

                    throw;
                }

                SetFinished("数据包导入成功，正在启动游戏…", 1f);
            }
            catch (Exception exception)
            {
                DeleteDirectoryIfPresent(stagingPath);
                SetFinished("导入失败：" + exception.Message);
            }
            finally
            {
                if (deleteSourceWhenFinished)
                {
                    DeleteFileIfPresent(packPath);
                }
            }
        }

        private void ValidateArchiveFingerprint(string packPath)
        {
            var profile = HigurashiActiveChapter.Profile;
            var fileInfo = new FileInfo(packPath);
            if (fileInfo.Length != profile.ExpectedDataPackSize)
            {
                throw new InvalidDataException(
                    "ZIP 文件大小不正确；请选择 " + profile.DataPackFileName + "。");
            }

            SetProgress(0.02f, "正在计算整个 ZIP 的 SHA-256…");
            var actualHash = ComputeSha256(packPath);
            if (!string.Equals(
                    actualHash,
                    profile.ExpectedDataPackSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "ZIP 的 SHA-256 不匹配，文件可能选错或已损坏。");
            }

            SetProgress(0.1f, "ZIP 校验通过，正在读取文件清单…");
        }

        private static Dictionary<string, ZipArchiveEntry> BuildArchiveEntryMap(
            ZipArchive archive,
            DataPackManifest manifest)
        {
            var expected = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < manifest.files.Length; i++)
            {
                var path = "data/" + NormalizeArchivePath(manifest.files[i].path);
                if (!expected.Add(path))
                {
                    throw new InvalidDataException("Duplicate manifest path: " + path);
                }
            }

            var result = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            for (var i = 0; i < archive.Entries.Count; i++)
            {
                var entry = archive.Entries[i];
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                var path = NormalizeArchivePath(entry.FullName);
                if (path == ManifestEntryName)
                {
                    continue;
                }

                if (!expected.Contains(path))
                {
                    throw new InvalidDataException("Unexpected data-pack entry: " + path);
                }

                if (!result.TryAdd(path, entry))
                {
                    throw new InvalidDataException("Duplicate archive entry: " + path);
                }
            }

            foreach (var path in expected)
            {
                if (!result.ContainsKey(path))
                {
                    throw new InvalidDataException("Missing data-pack entry: " + path);
                }
            }

            return result;
        }

        private static void ValidateManifest(DataPackManifest manifest)
        {
            if (manifest == null)
            {
                throw new InvalidDataException("Manifest JSON is invalid.");
            }

            if (manifest.formatVersion != SupportedFormatVersion)
            {
                throw new InvalidDataException("Unsupported data-pack format.");
            }

            var profile = HigurashiActiveChapter.Profile;
            if (!string.Equals(manifest.gameId, profile.GameId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "This data pack is not Higurashi chapter " + profile.EpisodeNumber + ".");
            }

            if (!string.Equals(manifest.chapter, profile.ChapterSlug, StringComparison.Ordinal))
            {
                throw new InvalidDataException("数据包章节标识不正确。");
            }

            if (manifest.files == null || manifest.files.Length == 0)
            {
                throw new InvalidDataException("The data-pack file list is empty.");
            }

            for (var i = 0; i < manifest.files.Length; i++)
            {
                var file = manifest.files[i];
                if (file == null || file.size < 0 ||
                    string.IsNullOrWhiteSpace(file.path) ||
                    string.IsNullOrWhiteSpace(file.sha256) ||
                    file.sha256.Length != 64)
                {
                    throw new InvalidDataException("Manifest contains an invalid file entry.");
                }
            }
        }

        private static long GetTotalBytes(DataPackManifest manifest)
        {
            long result = 0;
            for (var i = 0; i < manifest.files.Length; i++)
            {
                checked
                {
                    result += manifest.files[i].size;
                }
            }

            return result;
        }

        private static string NormalizeArchivePath(string path)
        {
            return path.Replace('\\', '/').TrimStart('/');
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       1024 * 1024,
                       FileOptions.SequentialScan))
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static void DeleteDirectoryIfPresent(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static void DeleteFileIfPresent(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unable to remove temporary data pack: " + exception.Message);
            }
        }

        private void SetProgress(
            float progress,
            string status,
            string currentFile = "",
            int currentFileIndex = 0,
            int totalFiles = 0)
        {
            lock (_stateLock)
            {
                _progress = progress;
                _status = status;
                _currentFile = currentFile ?? string.Empty;
                _currentFileIndex = Math.Max(0, currentFileIndex);
                _totalFiles = Math.Max(0, totalFiles);
            }
        }

        private void SetFinished(string status, float progress = 0)
        {
            lock (_stateLock)
            {
                _status = status;
                _progress = progress;
                _isRunning = false;
                _currentFile = string.Empty;
                if (progress >= 1f)
                {
                    _currentFileIndex = _totalFiles;
                }
            }
        }
    }
}
