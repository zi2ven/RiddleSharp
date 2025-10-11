using CppAst;

namespace RiddleSharp.Frontend;

public static class CppConverter
{
    public static Unit Run(string code, string filename)
    {
        var opts = new CppParserOptions
        {
            ParseMacros = true,
            AdditionalArguments = { "-std=c++20" } // TODO: 按需切换标准
        };
        var comp = CppParser.Parse(code, opts);

        var decls = new List<Stmt>();

        decls.AddRange(comp.Fields.Select(LowerGlobalVariable));

        decls.AddRange(comp.Functions.Select(LowerFunction));

        decls.AddRange(comp.Classes.Select(LowerClass));

        return new Unit(decls.ToArray(), QualifiedName.Parse(filename));
    }


    private static ClassDecl LowerClass(CppClass @class)
    {
        var stmts = @class.Fields.Select(LowerField).Cast<Stmt>().ToList();
        stmts.AddRange(@class.Functions.Select(LowerFunction));
        stmts.AddRange(@class.Classes.Select(LowerClass));

        var decl = new ClassDecl(@class.Name, stmts.ToArray());
        return decl;
    }

    private static VarDecl LowerField(CppField field)
    {
        var typeLit = LowerType(field.Type);
        var init = LowerExpr(field.InitExpression);
        var decl = new VarDecl(field.Name, typeLit, init)
        {
            IsGlobal = false
        };
        return decl;
    }

    private static VarDecl LowerGlobalVariable(CppField v)
    {
        var typeLit = LowerType(v.Type);
        var init = LowerExpr(v.InitExpression);
        var decl = new VarDecl(v.Name, typeLit, init)
        {
            IsGlobal = true
        };
        return decl;
    }

    private static FuncDecl LowerFunction(CppFunction f)
    {
        var retType = f.ReturnType != null ? LowerType(f.ReturnType) : new Symbol(QualifiedName.Parse("void"));

        var args = f.Parameters
            .Select(p => new FuncParam(p.Name, LowerType(p.Type)))
            .ToList();

        var func = new FuncDecl(
            f.Name,
            retType,
            args,
            (f.Flags & CppFunctionFlags.Variadic) != CppFunctionFlags.None,
            null
        );
        return func;
    }

    private static Expr? LowerExpr(CppExpression? e)
    {
        if (e is null) return null;

        if (e is CppLiteralExpression lit)
            return LowerLiteral(lit.Value);

        return new Symbol(QualifiedName.Parse(e.ToString() ?? "<expr>"));
    }

    private static Expr LowerLiteral(object? value)
    {
        return value switch
        {
            bool b => new Boolean(b),
            sbyte i => new Integer(i),
            byte i => new Integer(i),
            short i => new Integer(i),
            ushort i => new Integer(i),
            int i => new Integer(i),
            uint i => new Integer(i),
            long l => new Integer(l),
            ulong u => new Integer(unchecked((long)u)),
            _ => new Symbol(QualifiedName.Parse(value?.ToString() ?? "<lit>"))
        };
    }

    private static Expr LowerType(CppType type)
    {
        return new Symbol(QualifiedName.Parse(type.FullName));
    }
}