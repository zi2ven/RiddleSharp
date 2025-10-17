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
        private Unit? _unit;

        public override object? VisitUnit(Unit node)
        {
            _unit = node;
            return base.VisitUnit(node);
        }

        private static bool CheckType(Ty x, Ty y)
        {
            return x switch
            {
                Ty.IntTy or Ty.VoidTy => x == y,
                Ty.PointerType pty => y is Ty.PointerType pty2 && CheckType(pty.Pointee, pty2.Pointee),
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
                returnType = Ty.VoidTy.Instance;
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
                    "void" => Ty.VoidTy.Instance,
                    "bool" => Ty.IntTy.Boolean,
                    "int" => Ty.IntTy.Int32,
                    "char" => Ty.IntTy.UInt8,
                    _ => throw new NotImplementedException($"Builtin type \'{b.Name}\' not implemented")
                },
                VarDecl v => v.Type,
                FuncDecl f => f.Type,
                ClassDecl c => new Ty.ClassTy(c.QualifiedName!),
                _ => throw new NotImplementedException()
            };

            return null;
        }

        public override object? VisitCall(Call node)
        {
            Visit(node.Callee);
            foreach (var a in node.Args) Visit(a);

            var ft = node.Callee.Type as Ty.FuncTy ?? node.Callee switch
            {
                Symbol { DeclReference: not null } s when s.DeclReference.TryGetTarget(out var sd) &&
                                                          sd is FuncDecl sf => sf.Type!,
                MemberAccess { DeclReference: not null } ma when ma.DeclReference.TryGetTarget(out var md) &&
                                                                 md is FuncDecl mf => mf.Type!,
                _ => throw new NotImplementedException(),
            };

            if (ft is null) throw new Exception("Callee is not a function");

            CheckArgs(ft, node.Args);
            node.Type = ft.Ret;
            return null;
        }

        private static void CheckArgs(Ty.FuncTy ft, IReadOnlyList<Expr> args)
        {
            var nFix = ft.Args.Count;
            switch (ft.IsVarArg)
            {
                case false when args.Count != nFix:
                    throw new Exception($"Argument count mismatch: expected {nFix}, got {args.Count}");
                case true when args.Count < nFix:
                    throw new Exception($"Argument count mismatch: expected at least {nFix}, got {args.Count}");
            }

            for (var i = 0; i < Math.Min(nFix, args.Count); i++)
            {
                if (!CheckType(args[i].Type!, ft.Args[i]))
                    throw new Exception(
                        $"Argument {i + 1} type mismatch: got {args[i].Type}, expect {ft.Args[i]}");
            }
        }

        public override object? VisitIf(If node)
        {
            Visit(node.Condition);
            if (node.Condition.Type is not Ty.IntTy { WidthInBits: 1 })
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
            if (node.Condition.Type is not Ty.IntTy { WidthInBits: 1 })
            {
                throw new Exception("While condition must be boolean");
            }

            Visit(node.Body);
            return null;
        }

        public override object? VisitReturn(Return node)
        {
            var func = _funcStack.Peek();
            Ty t = Ty.VoidTy.Instance;
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

        public override object? VisitMemberAccess(MemberAccess node)
        {
            Visit(node.Parent);

            if (node.Parent is Symbol { DeclReference: not null } s &&
                s.DeclReference.TryGetTarget(out var d) &&
                d is ClassDecl cls)
            {
                if (cls.Members.TryGetValue(node.Child, out var sf))
                {
                    if (!sf.IsStatic) throw new Exception($"'{cls.QualifiedName}::{node.Child}' is not static");
                    node.DeclReference = new WeakReference<Decl>(sf);
                    node.Type = sf.Type;
                    return null;
                }

                if (cls.Methods.TryGetValue(node.Child, out var sm))
                {
                    if (sm.isMethod) throw new Exception($"'{cls.QualifiedName}::{node.Child}' is not static");
                    node.DeclReference = new WeakReference<Decl>(sm);
                    node.Type = sm.Type; // Ty.FuncTy
                    return null;
                }

                if (cls.Nested.TryGetValue(node.Child, out var sc))
                {
                    node.DeclReference = new WeakReference<Decl>(sc);
                    node.Type = new Ty.ClassTy(sc.QualifiedName!);
                    return null;
                }

                throw new Exception($"'{cls.QualifiedName}::{node.Child}' does not exist");
            }

            if (node.Parent.Type is Ty.ClassTy ct)
            {
                var cl = GetClassDecl(ct);

                if (cl.Members.TryGetValue(node.Child, out var fld))
                {
                    if (fld.IsStatic)
                        throw new Exception($"'{cl.QualifiedName}::{node.Child}' is static; use TypeName.{node.Child}");
                    node.DeclReference = new WeakReference<Decl>(fld);
                    node.Type = fld.Type;
                    return null;
                }

                if (cl.Methods.TryGetValue(node.Child, out var m))
                {
                    if (!m.isMethod)
                        throw new Exception(
                            $"'{cl.QualifiedName}::{node.Child}' is static; use TypeName.{node.Child}()");
                    node.DeclReference = new WeakReference<Decl>(m);
                    node.Type = m.Type; // Ty.FuncTy
                    return null;
                }

                if (cl.Nested.TryGetValue(node.Child, out var nested))
                {
                    // 访问嵌套类型需要通过类型名，一般不允许通过实例访问；可按语言规则选择允许/禁止
                    throw new Exception($"Cannot access nested type '{nested.Name}' through instance");
                }

                // 例：内置成员（如数组 length）可在此特殊处理
                throw new Exception($"'{ct}' has no member '{node.Child}'");
            }

            throw new Exception($"Receiver of member access must be a type or class instance");
        }

        private ClassDecl GetClassDecl(Ty.ClassTy ct)
        {
            var qn = ct.Name;
            if (!_unit!.Decls.Value.TryGetValue(qn, out var d) || d is not ClassDecl cls)
                throw new Exception($"Type '{qn}' is not a class");
            return cls;
        }

        public override object? VisitPointed(PointedExpr node)
        {
            Visit(node.Value);
            node.Type = new Ty.PointerType(node.Value.Type!);
            return null;
        }
    }
}