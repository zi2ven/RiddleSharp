namespace RiddleSharp.Frontend;

public abstract class AstVisitor<T>
{
    public virtual T Visit(AstNode node)
    {
        return node.Accept(this);
    }

    public virtual T VisitUnit(Unit node)
    {
        foreach (var i in node.Stmts)
        {
            Visit(i);
        }

        return default!;
    }

    public virtual T VisitVarDecl(VarDecl node)
    {
        if (node.Value is not null) Visit(node.Value);
        if (node.TypeLit is not null) Visit(node.TypeLit);
        return default!;
    }

    public virtual T VisitFuncDecl(FuncDecl node)
    {
        if (node.TypeLit is not null) Visit(node.TypeLit);
        foreach (var i in node.Args)
        {
            Visit(i);
        }

        foreach (var i in node.Body)
        {
            Visit(i);
        }

        return default!;
    }

    public virtual T VisitBlock(Block node)
    {
        foreach (var i in node.Body)
        {
            Visit(i);
        }

        return default!;
    }


    public virtual T VisitInteger(Integer node)
    {
        return default!;
    }


    public virtual T VisitSymbol(Symbol node)
    {
        return default!;
    }

    public virtual T VisitBinaryOp(BinaryOp node)
    {
        Visit(node.Left);
        Visit(node.Right);
        return default!;
    }

    public virtual T VisitCall(Call node)
    {
        Visit(node.Callee);
        foreach (var i in node.Args)
        {
            Visit(i);
        }
        return default!;
    }

    public virtual T VisitReturn(Return node)
    {
        if (node.Expr is not null)
        {
            Visit(node.Expr);
        }

        return default!;
    }

    public virtual T VisitIf(If node)
    {
        Visit(node.Condition);
        Visit(node.Then);
        if (node.Else is not null)
        {
            Visit(node.Else);
        }

        return default!;
    }

    public virtual T VisitWhile(While node)
    {
        Visit(node.Condition);
        Visit(node.Body);
        return default!;
    }
}