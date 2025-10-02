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
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "+")] = Ty.IntTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "-")] = Ty.IntTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "*")] = Ty.IntTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "/")] = Ty.IntTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "%")] = Ty.IntTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "=")] = Ty.IntTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "==")] = Ty.BoolTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "!=")] = Ty.BoolTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "<")] = Ty.BoolTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "<=")] = Ty.BoolTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, ">")] = Ty.BoolTy.Instance,
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, ">=")] = Ty.BoolTy.Instance,
        };

        private static bool CheckType(Ty x, Ty y)
        {
            return x switch
            {
                Ty.IntTy or Ty.BoolTy or Ty.VoidTy => x == y,
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
                Visit(i.TypeLit);
                paramTypes.Add(i.TypeLit.Type ?? throw new InvalidOperationException());
            }

            node.Type = new Ty.FuncTy(paramTypes, returnType);

            foreach (var i in node.Body)
            {
                Visit(i);
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
                    "bool" => Ty.BoolTy.Instance,
                    "int" => Ty.IntTy.Instance,
                    _ => throw new NotImplementedException($"Builtin type \'{b.Name}\' not implemented")
                },
                VarDecl v => v.Type,
                FuncDecl f => (f.Type as Ty.FuncTy)!.Ret,
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

                    node.Type = (f.Type as Ty.FuncTy)!.Ret;

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
    }
}