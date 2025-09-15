namespace RiddleSharp.Frontend;

public abstract record AstNode
{
    public abstract T Accept<T>(AstVisitor<T> visitor);
}

public record Unit(Stmt[] Stmts) : AstNode
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitUnit(this);
    }
}

public abstract record Stmt : AstNode;

public abstract record Decl(string Name) : Stmt;

public record VarDecl(string Name, Expr? TypeLit, Expr? Value) : Decl(Name)
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitVarDecl(this);
    }
}

public record FuncParam(string Name, Expr TypeLit) : Decl(Name)
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

public record Symbol(string Name) : Expr
{
    public WeakReference<Decl>? DeclReference { get; set; } = null;
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitSymbol(this);
    }
}

public record BinaryOp(string Op, Expr Left, Expr Right) : Expr
{
    public override T Accept<T>(AstVisitor<T> visitor)
    {
        return visitor.VisitBinaryOp(this);
    }
}