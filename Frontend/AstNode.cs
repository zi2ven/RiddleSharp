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

public record Unit(Stmt[] Stmts, QualifiedName PackageName) : AstNode
{
    public Lazy<Dictionary<QualifiedName, Decl>> Decls { get; } = new();
    public HashSet<QualifiedName> Depend = [];

    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitUnit(this);
    }

    public override string ToString()
    {
        var s = string.Join(", ", Stmts.Select(s => s.ToString()));
        return $"Unit{{ PackageName = {PackageName}, Statements = [{s}] }}";
    }
}

public abstract record Stmt : AstNode;

public abstract record Decl(string Name) : Stmt
{
    public QualifiedName? QualifiedName { get; set; } = null;
}

public record VarDecl(string Name, Expr? TypeLit, Expr? Value) : Decl(Name)
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitVarDecl(this);
    }
}

public record FuncParam(string Name, Expr TypeLit) : Stmt
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        throw new NotImplementedException();
    }
}

public record FuncDecl(string Name, Expr? TypeLit, FuncParam[] Args, Stmt[] Body) : Decl(Name)
{
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

public abstract record Expr : AstNode;

public record Integer(int Value) : Expr
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitInteger(this);
    }
}

public record Symbol(QualifiedName Name) : Expr
{
    public WeakReference<Decl>? DeclReference { get; set; } = null;

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