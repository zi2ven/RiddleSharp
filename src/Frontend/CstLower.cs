using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;

namespace RiddleSharp.Frontend;

using Antlr4.Runtime.Tree;

/// <summary>
/// 用于将 Riddle 语言的语法树转换为抽象语法树（AST）。
/// </summary>
public class CstLower : RiddleBaseVisitor<AstNode>
{
    public Unit Parse(string code)
    {
        var el = new ErrorListener(code);
        var input = new AntlrInputStream(code);
        var lexer = new RiddleLexer(input);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(el);

        var tokens = new CommonTokenStream(lexer);
        var parser = new RiddleParser(tokens)
        {
            Interpreter =
            {
                PredictionMode = PredictionMode.SLL
            }
        };
        parser.RemoveErrorListeners();
        parser.AddErrorListener(el);

        RiddleParser.CompileUnitContext tree;
        try
        {
            parser.ErrorHandler = new BailErrorStrategy();
            tree = parser.compileUnit();
        }
        catch (ParseCanceledException)
        {
            tokens.Seek(0);
            parser.Reset();
            parser.Interpreter.PredictionMode = PredictionMode.LL;
            parser.ErrorHandler = new DefaultErrorStrategy();
            tree = parser.compileUnit();
        }

        if (el.Messages.Count > 0)
        {
            foreach (var msg in el.Messages)
                Console.WriteLine(msg);
            Environment.Exit(62);
        }

        return (VisitCompileUnit(tree) as Unit)!;
    }

    private T? LowerOrNull<T>(IParseTree? tree) where T : AstNode
    {
        if (tree is null)
        {
            return null;
        }

        return (T)Visit(tree);
    }

    private T LowerOrThrow<T>(IParseTree? tree) where T : AstNode
    {
        if (tree is null)
        {
            throw new NullReferenceException();
        }

        return (T)Visit(tree);
    }

    public override AstNode VisitCompileUnit(RiddleParser.CompileUnitContext context)
    {
        var stmts = context.statememt().Select(LowerOrThrow<Stmt>).ToList();
        var package = new QualifiedName();
        if (context.packageStmt() is not null)
        {
            package = QualifiedName.Parse(context.packageStmt().name.GetText());
        }

        var depends = new HashSet<QualifiedName>();

        foreach (var stmt in context.importStmt())
        {
            depends.Add(QualifiedName.Parse(stmt.name.GetText()));
        }

        return new Unit(stmts.ToArray(), package)
        {
            Depend = depends
        };
    }

    public override AstNode VisitExprStmt(RiddleParser.ExprStmtContext context)
    {
        return new ExprStmt(LowerOrThrow<Expr>(context.expression()));
    }

    public override AstNode VisitInteger(RiddleParser.IntegerContext context) =>
        new Integer(int.Parse(context.GetText()));

    public override AstNode VisitSymbol(RiddleParser.SymbolContext context)
    {
        return new Symbol(QualifiedName.Parse(context.GetText()));
    }

    public override AstNode VisitVarDecl(RiddleParser.VarDeclContext context)
    {
        var type = LowerOrNull<Expr>(context.type);
        var value = LowerOrNull<Expr>(context.value);
        return new VarDecl(context.name.Text, type, value);
    }

    public override AstNode VisitBinaryOp(RiddleParser.BinaryOpContext context)
    {
        var left = LowerOrThrow<Expr>(context.left);
        var right = LowerOrThrow<Expr>(context.right);
        return new BinaryOp(context.op().GetText(), left, right);
    }

    public override AstNode VisitFuncParam(RiddleParser.FuncParamContext context)
    {
        return new FuncParam(context.name.Text, LowerOrThrow<Expr>(context.type));
    }

    public override AstNode VisitFuncDecl(RiddleParser.FuncDeclContext context)
    {
        var name = context.name.Text;
        var @params = context.funcParam().Select(LowerOrThrow<FuncParam>).ToList();
        var returnType = LowerOrNull<Expr>(context.type);
        var body = LowerOrThrow<Block>(context.body);
        return new FuncDecl(name, returnType, @params.ToArray(), body.Body);
    }

    public override AstNode VisitBlock(RiddleParser.BlockContext context)
    {
        var stmt = context.statememt().Select(LowerOrThrow<Stmt>).ToArray();
        return new Block(stmt);
    }

    public override AstNode VisitCall(RiddleParser.CallContext context)
    {
        var args = context._args.Select(LowerOrThrow<Expr>).ToList();
        return new Call(LowerOrThrow<Expr>(context.callee), args.ToArray());
    }

    public override AstNode VisitReturnStmt(RiddleParser.ReturnStmtContext context)
    {
        return new Return(LowerOrNull<Expr>(context.result));
    }
}