using System;
using System.IO;
using System.Text;
using Higurashi.IOS.Runtime;
using UnityEngine;

namespace Higurashi.IOS.Runtime.Diagnostics
{
    internal static class HigurashiDiagnosticLog
    {
        private const long MaximumLogBytes = 1024 * 1024;
        private const long TrimmedLogBytes = 768 * 1024;
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;

        public static string LogPath => _logPath ?? string.Empty;

        public static void Initialize(string persistentDataPath)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                // Application.persistentDataPath is Documents on iOS.
                var directory = Path.Combine(persistentDataPath, "logs");
                Directory.CreateDirectory(directory);
                _logPath = Path.Combine(directory, "higurashi-system.log");
                Application.logMessageReceived += OnUnityLog;
                _initialized = true;
                Info("Session", "Diagnostic logging started");
            }
            catch
            {
                _logPath = string.Empty;
            }
        }

        public static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }
            Info("Session", "Diagnostic logging stopped");
            Application.logMessageReceived -= OnUnityLog;
            _initialized = false;
        }

        public static void Info(string category, string message)
        {
            Append("INFO", category, message, string.Empty);
        }

        public static void Warning(string category, string message)
        {
            Append("WARN", category, message, string.Empty);
        }

        public static void Error(string category, string message, Exception exception = null)
        {
            Append("ERROR", category, message,
                exception == null ? string.Empty : exception.ToString());
        }

        public static string CreateExport(string persistentDataPath, string header)
        {
            var directory = Path.Combine(persistentDataPath, "logs");
            Directory.CreateDirectory(directory);
            var episode = HigurashiActiveChapter.Profile.EpisodeCode;
            var name = "Higurashi-EP" + episode + "-diagnostic-" +
                       DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            var outputPath = Path.Combine(directory, name);

            lock (Sync)
            {
                var body = File.Exists(_logPath) ? File.ReadAllText(_logPath, Encoding.UTF8) : string.Empty;
                File.WriteAllText(outputPath,
                    (header ?? string.Empty) + Environment.NewLine +
                    "================ RECENT SYSTEM LOG ================" + Environment.NewLine +
                    body,
                    new UTF8Encoding(false));
            }
            Info("Export", "Diagnostic log prepared: " + name);
            return outputPath;
        }

        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log)
            {
                return;
            }
            var level = type == LogType.Warning ? "WARN" : "ERROR";
            Append(level, "Unity", condition,
                type == LogType.Warning ? string.Empty : stackTrace);
        }

        private static void Append(string level, string category, string message, string stackTrace)
        {
            if (string.IsNullOrEmpty(_logPath))
            {
                return;
            }

            try
            {
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] [" +
                           Sanitize(category) + "] " + Sanitize(message);
                if (!string.IsNullOrWhiteSpace(stackTrace))
                {
                    line += Environment.NewLine + Sanitize(stackTrace);
                }
                line += Environment.NewLine;

                lock (Sync)
                {
                    File.AppendAllText(_logPath, line, new UTF8Encoding(false));
                    TrimIfNeeded();
                }
            }
            catch
            {
                // Diagnostics must never interrupt gameplay.
            }
        }

        private static void TrimIfNeeded()
        {
            var file = new FileInfo(_logPath);
            if (!file.Exists || file.Length <= MaximumLogBytes)
            {
                return;
            }

            byte[] tail;
            using (var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var length = (int)Math.Min(TrimmedLogBytes, stream.Length);
                tail = new byte[length];
                stream.Position = stream.Length - length;
                var read = stream.Read(tail, 0, length);
                if (read != length)
                {
                    Array.Resize(ref tail, read);
                }
            }
            var text = Encoding.UTF8.GetString(tail);
            var firstLine = text.IndexOf('\n');
            if (firstLine >= 0 && firstLine + 1 < text.Length)
            {
                text = text.Substring(firstLine + 1);
            }
            File.WriteAllText(_logPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                " [INFO] [Log] Older entries were trimmed." + Environment.NewLine + text,
                new UTF8Encoding(false));
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            var sanitized = value.Replace('\0', ' ');
            if (!string.IsNullOrEmpty(Application.persistentDataPath))
            {
                sanitized = sanitized.Replace(Application.persistentDataPath, "<app-data>");
            }
            if (!string.IsNullOrEmpty(Application.streamingAssetsPath))
            {
                sanitized = sanitized.Replace(Application.streamingAssetsPath, "<streaming-assets>");
            }
            return sanitized;
        }
    }
}
