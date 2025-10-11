namespace RiddleSharp.Frontend;

internal enum Assoc
{
    Left,
    Right,
    Non
}

internal static class Precedence
{
    public static int Of(string op) => op switch
    {
        "*" or "/" or "%" => 70,
        "+" or "-" => 60,
        "<" or ">" or "<=" or ">=" => 50,
        "==" or "!=" => 40,
        "=" => 10,
        _ => 0
    };

    public static Assoc AssocOf(string op) => op switch
    {
        "=" => Assoc.Right,
        _ => Assoc.Left
    };
}

public static class BinaryRotate
{
    public static Unit Run(Unit unit) =>
        unit with { Stmts = unit.Stmts.Select(RewriteStmt).ToArray() };

    private static Stmt RewriteStmt(Stmt s) => s switch
    {
        VarDecl vd => new VarDecl(
            vd.Name,
            vd.TypeLit is null ? null : RewriteExpr(vd.TypeLit),
            vd.Value is null ? null : RewriteExpr(vd.Value)
        ),

        ExprStmt es => new ExprStmt(RewriteExpr(es.Expr)),

        Block b => new Block(b.Body.Select(RewriteStmt).ToArray()),

        FuncDecl fd => new FuncDecl(
            fd.Name,
            fd.TypeLit is null ? null : RewriteExpr(fd.TypeLit),
            fd.Args.Select(a => new FuncParam(a.Name, RewriteExpr(a.TypeLit!))).ToList(),
            fd.IsVarArg,
            fd.Body?.Select(RewriteStmt).ToArray()
        ),

        If @if => new If(
            RewriteExpr(@if.Condition),
            RewriteStmt(@if.Then),
            @if.Else is null ? null : RewriteStmt(@if.Else)),

        While @while => new While(RewriteExpr(@while.Condition), RewriteStmt(@while.Body)),

        Return res => new Return(res.Expr is null ? null : RewriteExpr(res.Expr)),

        _ => s
    };

    private static Expr RewriteExpr(Expr e) => e switch
    {
        BinaryOp b => Rotate(new BinaryOp(b.Op, RewriteExpr(b.Left), RewriteExpr(b.Right))),
        Call c => new Call(RewriteExpr(c.Callee), c.Args.Select(RewriteExpr).ToArray()),
        MemberAccess m => new MemberAccess(RewriteExpr(m.Parent), m.Child),
        PointedExpr pe => new PointedExpr(RewriteExpr(pe.Value)),
        _ => e
    };

    /// <summary>
    /// 把形如 ((A opL B) op C) 旋成 (A opL (B op C))，
    /// 条件：opC 的优先级高于 opL，或同级且 opC 右结合（如 =）。
    /// </summary>
    private static BinaryOp Rotate(BinaryOp root)
    {
        while (true)
        {
            if (root.Left is BinaryOp(var op, var l, var r) && ShouldRightRotate(op, root.Op))
            {
                // ((A lOp B) op C) ==> (A lOp (B op C))
                var c = root.Right;

                var newRight = Rotate(new BinaryOp(root.Op, r, c));
                root = new BinaryOp(op, l, newRight);
                continue;
            }

            break;
        }

        return root;
    }

    private static bool ShouldRightRotate(string leftOp, string parentOp)
    {
        var pl = Precedence.Of(leftOp);
        var pp = Precedence.Of(parentOp);
        if (pl < pp) return true;
        return pl == pp && Precedence.AssocOf(parentOp) == Assoc.Right;
    }
}