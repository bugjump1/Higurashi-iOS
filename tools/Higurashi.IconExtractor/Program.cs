using System.ComponentModel;
using System.Runtime.InteropServices;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Higurashi.IconExtractor <input.exe> <output.ico>");
    return 2;
}

var input = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
if (!File.Exists(input))
{
    Console.Error.WriteLine("Input executable was not found: " + input);
    return 2;
}

var iconData = IconResources.ExtractFirstGroup(input);
var outputDirectory = Path.GetDirectoryName(output);
if (!string.IsNullOrEmpty(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

File.WriteAllBytes(output, iconData.IcoBytes);

Console.WriteLine(
    $"Extracted {iconData.FrameCount} icon frame(s); largest={iconData.LargestWidth}x{iconData.LargestHeight}; output={output}");
return 0;

internal static class IconResources
{
    private const uint LoadLibraryAsDataFile = 0x00000002;
    private static readonly IntPtr RtIcon = new IntPtr(3);
    private static readonly IntPtr RtGroupIcon = new IntPtr(14);

    public static ExtractedIcon ExtractFirstGroup(string path)
    {
        var module = LoadLibraryEx(path, IntPtr.Zero, LoadLibraryAsDataFile);
        if (module == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to load executable resources.");
        }

        try
        {
            IntPtr groupName = IntPtr.Zero;
            EnumResNameProc callback = (_, _, name, _) =>
            {
                groupName = name;
                return false;
            };
            EnumResourceNames(module, RtGroupIcon, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            if (groupName == IntPtr.Zero)
            {
                throw new InvalidDataException("The executable has no RT_GROUP_ICON resource.");
            }

            var group = ReadResource(module, RtGroupIcon, groupName);
            using var groupReader = new BinaryReader(new MemoryStream(group, writable: false));
            var reserved = groupReader.ReadUInt16();
            var type = groupReader.ReadUInt16();
            var count = groupReader.ReadUInt16();
            if (reserved != 0 || type != 1 || count == 0)
            {
                throw new InvalidDataException("The RT_GROUP_ICON directory is invalid.");
            }

            var entries = new List<GroupEntry>(count);
            for (var i = 0; i < count; i++)
            {
                entries.Add(new GroupEntry(
                    groupReader.ReadByte(),
                    groupReader.ReadByte(),
                    groupReader.ReadByte(),
                    groupReader.ReadByte(),
                    groupReader.ReadUInt16(),
                    groupReader.ReadUInt16(),
                    groupReader.ReadUInt32(),
                    groupReader.ReadUInt16()));
            }

            var images = new List<byte[]>(count);
            foreach (var entry in entries)
            {
                images.Add(ReadResource(module, RtIcon, new IntPtr(entry.ResourceId)));
            }

            using var output = new MemoryStream();
            using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write(count);
                var offset = 6 + count * 16;
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    writer.Write(entry.WidthByte);
                    writer.Write(entry.HeightByte);
                    writer.Write(entry.ColorCount);
                    writer.Write(entry.Reserved);
                    writer.Write(entry.Planes);
                    writer.Write(entry.BitCount);
                    writer.Write((uint)images[i].Length);
                    writer.Write((uint)offset);
                    offset += images[i].Length;
                }
                foreach (var image in images)
                {
                    writer.Write(image);
                }
            }

            var largest = entries.MaxBy(entry => entry.Width * entry.Height)
                ?? throw new InvalidDataException("No icon frame was extracted.");
            return new ExtractedIcon(output.ToArray(), count, largest.Width, largest.Height);
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    private static byte[] ReadResource(IntPtr module, IntPtr type, IntPtr name)
    {
        var resource = FindResource(module, name, type);
        if (resource == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to find icon resource.");
        }
        var size = SizeofResource(module, resource);
        var loaded = LoadResource(module, resource);
        var pointer = LockResource(loaded);
        if (size == 0 || pointer == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to read icon resource.");
        }
        var length = checked((int)size);
        var result = new byte[length];
        Marshal.Copy(pointer, result, 0, length);
        return result;
    }

    private sealed record GroupEntry(
        byte WidthByte,
        byte HeightByte,
        byte ColorCount,
        byte Reserved,
        ushort Planes,
        ushort BitCount,
        uint BytesInResource,
        ushort ResourceId)
    {
        public int Width => WidthByte == 0 ? 256 : WidthByte;
        public int Height => HeightByte == 0 ? 256 : HeightByte;
    }

    internal sealed record ExtractedIcon(byte[] IcoBytes, int FrameCount, int LargestWidth, int LargestHeight);

    private delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr parameter);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumResourceNames(
        IntPtr module,
        IntPtr type,
        EnumResNameProc callback,
        IntPtr parameter);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr resourceData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr module, IntPtr resource);
}
