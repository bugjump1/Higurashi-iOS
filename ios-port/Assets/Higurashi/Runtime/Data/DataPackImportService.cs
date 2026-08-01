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
        public const string DataPackFileName = "Higurashi-01-data.zip";
        public const string InstalledFolderName = "GameData";
        private const string ManifestEntryName = "manifest.json";
        private const int SupportedFormatVersion = 1;

        private readonly object _stateLock = new object();
        private string _status = "Waiting for data pack";
        private float _progress;
        private bool _isRunning;

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

        public static string GetPackPath(string persistentDataPath)
        {
            return Path.Combine(persistentDataPath, DataPackFileName);
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
            lock (_stateLock)
            {
                if (_isRunning)
                {
                    return false;
                }

                _isRunning = true;
                _progress = 0;
                _status = "Reading manifest";
            }

            var packPath = GetPackPath(persistentDataPath);
            if (!File.Exists(packPath))
            {
                SetFinished("Data pack not found in the app Files directory");
                return false;
            }

            DataPackManifest manifest;
            string manifestJson;
            try
            {
                using (var archive = ZipFile.OpenRead(packPath))
                {
                    var manifestEntry = archive.GetEntry(ManifestEntryName)
                        ?? throw new InvalidDataException("The data pack has no manifest.json.");
                    using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8, true))
                    {
                        manifestJson = reader.ReadToEnd();
                    }
                }

                manifest = JsonUtility.FromJson<DataPackManifest>(manifestJson);
                ValidateManifest(manifest);
            }
            catch (Exception exception)
            {
                SetFinished("Manifest error: " + exception.Message);
                return false;
            }

            var installPath = GetInstallPath(persistentDataPath);
            Task.Run(() => ImportWorker(packPath, installPath, manifest, manifestJson));
            return true;
        }

        private void ImportWorker(
            string packPath,
            string installPath,
            DataPackManifest manifest,
            string manifestJson)
        {
            var stagingPath = installPath + ".staging";
            var backupPath = installPath + ".backup";

            try
            {
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
                            totalBytes == 0 ? 0 : (float)completedBytes / totalBytes,
                            "Importing " + file.path);

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

                SetFinished("Data pack imported successfully", 1f);
            }
            catch (Exception exception)
            {
                DeleteDirectoryIfPresent(stagingPath);
                SetFinished("Import failed: " + exception.Message);
            }
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

            if (!string.Equals(manifest.gameId, "higurashi-01", StringComparison.Ordinal))
            {
                throw new InvalidDataException("This data pack is not Higurashi chapter 1.");
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
                    string.IsNullOrWhiteSpace(file.sha256))
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

        private void SetProgress(float progress, string status)
        {
            lock (_stateLock)
            {
                _progress = progress;
                _status = status;
            }
        }

        private void SetFinished(string status, float progress = 0)
        {
            lock (_stateLock)
            {
                _status = status;
                _progress = progress;
                _isRunning = false;
            }
        }
    }
}

