namespace RiddleSharp.Semantics;

public abstract record Ty
{
    public sealed record IntTy : Ty
    {
        private IntTy() {}
        public static readonly IntTy Instance = new();
    }

    public sealed record BoolTy : Ty
    {
        private BoolTy() {}
        public static readonly BoolTy Instance = new();
    }
    
    public sealed record VoidTy : Ty;

    public sealed record FuncTy(IReadOnlyList<Ty> Args, Ty Ret) : Ty
    {
        public bool Equals(FuncTy? other)
            => other is not null
               && Ret == other.Ret
               && Args.Count == other.Args.Count
               && Args.SequenceEqual(other.Args);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (var a in Args) hc.Add(a);
            hc.Add(Ret);
            return hc.ToHashCode();
        }
    }
}