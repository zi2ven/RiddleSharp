namespace RiddleSharp.Semantics;

public abstract record Ty
{
    public sealed record IntTy : Ty;

    public sealed record BoolTy : Ty;
    
    public sealed record VoidTy : Ty;
    
    public sealed record FuncTy(IReadOnlyList<Ty> Args, Ty Ret) : Ty;
}