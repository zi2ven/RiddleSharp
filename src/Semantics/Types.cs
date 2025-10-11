using RiddleSharp.Frontend;

namespace RiddleSharp.Semantics;

public abstract record Ty
{
    public sealed record IntTy : Ty
    {
        private IntTy(int width, bool signed)
        {
            Width = width;
            Signed = signed;
        }

        public static readonly IntTy Boolean = new(1, false);
        
        public static readonly IntTy Int8 = new(8, true);
        public static readonly IntTy Int16 = new(16, true);
        public static readonly IntTy Int32 = new(32, true);
        public static readonly IntTy Int64 = new(32, true);
        public static readonly IntTy UInt8 = new(8, true);
        public static readonly IntTy UInt16 = new(16, true);
        public static readonly IntTy UInt32 = new(32, true);
        public static readonly IntTy UInt64 = new(32, true);

        public static readonly IntTy[] SignedList = [Int8, Int16, Int32, Int64];
        public static readonly IntTy[] UnSignedList = [UInt8, UInt16, UInt32, UInt64];

        public int Width { get; }
        public bool Signed { get; }
    }

    public sealed record VoidTy : Ty
    {
        private VoidTy()
        {
        }

        public static VoidTy Instance { get; } = new();
    }

    public sealed record FuncTy(IReadOnlyList<Ty> Args, Ty Ret, bool IsVarArg) : Ty
    {
        public bool Equals(FuncTy? other)
            => other is not null
               && Ret == other.Ret
               && Args.Count == other.Args.Count
               && Args.SequenceEqual(other.Args)
               && IsVarArg.Equals(other.IsVarArg);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (var a in Args) hc.Add(a);
            hc.Add(Ret);
            hc.Add(IsVarArg);
            return hc.ToHashCode();
        }
    }

    public sealed record PointerType(Ty Pointee) : Ty
    {
        public static readonly PointerType CharPointer = new(IntTy.Int8);
        public override string ToString() => $"{Pointee}*";
    }
    
    public sealed record ClassTy(QualifiedName Name) : Ty
    {
        public override string ToString() => $"class {Name}";
    }
}