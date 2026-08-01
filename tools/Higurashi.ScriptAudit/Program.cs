using System.Text;
using System.Text.RegularExpressions;
using Higurashi.IOS.Buriko;

internal static class Program
{
    private static int Main(string[] args)
    {
        var parsed = Arguments.Parse(args);
        if (parsed.ScriptPaths.Count == 0)
        {
            Console.Error.WriteLine(
                "Usage: Higurashi.ScriptAudit [--reference-dir <higurashi-assembly>] " +
                "<script-folder> [script-folder...]");
            return 2;
        }

        var paths = parsed.ScriptPaths
            .SelectMany(argument => Directory.Exists(argument)
                ? Directory.EnumerateFiles(argument, "*.mg", SearchOption.AllDirectories)
                : new[] { argument })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (paths.Length == 0)
        {
            Console.Error.WriteLine("No .mg scripts were found.");
            return 2;
        }

        OperationSpecificationCatalog catalog = null;
        if (!string.IsNullOrWhiteSpace(parsed.ReferenceDirectory))
        {
            catalog = OperationSpecificationCatalog.Load(parsed.ReferenceDirectory);
            if (!string.IsNullOrWhiteSpace(parsed.EmitCatalogPath))
            {
                catalog.WriteCSharp(parsed.EmitCatalogPath);
                Console.WriteLine("Generated operation catalog: " + parsed.EmitCatalogPath);
            }
        }

        var failures = new List<string>();
        var operationCounts = new Dictionary<short, long>();
        long totalDataBytes = 0;
        long totalLines = 0;
        long totalBlocks = 0;
        long totalCommands = 0;

        foreach (var path in paths)
        {
            try
            {
                var script = CompiledScriptContainer.ReadFile(path);
                totalDataBytes += script.Data.Length;
                totalLines += script.LineOffsets.Count;
                totalBlocks += script.Blocks.Count;

                if (catalog != null)
                {
                    var result = BurikoBytecodeScanner.Scan(script.Data, catalog);
                    totalCommands += result.CommandCount;
                    foreach (var pair in result.OperationCounts)
                    {
                        operationCounts.TryGetValue(pair.Key, out var count);
                        operationCounts[pair.Key] = count + pair.Value;
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add(path + ": " + exception.Message);
            }
        }

        Console.WriteLine("MGSC audit");
        Console.WriteLine("  scripts: " + paths.Length);
        Console.WriteLine("  blocks: " + totalBlocks);
        Console.WriteLine("  line checkpoints: " + totalLines);
        Console.WriteLine("  data bytes: " + totalDataBytes);
        Console.WriteLine("  bytecode commands: " + totalCommands);
        Console.WriteLine("  distinct operations: " + operationCounts.Count);
        Console.WriteLine("  failures: " + failures.Count);

        if (catalog != null && operationCounts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Operation coverage (descending use count)");
            foreach (var pair in operationCounts.OrderByDescending(pair => pair.Value))
            {
                var specification = catalog.Get(pair.Key);
                Console.WriteLine(
                    $"  {pair.Value,8}  {pair.Key,3}  {specification.ScriptName} [{specification.Signature}]");
            }
        }

        foreach (var failure in failures)
        {
            Console.Error.WriteLine("FAIL " + failure);
        }

        return failures.Count == 0 ? 0 : 1;
    }

    private sealed class Arguments
    {
        public string ReferenceDirectory { get; private set; }
        public string EmitCatalogPath { get; private set; }
        public List<string> ScriptPaths { get; } = new List<string>();

        public static Arguments Parse(string[] args)
        {
            var result = new Arguments();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--reference-dir" && i + 1 < args.Length)
                {
                    result.ReferenceDirectory = args[++i];
                }
                else if (args[i] == "--emit-catalog" && i + 1 < args.Length)
                {
                    result.EmitCatalogPath = args[++i];
                }
                else
                {
                    result.ScriptPaths.Add(args[i]);
                }
            }

            return result;
        }
    }
}

internal sealed class OperationSpecification
{
    public short OpCode { get; init; }
    public string EnumName { get; init; }
    public string ScriptName { get; init; }
    public string Signature { get; init; }
}

internal sealed class OperationSpecificationCatalog
{
    private readonly Dictionary<short, OperationSpecification> _byCode;

    private OperationSpecificationCatalog(Dictionary<short, OperationSpecification> byCode)
    {
        _byCode = byCode;
    }

    public OperationSpecification Get(short opCode)
    {
        if (!_byCode.TryGetValue(opCode, out var result))
        {
            throw new InvalidDataException("No operation specification for opcode " + opCode + ".");
        }

        return result;
    }

    public void WriteCSharp(string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("using System;");
        builder.AppendLine();
        builder.AppendLine("namespace Higurashi.IOS.Buriko");
        builder.AppendLine("{");
        builder.AppendLine("    public readonly struct BurikoOperationSpecification");
        builder.AppendLine("    {");
        builder.AppendLine("        public BurikoOperationSpecification(short code, string name, string signature)");
        builder.AppendLine("        {");
        builder.AppendLine("            Code = code;");
        builder.AppendLine("            Name = name;");
        builder.AppendLine("            Signature = signature;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public short Code { get; }");
        builder.AppendLine("        public string Name { get; }");
        builder.AppendLine("        public string Signature { get; }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static class BurikoOperationCatalog");
        builder.AppendLine("    {");
        builder.AppendLine("        private static readonly BurikoOperationSpecification?[] Items = Create();");
        builder.AppendLine();
        builder.AppendLine("        public static BurikoOperationSpecification Get(short code)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (code < 0 || code >= Items.Length || !Items[code].HasValue)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new ArgumentOutOfRangeException(nameof(code), code, \"Unknown Buriko operation.\");");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            return Items[code].Value;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        private static BurikoOperationSpecification?[] Create()");
        builder.AppendLine("        {");
        builder.AppendLine("            var result = new BurikoOperationSpecification?[256];");
        foreach (var specification in _byCode.Values.OrderBy(value => value.OpCode))
        {
            builder.Append("            result[")
                .Append(specification.OpCode)
                .Append("] = new BurikoOperationSpecification(")
                .Append(specification.OpCode)
                .Append(", \"")
                .Append(Escape(specification.ScriptName))
                .Append("\", \"")
                .Append(Escape(specification.Signature))
                .AppendLine("\");");
        }
        builder.AppendLine("            return result;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        File.WriteAllText(fullPath, builder.ToString(), new UTF8Encoding(false));
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public static OperationSpecificationCatalog Load(string referenceDirectory)
    {
        var operationsPath = Path.Combine(
            referenceDirectory,
            "Assets.Scripts.Core.Buriko",
            "BurikoOperations.cs");
        var handlerPath = Path.Combine(
            referenceDirectory,
            "BGICompiler.Compiler",
            "OperationHandler.cs");

        if (!File.Exists(operationsPath) || !File.Exists(handlerPath))
        {
            throw new FileNotFoundException("The 07th-Mod operation source files were not found.");
        }

        var enumValues = ParseEnum(File.ReadAllText(operationsPath));
        var specifications = ParseSignatures(File.ReadAllText(handlerPath), enumValues);
        return new OperationSpecificationCatalog(specifications);
    }

    private static Dictionary<string, short> ParseEnum(string source)
    {
        var match = Regex.Match(
            source,
            @"enum\s+BurikoOperations\s*\{(?<body>.*?)\}",
            RegexOptions.Singleline);
        if (!match.Success)
        {
            throw new InvalidDataException("Unable to parse BurikoOperations enum.");
        }

        var result = new Dictionary<string, short>(StringComparer.Ordinal);
        short value = -1;
        foreach (var raw in match.Groups["body"].Value.Split(','))
        {
            var token = Regex.Replace(raw, @"//.*", string.Empty).Trim();
            if (token.Length == 0)
            {
                continue;
            }

            var assignment = token.Split('=', 2);
            var name = assignment[0].Trim();
            if (assignment.Length == 2)
            {
                value = short.Parse(assignment[1].Trim());
            }
            else
            {
                value++;
            }

            result.Add(name, value);
        }

        return result;
    }

    private static Dictionary<short, OperationSpecification> ParseSignatures(
        string source,
        IReadOnlyDictionary<string, short> enumValues)
    {
        var regex = new Regex(
            "paramLookup\\.Add\\(\\s*\"(?<script>[^\"]+)\"\\s*,\\s*new\\s+OpType\\(\\s*" +
            "BurikoOperations\\.(?<enum>\\w+)\\s*,\\s*(?:\"(?<sig>[^\"]*)\"|string\\.Empty)\\s*\\)\\s*\\)",
            RegexOptions.Singleline);
        var result = new Dictionary<short, OperationSpecification>();

        foreach (Match match in regex.Matches(source))
        {
            var enumName = match.Groups["enum"].Value;
            if (!enumValues.TryGetValue(enumName, out var opCode))
            {
                throw new InvalidDataException("Unknown operation enum in signature table: " + enumName);
            }

            result[opCode] = new OperationSpecification
            {
                OpCode = opCode,
                EnumName = enumName,
                ScriptName = match.Groups["script"].Value,
                Signature = match.Groups["sig"].Success ? match.Groups["sig"].Value : string.Empty
            };
        }

        if (result.Count < 100)
        {
            throw new InvalidDataException("Operation signature table appears incomplete: " + result.Count);
        }

        return result;
    }
}

internal sealed class BytecodeScanResult
{
    public long CommandCount { get; set; }
    public Dictionary<short, long> OperationCounts { get; } = new Dictionary<short, long>();

    public void RecordOperation(short opCode)
    {
        OperationCounts.TryGetValue(opCode, out var count);
        OperationCounts[opCode] = count + 1;
    }
}

internal static class BurikoBytecodeScanner
{
    private const short CommandReturn = 0;
    private const short CommandLineNumber = 1;
    private const short CommandOperation = 2;
    private const short CommandIf = 3;
    private const short CommandDeclaration = 4;
    private const short CommandAssignment = 5;
    private const short CommandJump = 6;

    private const short ValueNull = 1;
    private const short ValueInt = 2;
    private const short ValueString = 3;
    private const short ValueBool = 4;
    private const short ValueVariable = 5;
    private const short ValueOperation = 6;
    private const short ValueMath = 8;

    public static BytecodeScanResult Scan(byte[] data, OperationSpecificationCatalog catalog)
    {
        var result = new BytecodeScanResult();
        using var stream = new MemoryStream(data, false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, false);

        while (stream.Position < stream.Length)
        {
            result.CommandCount++;
            var command = reader.ReadInt16();
            switch (command)
            {
                case CommandReturn:
                    break;
                case CommandLineNumber:
                    reader.ReadInt32();
                    break;
                case CommandOperation:
                    ReadOperation(reader, catalog, result);
                    break;
                case CommandIf:
                    ReadValue(reader, catalog, result);
                    reader.ReadInt32();
                    break;
                case CommandDeclaration:
                    reader.ReadString();
                    reader.ReadString();
                    ReadValue(reader, catalog, result);
                    break;
                case CommandAssignment:
                    ReadReference(reader, catalog, result);
                    ReadValue(reader, catalog, result);
                    break;
                case CommandJump:
                    reader.ReadInt32();
                    break;
                default:
                    throw new InvalidDataException(
                        "Unknown Buriko command " + command + " at byte " + (stream.Position - 2) + ".");
            }
        }

        return result;
    }

    private static void ReadOperation(
        BinaryReader reader,
        OperationSpecificationCatalog catalog,
        BytecodeScanResult result)
    {
        var opCode = reader.ReadInt16();
        var specification = catalog.Get(opCode);
        result.RecordOperation(opCode);
        for (var i = 0; i < specification.Signature.Length; i++)
        {
            ReadValue(reader, catalog, result);
        }
    }

    private static void ReadValue(
        BinaryReader reader,
        OperationSpecificationCatalog catalog,
        BytecodeScanResult result)
    {
        var valueType = reader.ReadInt16();
        switch (valueType)
        {
            case ValueNull:
                return;
            case ValueInt:
                reader.ReadInt32();
                return;
            case ValueString:
                reader.ReadString();
                return;
            case ValueBool:
                reader.ReadBoolean();
                return;
            case ValueVariable:
                ReadReferenceBody(reader, catalog, result);
                return;
            case ValueOperation:
                ReadOperation(reader, catalog, result);
                return;
            case ValueMath:
                reader.ReadInt16();
                ReadValue(reader, catalog, result);
                ReadValue(reader, catalog, result);
                return;
            default:
                throw new InvalidDataException(
                    "Unknown Buriko value type " + valueType +
                    " at byte " + (reader.BaseStream.Position - 2) + ".");
        }
    }

    private static void ReadReference(
        BinaryReader reader,
        OperationSpecificationCatalog catalog,
        BytecodeScanResult result)
    {
        var valueType = reader.ReadInt16();
        if (valueType != ValueVariable)
        {
            throw new InvalidDataException("Assignment target is not a Buriko variable.");
        }

        ReadReferenceBody(reader, catalog, result);
    }

    private static void ReadReferenceBody(
        BinaryReader reader,
        OperationSpecificationCatalog catalog,
        BytecodeScanResult result)
    {
        reader.ReadString();
        ReadValue(reader, catalog, result);
        if (reader.ReadBoolean())
        {
            ReadReference(reader, catalog, result);
        }
    }
}
