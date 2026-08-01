using System;
using System.Collections.Generic;
using System.IO;

namespace Higurashi.IOS.Buriko
{
    public sealed class DirectoryBurikoScriptRepository : IBurikoScriptRepository
    {
        private readonly string[] _directories;
        private readonly Dictionary<string, CompiledScriptContainer> _cache =
            new Dictionary<string, CompiledScriptContainer>(StringComparer.OrdinalIgnoreCase);

        public DirectoryBurikoScriptRepository(params string[] directories)
        {
            if (directories == null || directories.Length == 0)
            {
                throw new ArgumentException("At least one script directory is required.", nameof(directories));
            }

            _directories = new string[directories.Length];
            for (var i = 0; i < directories.Length; i++)
            {
                _directories[i] = Path.GetFullPath(directories[i]);
            }
        }

        public CompiledScriptContainer Load(string scriptName)
        {
            var safeName = Path.GetFileNameWithoutExtension(scriptName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(safeName) || safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException("Invalid Buriko script name: " + scriptName);
            }

            if (_cache.TryGetValue(safeName, out var cached))
            {
                return cached;
            }

            for (var i = 0; i < _directories.Length; i++)
            {
                var path = Path.Combine(_directories[i], safeName + ".mg");
                if (!File.Exists(path))
                {
                    continue;
                }

                var script = CompiledScriptContainer.ReadFile(path);
                _cache.Add(safeName, script);
                return script;
            }

            throw new FileNotFoundException("Buriko script was not found: " + safeName + ".mg");
        }
    }
}

