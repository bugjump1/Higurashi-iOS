using System;

namespace Higurashi.IOS.Buriko
{
    public enum BurikoValueKind : short
    {
        None = 0,
        Null = 1,
        Int = 2,
        String = 3,
        Bool = 4,
        Variable = 5
    }

    public sealed class BurikoReference
    {
        public BurikoReference(string name, int index, BurikoReference member = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Index = index;
            Member = member;
        }

        public string Name { get; }
        public int Index { get; }
        public BurikoReference Member { get; }
    }

    public readonly struct BurikoValue
    {
        private BurikoValue(BurikoValueKind kind, int integer, string text, BurikoReference reference)
        {
            Kind = kind;
            Integer = integer;
            Text = text;
            Reference = reference;
        }

        public BurikoValueKind Kind { get; }
        public int Integer { get; }
        public string Text { get; }
        public BurikoReference Reference { get; }

        public static BurikoValue Null => new BurikoValue(BurikoValueKind.Null, 0, null, null);
        public static BurikoValue FromInt(int value) => new BurikoValue(BurikoValueKind.Int, value, null, null);
        public static BurikoValue FromBool(bool value) => new BurikoValue(BurikoValueKind.Bool, value ? 1 : 0, null, null);
        public static BurikoValue FromString(string value) => new BurikoValue(BurikoValueKind.String, 0, value ?? string.Empty, null);
        public static BurikoValue FromReference(BurikoReference value) => new BurikoValue(BurikoValueKind.Variable, 0, null, value);

        public int AsInt(BurikoMemory memory)
        {
            switch (Kind)
            {
                case BurikoValueKind.Int:
                case BurikoValueKind.Bool:
                    return Integer;
                case BurikoValueKind.String:
                    if (int.TryParse(Text, out var parsed))
                    {
                        return parsed;
                    }

                    throw new InvalidCastException("Buriko string is not an integer: " + Text);
                case BurikoValueKind.Variable:
                    return memory.Get(Reference).AsInt(memory);
                default:
                    throw new InvalidCastException("Cannot convert " + Kind + " to integer.");
            }
        }

        public bool AsBool(BurikoMemory memory)
        {
            if (Kind == BurikoValueKind.String)
            {
                return !string.IsNullOrEmpty(Text) && Text != "0";
            }

            return AsInt(memory) != 0;
        }

        public string AsString(BurikoMemory memory)
        {
            switch (Kind)
            {
                case BurikoValueKind.Null:
                    return string.Empty;
                case BurikoValueKind.Int:
                case BurikoValueKind.Bool:
                    return Integer.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case BurikoValueKind.String:
                    return Text ?? string.Empty;
                case BurikoValueKind.Variable:
                    return memory.Get(Reference).AsString(memory);
                default:
                    throw new InvalidCastException("Cannot convert " + Kind + " to string.");
            }
        }
    }
}

