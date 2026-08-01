using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Higurashi.IOS.Buriko
{
    /// <summary>
    /// Parses and validates the MGSC container used by compiled Buriko scripts.
    /// Operation execution is implemented separately.
    /// </summary>
    public sealed class CompiledScriptContainer
    {
        private const int SupportedVersion = 1;
        private const int MaximumLookupEntries = 1_000_000;

        private CompiledScriptContainer(
            Dictionary<string, int> blocks,
            int[] lineOffsets,
            byte[] data)
        {
            Blocks = blocks;
            LineOffsets = lineOffsets;
            Data = data;
        }

        public IReadOnlyDictionary<string, int> Blocks { get; }
        public IReadOnlyList<int> LineOffsets { get; }
        public byte[] Data { get; }

        public static CompiledScriptContainer Read(Stream stream)
        {
            if (stream == null || !stream.CanRead)
            {
                throw new ArgumentException("A readable script stream is required.", nameof(stream));
            }

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var magic = Encoding.ASCII.GetString(ReadExactly(reader, 4));
                if (magic != "MGSC")
                {
                    throw new InvalidDataException("Script is not an MGSC container.");
                }

                var version = reader.ReadInt32();
                if (version != SupportedVersion)
                {
                    throw new InvalidDataException("Unsupported MGSC version: " + version);
                }

                var blockCount = ReadSafeCount(reader, "block");
                var lineCount = ReadSafeCount(reader, "line");
                var dataLength = reader.ReadInt32();
                if (dataLength < 0)
                {
                    throw new InvalidDataException("MGSC data length is negative.");
                }

                var blocks = new Dictionary<string, int>(blockCount, StringComparer.Ordinal);
                for (var i = 0; i < blockCount; i++)
                {
                    var name = reader.ReadString();
                    var offset = reader.ReadInt32();
                    if (!blocks.TryAdd(name, offset))
                    {
                        throw new InvalidDataException("Duplicate MGSC block: " + name);
                    }
                }

                var lineOffsets = new int[lineCount];
                for (var i = 0; i < lineCount; i++)
                {
                    lineOffsets[i] = reader.ReadInt32();
                }

                var data = ReadExactly(reader, dataLength);
                ValidateOffsets(blocks.Values, lineOffsets, data.Length);
                return new CompiledScriptContainer(blocks, lineOffsets, data);
            }
        }

        public static CompiledScriptContainer ReadFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(stream);
            }
        }

        private static int ReadSafeCount(BinaryReader reader, string description)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaximumLookupEntries)
            {
                throw new InvalidDataException("Invalid MGSC " + description + " count: " + count);
            }

            return count;
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            var bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException("MGSC container ended unexpectedly.");
            }

            return bytes;
        }

        private static void ValidateOffsets(
            IEnumerable<int> blockOffsets,
            IReadOnlyList<int> lineOffsets,
            int dataLength)
        {
            foreach (var offset in blockOffsets)
            {
                ValidateOffset(offset, dataLength, "block");
            }

            for (var i = 0; i < lineOffsets.Count; i++)
            {
                ValidateOffset(lineOffsets[i], dataLength, "line");
            }
        }

        private static void ValidateOffset(int offset, int dataLength, string description)
        {
            if (offset < 0 || offset > dataLength)
            {
                throw new InvalidDataException(
                    "MGSC " + description + " offset is outside the data segment: " + offset);
            }
        }
    }
}
