namespace RiddleSharp.Frontend;

public class AstPrinter : AstVisitor<string>
{
    private string VisitOrNull(AstNode? node) => node is null ? "" : Visit(node);

    private const string Tab = "   ";
    
    public override string VisitUnit(Unit node)
    {
        return $"package {node.PackageName}{{\n{node.Stmts.Aggregate("", (current, i) => current + Tab + Visit(i))}\n}}";
    }

    public override string VisitVarDecl(VarDecl node)
    {
        return $"var {node.QualifiedName} : {VisitOrNull(node.TypeLit)} = {VisitOrNull(node.Value)}";
    }

    public override string VisitSymbol(Symbol node)
    {
        return node.Name.ToString();
    }

    public override string VisitInteger(Integer node)
    {
        return node.Value.ToString();
    }
}