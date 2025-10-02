using RiddleSharp.Semantics;

namespace RiddleSharp.Frontend;

public sealed record QualifiedName(IReadOnlyList<string> Parts)
{
    public QualifiedName(string part) : this([part])
    {
    }

    public QualifiedName() : this([])
    {
    }

    public static QualifiedName Parse(string text)
    {
        var parts = text.Split(["::"], StringSplitOptions.None);
        return new QualifiedName(parts);
    }

    public QualifiedName Add(string value)
    {
        return new QualifiedName(Parts.Concat([value]).ToArray());
    }

    public QualifiedName Add(QualifiedName value)
    {
        return new QualifiedName(Parts.Concat(value.Parts).ToArray());
    }

    public override string ToString() => string.Join("::", Parts);

    public bool Equals(QualifiedName? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (Parts.Count != other.Parts.Count) return false;
        return !Parts.Where((t, i) => !string.Equals(t, other.Parts[i], StringComparison.Ordinal)).Any();
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        foreach (var p in Parts)
            hc.Add(p, StringComparer.Ordinal);
        return hc.ToHashCode();
    }
}

public abstract record AstNode
{
    public abstract T Accept<T>(AstVisitor<T> visitor);
}

public sealed record Unit(Stmt[] Stmts, QualifiedName PackageName) : AstNode
{
    public Lazy<Dictionary<QualifiedName, Decl>> Decls { get; } = new();
    public HashSet<QualifiedName> Depend { get; init; } = [];

    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitUnit(this);
    }

    public override string ToString()
    {
        var s = string.Join(", ", Stmts.Select(s => s.ToString()));
        return $"Unit{{ PackageName = {PackageName}, Stmts = [{s}] }}";
    }

    public bool Equals(Unit? other)
    {
        if (other is null) return false;
        if (PackageName != other.PackageName) return false;
        if (Stmts.Length != other.Stmts.Length) return false;
        return !Stmts.Where((t, i) => !t.Equals(other.Stmts[i])).Any();
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Depend, Decls, Stmts, PackageName);
    }
}

public abstract record Stmt : AstNode;

public abstract record Decl(string Name) : Stmt
{
    public QualifiedName? QualifiedName { get; set; }
}

public record VarDecl(string Name, Expr? TypeLit, Expr? Value) : Decl(Name)
{
    public bool IsGlobal { get; set; }
    public Ty? Type { get; set; }

    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitVarDecl(this);
    }
}

public record FuncParam(string Name, Expr TypeLit) : VarDecl(Name, TypeLit, null)
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        throw new NotImplementedException();
    }
}

public record FuncDecl(string Name, Expr? TypeLit, FuncParam[] Args, Stmt[] Body) : Decl(Name)
{
    public Ty? Type { get; set; }
    public List<VarDecl> Alloc { get; set; } = [];

    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitFuncDecl(this);
    }

    public override string ToString()
    {
        var args = string.Join(", ", Args.Select(s => s.ToString()));
        var body = string.Join(", ", Body.Select(s => s.ToString()));
        return
            $"FuncDecl {{ Name = {Name}, QualifiedName = {QualifiedName}, TypeLit = {TypeLit}, Args = [{args}], Body = [{body}] }} }}";
    }
}

// only in Symbol Pass
public record BuiltinTypeDecl(string Name) : Decl(Name)
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        throw new NotImplementedException();
    }
}

public record ExprStmt(Expr Expr) : Stmt
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.Visit(Expr);
    }
}

public record Block(Stmt[] Body) : Stmt
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitBlock(this);
    }
}

public record Return(Expr? Expr) : Stmt
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitReturn(this);
    }
}

public abstract record Expr : AstNode
{
    public Ty? Type { get; set; }
}

public record Integer : Expr
{
    public Integer(int Value)
    {
        this.Value = Value;
        Type = Ty.IntTy.Instance;
    }

    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitInteger(this);
    }

    public int Value { get; }
}

public record Symbol(QualifiedName Name) : Expr
{
    public WeakReference<Decl>? DeclReference { get; set; }

    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitSymbol(this);
    }

    public override string ToString()
    {
        var d = DeclReference != null && DeclReference.TryGetTarget(out _) ? "<found>" : "<unfound>";
        return $"Symbol {{ Name = {Name}, DeclReference = {d} }}";
    }
}

public record BinaryOp(string Op, Expr Left, Expr Right) : Expr
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitBinaryOp(this);
    }
}

public record Call(Expr Callee, Expr[] Args) : Expr
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitCall(this);
    }
}