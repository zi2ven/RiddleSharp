namespace RiddleSharp.Hir;

public abstract record HirType
{
    public abstract ulong SizeInBits { get; }

    public ulong SizeInBytes => (SizeInBits + 7) / 8;
}

public abstract record HirPrimitiveType : HirType
{
}

public record HirIntType(long Width, bool Signed = true) : HirType
{
    public override ulong SizeInBits => (ulong)Width * 8;

    public override string ToString()
    {
        return $"i{SizeInBits}";
    }
}

public abstract record HirFpType : HirType;

public record HirFloatType : HirFpType
{
    public override ulong SizeInBits => 32;

    public override string ToString()
    {
        return "float";
    }
}

public record HirDoubleType : HirFpType
{
    public override ulong SizeInBits => 64;

    public override string ToString()
    {
        return "double";
    }
}

public record HirPointerType(HirType Pointee) : HirPrimitiveType
{
    public override ulong SizeInBits => 64; // todo 根据不同架构来使用不同大小

    public static readonly Lazy<HirPointerType> CharPtrTy = new(() => new HirPointerType(new HirIntType(8)));

    public override string ToString()
    {
        return $"{Pointee}*";
    }
}

public record HirFunctionType(HirType ReturnType, IReadOnlyList<HirType> Params, bool IsVarArg) : HirPrimitiveType
{
    public override ulong SizeInBits => 0;

    public override string ToString()
    {
        return $"{ReturnType} ({string.Join(", ", Params)}";
    }
}

public sealed record HirVoidType : HirType
{
    public override ulong SizeInBits => 0;
}

public sealed record HirStructType(string Name, IReadOnlyList<HirType> Fields) : HirType
{
    public override ulong SizeInBits => Fields.Aggregate(0UL, (sum, t) => checked(sum + t.SizeInBits));
}

// 类型变量（推断用）
public sealed record HirTypeVar(int Id) : HirType
{
    public override ulong SizeInBits => 0; // todo
}