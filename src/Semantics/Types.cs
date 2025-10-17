using RiddleSharp.Frontend;

namespace RiddleSharp.Semantics;

public abstract record Ty
{
    public sealed record IntTy : Ty
    {
        private IntTy(ulong widthInBits, bool signed)
        {
            if (widthInBits is not (1 or 8 or 16 or 32 or 64))
                throw new ArgumentOutOfRangeException(nameof(widthInBits), "integer width must be 1/8/16/32/64");
            WidthInBits = widthInBits;
            Signed = signed;
        }

        public static readonly IntTy Boolean = new(1, true);

        public static readonly IntTy Int8  = new(8,  true);
        public static readonly IntTy Int16 = new(16, true);
        public static readonly IntTy Int32 = new(32, true);
        public static readonly IntTy Int64 = new(64, true);

        public static readonly IntTy UInt8  = new(8,  false);
        public static readonly IntTy UInt16 = new(16, false);
        public static readonly IntTy UInt32 = new(32, false);
        public static readonly IntTy UInt64 = new(64, false);

        public static readonly IntTy[] SignedList   = [Int8, Int16, Int32, Int64];
        public static readonly IntTy[] UnSignedList = [UInt8, UInt16, UInt32, UInt64];

        public ulong  WidthInBits  { get; }
        public bool Signed { get; }

        public override string ToString()
        {
            if (WidthInBits == 1) return "i1";
            return Signed ? $"i{WidthInBits}" : $"u{WidthInBits}";
        }
    }

    public sealed record VoidTy : Ty
    {
        private VoidTy() { }
        public static VoidTy Instance { get; } = new();
        public override string ToString() => "void";
    }

    public sealed record FuncTy(IReadOnlyList<Ty> Args, Ty Ret, bool IsVarArg) : Ty
    {
        public bool Equals(FuncTy? other)
            => other is not null
               && Ret == other.Ret
               && Args.Count == other.Args.Count
               && Args.SequenceEqual(other.Args)
               && IsVarArg == other.IsVarArg;

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (var a in Args) hc.Add(a);
            hc.Add(Ret);
            hc.Add(IsVarArg);
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            var args = string.Join(", ", Args.Select(a => a.ToString()));
            if (IsVarArg) args = (args.Length == 0) ? "..." : $"{args}, ...";
            return $"fn({args}) -> {Ret}";
        }
    }

    public sealed record PointerType(Ty Pointee) : Ty
    {
        public static readonly PointerType CharPointer = new(IntTy.UInt8);
        public override string ToString() => $"{Pointee}*";
    }

    public sealed record ClassTy(QualifiedName Name) : Ty
    {
        public WeakReference<ClassDecl>? DeclRef { get; init; }

        public bool TryGetDecl(out ClassDecl? decl)
        {
            decl = null;
            return DeclRef != null && DeclRef.TryGetTarget(out decl);
        }

        public override string ToString() => $"class {Name}";
    }

    public abstract record FpTy : Ty;

    public sealed record FloatTy : FpTy
    {
        public override string ToString() => "f32";
    }

    public sealed record DoubleTy : FpTy
    {
        public override string ToString() => "f64";
    }
}
