using RiddleSharp.Frontend;

namespace RiddleSharp.Semantics;

public static class TypeInfer
{
    public static Unit[] Run(Unit[] units)
    {
        foreach (var unit in units)
        {
            var visitor = new TypeVisitor();
            visitor.Visit(unit);
        }

        return units;
    }

    private class TypeVisitor : AstVisitor<object?>
    {
        private readonly Dictionary<(Ty, Ty, string), Ty> _opType = new()
        {
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "+")] = Ty.IntTy.Int32,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "-")] = Ty.IntTy.Int32,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "*")] = Ty.IntTy.Int32,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "/")] = Ty.IntTy.Int32,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "%")] = Ty.IntTy.Int32,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "=")] = Ty.IntTy.Int32,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "==")] = Ty.IntTy.Boolean,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "!=")] = Ty.IntTy.Boolean,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "<")] = Ty.IntTy.Boolean,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "<=")] = Ty.IntTy.Boolean,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, ">")] = Ty.IntTy.Boolean,
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, ">=")] = Ty.IntTy.Boolean,
        };


        private readonly Stack<FuncDecl> _funcStack = new();

        private static bool CheckType(Ty x, Ty y)
        {
            return x switch
            {
                Ty.IntTy or Ty.VoidTy => x == y,
                _ => throw new NotSupportedException($"Type {x} is not supported")
            };
        }

        public override object? VisitBinaryOp(BinaryOp node)
        {
            Visit(node.Left);
            Visit(node.Right);
            if (!_opType.ContainsKey((node.Left.Type!, node.Right.Type!, node.Op)))
            {
                throw new Exception($"Unknown operator \'{node.Left.Type} {node.Op} {node.Right.Type} \'");
            }

            node.Type = _opType[(node.Left.Type!, node.Right.Type!, node.Op)];
            return base.VisitBinaryOp(node);
        }

        public override object? VisitVarDecl(VarDecl node)
        {
            if (node.TypeLit is not null)
            {
                Visit(node.TypeLit);
                node.Type = node.TypeLit.Type;
            }

            if (node.Value is not null)
            {
                Visit(node.Value);
                if (node.Type is not null)
                {
                    if (!CheckType(node.Value.Type ?? throw new InvalidOperationException(), node.Type))
                    {
                        throw new Exception(
                            $"Type {node.Value.Type ?? throw new InvalidOperationException()} is not supported");
                    }
                }
                else
                {
                    node.Type = node.Value.Type;
                }
            }

            return null;
        }

        public override object? VisitFuncDecl(FuncDecl node)
        {
            Ty returnType;
            if (node.TypeLit is not null)
            {
                Visit(node.TypeLit);
                returnType = node.TypeLit.Type!;
            }
            else
            {
                returnType = new Ty.VoidTy();
            }

            List<Ty> paramTypes = [];
            foreach (var i in node.Args)
            {
                Visit(i.TypeLit!);
                i.Type = i.TypeLit!.Type;
                paramTypes.Add(i.TypeLit!.Type ?? throw new InvalidOperationException());
            }

            node.Type = new Ty.FuncTy(paramTypes, returnType, node.IsVarArg);

            if (node.Body is not null)
            {
                _funcStack.Push(node);
                foreach (var i in node.Body)
                {
                    Visit(i);
                }

                _funcStack.Pop();
            }

            return null;
        }

        public override object? VisitSymbol(Symbol node)
        {
            if (node.DeclReference is null || !node.DeclReference.TryGetTarget(out var decl))
            {
                throw new Exception($"Symbol \'{node.Name}\' not have decl ref");
            }

            node.Type = decl switch
            {
                BuiltinTypeDecl b => b.Name switch
                {
                    "void" => new Ty.VoidTy(),
                    "bool" => Ty.IntTy.Boolean,
                    "int" => Ty.IntTy.Int32,
                    _ => throw new NotImplementedException($"Builtin type \'{b.Name}\' not implemented")
                },
                VarDecl v => v.Type,
                FuncDecl f => f.Type!.Ret,
                _ => throw new NotImplementedException()
            };

            return null;
        }

        public override object? VisitCall(Call node)
        {
            Visit(node.Callee);
            switch (node.Callee)
            {
                case Symbol s:
                    if (s.DeclReference is null || !s.DeclReference.TryGetTarget(out var decl))
                    {
                        throw new Exception("Decl reference not found");
                    }

                    if (decl is not FuncDecl f)
                    {
                        throw new Exception("Call Function Decl not implemented");
                    }

                    node.Type = f.Type!.Ret;

                    break;
                default:
                    throw new NotImplementedException();
            }

            foreach (var i in node.Args)
            {
                Visit(i);
            }

            return null;
        }

        public override object? VisitIf(If node)
        {
            Visit(node.Condition);
            if (node.Condition.Type is not Ty.IntTy { Width: 1 })
            {
                throw new Exception("If condition must be boolean");
            }

            Visit(node.Then);
            if (node.Else is not null)
            {
                Visit(node.Else);
            }

            return null;
        }

        public override object? VisitWhile(While node)
        {
            Visit(node.Condition);
            if (node.Condition.Type is not Ty.IntTy { Width: 1 })
            {
                throw new Exception("While condition must be boolean");
            }

            Visit(node.Body);
            return null;
        }

        public override object? VisitReturn(Return node)
        {
            var func = _funcStack.Peek();
            Ty t = new Ty.VoidTy();
            if (node.Expr is not null)
            {
                Visit(node.Expr);
                t = node.Expr.Type!;
            }

            if (!CheckType(func.Type!.Ret, t))
            {
                throw new Exception(
                    $"The return type {t} of the value is different from the return type {func.Type.Ret} of the function");
            }

            return null;
        }
    }
}