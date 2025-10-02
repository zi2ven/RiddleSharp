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
    
    public sealed record FuncTy(IReadOnlyList<Ty> Args, Ty Ret) : Ty;
}