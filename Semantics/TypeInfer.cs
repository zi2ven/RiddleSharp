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
        return [];
    }

    private class TypeVisitor : AstVisitor<object?>
    {
        private Dictionary<Tuple<Ty, Ty, string>, Ty> _opType = [];
        
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
                    "bool" => new Ty.BoolTy(),
                    "int" => new Ty.IntTy(),
                    _ => throw new NotImplementedException($"Builtin type \'{b.Name}\' not implemented")
                },
                VarDecl v => v.Type,
                FuncDecl f => f.Type,
                _ => throw new NotImplementedException()
            };

            return null;
        }
    }
}