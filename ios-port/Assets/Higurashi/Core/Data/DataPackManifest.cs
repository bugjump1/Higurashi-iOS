using System;

namespace Higurashi.IOS.Data
{
    [Serializable]
    public sealed class DataPackManifest
    {
        public int formatVersion;
        public string gameId;
        public string chapter;
        public string sourceEngine;
        public string modVersion;
        public string generatedUtc;
        public DataPackFileEntry[] files;
    }

    [Serializable]
    public sealed class DataPackFileEntry
    {
        public string path;
        public long size;
        public string sha256;
    }
}

