using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Higurashi.IOS.Data;

internal static class Program
{
    private const string ExpectedDataDirectoryName = "HigurashiEp01_Data";

    public static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: Higurashi.DataPack <game-directory> <output-zip>");
            return 2;
        }

        try
        {
            CreatePack(args[0], args[1]);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Data-pack creation failed: " + exception.Message);
            return 1;
        }
    }

    private static void CreatePack(string sourceArgument, string outputArgument)
    {
        var source = Path.GetFullPath(sourceArgument);
        var dataRoot = string.Equals(
                Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar)),
                ExpectedDataDirectoryName,
                StringComparison.OrdinalIgnoreCase)
            ? source
            : Path.Combine(source, ExpectedDataDirectoryName);

        if (!Directory.Exists(dataRoot))
        {
            throw new DirectoryNotFoundException("HigurashiEp01_Data was not found under the source directory.");
        }

        var streamingAssets = Path.Combine(dataRoot, "StreamingAssets");
        if (!Directory.Exists(streamingAssets))
        {
            throw new DirectoryNotFoundException("StreamingAssets was not found.");
        }

        var outputPath = Path.GetFullPath(outputArgument);
        if (!string.Equals(Path.GetExtension(outputPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The output file must use the .zip extension.");
        }

        if (IsUnderRoot(dataRoot, outputPath))
        {
            throw new InvalidOperationException("The output data pack cannot be written inside HigurashiEp01_Data.");
        }

        var files = EnumeratePackFiles(dataRoot, streamingAssets);
        CheckCaseInsensitiveCollisions(files);

        Console.WriteLine($"Hashing {files.Count} files...");
        var manifestFiles = new List<DataPackFileEntry>(files.Count);
        long totalBytes = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var info = new FileInfo(file.FullPath);
            totalBytes = checked(totalBytes + info.Length);
            manifestFiles.Add(new DataPackFileEntry
            {
                path = file.RelativePath,
                size = info.Length,
                sha256 = ComputeSha256(file.FullPath)
            });

            if ((i + 1) % 250 == 0 || i + 1 == files.Count)
            {
                Console.WriteLine($"  hashed {i + 1}/{files.Count}");
            }
        }

        EnsureOutputSpace(outputPath, totalBytes);
        var manifest = new DataPackManifest
        {
            formatVersion = 1,
            gameId = "higurashi-01",
            chapter = "onikakushi",
            sourceEngine = "Unity 5.2.2f1 / Mono",
            modVersion = ReadModVersion(dataRoot),
            generatedUtc = DateTimeOffset.UtcNow.ToString("O"),
            files = manifestFiles.ToArray()
        };

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var temporaryPath = outputPath + ".tmp";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        try
        {
            Console.WriteLine($"Writing {FormatBytes(totalBytes)} to {outputPath}");
            using (var output = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.None,
                       1024 * 1024,
                       FileOptions.SequentialScan))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, false, Encoding.UTF8))
            {
                for (var i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var entry = archive.CreateEntry(
                        "data/" + file.RelativePath,
                        ChooseCompression(file.RelativePath));
                    entry.LastWriteTime = new DateTimeOffset(File.GetLastWriteTimeUtc(file.FullPath), TimeSpan.Zero);
                    using var sourceStream = new FileStream(
                        file.FullPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        1024 * 1024,
                        FileOptions.SequentialScan);
                    using var targetStream = entry.Open();
                    sourceStream.CopyTo(targetStream, 1024 * 1024);

                    if ((i + 1) % 250 == 0 || i + 1 == files.Count)
                    {
                        Console.WriteLine($"  packed {i + 1}/{files.Count}");
                    }
                }

                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using var manifestStream = manifestEntry.Open();
                JsonSerializer.Serialize(
                    manifestStream,
                    manifest,
                    new JsonSerializerOptions
                    {
                        IncludeFields = true,
                        WriteIndented = true
                    });
            }

            File.Move(temporaryPath, outputPath, true);
            Console.WriteLine("Data pack created successfully.");
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static List<PackFile> EnumeratePackFiles(string dataRoot, string streamingAssets)
    {
        var files = Directory
            .EnumerateFiles(streamingAssets, "*", SearchOption.AllDirectories)
            .Select(path => new PackFile(
                path,
                NormalizePath(Path.GetRelativePath(dataRoot, path))))
            .ToList();

        var tipsPath = Path.Combine(dataRoot, "tips.json");
        if (File.Exists(tipsPath))
        {
            files.Add(new PackFile(tipsPath, "tips.json"));
        }

        files.Sort((left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));
        return files;
    }

    private static void CheckCaseInsensitiveCollisions(IEnumerable<PackFile> files)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (!seen.Add(file.RelativePath))
            {
                throw new InvalidDataException("Case-insensitive path collision: " + file.RelativePath);
            }
        }
    }

    private static string ReadModVersion(string dataRoot)
    {
        var versionPath = Path.Combine(dataRoot, "Managed", "Assembly-CSharp.version.txt");
        if (!File.Exists(versionPath))
        {
            return "unknown";
        }

        return File.ReadLines(versionPath).FirstOrDefault()?.Trim() ?? "unknown";
    }

    private static CompressionLevel ChooseCompression(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ogg":
            case ".png":
            case ".mp4":
            case ".ogv":
            case ".mg":
                return CompressionLevel.NoCompression;
            default:
                return CompressionLevel.Optimal;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.SequentialScan);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    private static bool IsUnderRoot(string root, string candidate)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return candidate.StartsWith(fullRoot, comparison);
    }

    private static void EnsureOutputSpace(string outputPath, long sourceBytes)
    {
        var root = Path.GetPathRoot(outputPath);
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        var drive = new DriveInfo(root);
        var required = checked(sourceBytes + 256L * 1024 * 1024);
        if (drive.AvailableFreeSpace < required)
        {
            throw new IOException(
                $"Not enough free space. Required approximately {FormatBytes(required)}, " +
                $"available {FormatBytes(drive.AvailableFreeSpace)}.");
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string FormatBytes(long bytes)
    {
        return $"{bytes / 1024d / 1024d / 1024d:0.00} GiB";
    }

    private sealed class PackFile
    {
        public PackFile(string fullPath, string relativePath)
        {
            FullPath = fullPath;
            RelativePath = relativePath;
        }

        public string FullPath { get; }
        public string RelativePath { get; }
    }
}

