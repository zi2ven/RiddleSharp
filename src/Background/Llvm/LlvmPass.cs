using System.Runtime.CompilerServices;
using RiddleSharp.Frontend;
using RiddleSharp.Semantics;
using Ubiquity.NET.Llvm;
using Ubiquity.NET.Llvm.Instructions;
using Ubiquity.NET.Llvm.Types;
using Ubiquity.NET.Llvm.Values;
using Boolean = RiddleSharp.Frontend.Boolean;
using Module = Ubiquity.NET.Llvm.Module;

namespace RiddleSharp.Background.Llvm;

internal class IntWrapper(int value)
{
    public int Value { get; set; } = value;
}

public static class LlvmPass
{
    public static void Run(Unit[] units)
    {
        ConditionalWeakTable<VarDecl, Value> vars = new();
        ConditionalWeakTable<FuncDecl, Value> functions = new();
        Dictionary<QualifiedName, IStructType> classes = new();
        ConditionalWeakTable<VarDecl, IntWrapper> fieldIndex = new();
        using var context = new Context();
        using var module = context.CreateBitcodeModule();
        foreach (var unit in units)
        {
            var v = new LlvmVisitor(context, module, vars, functions, classes, fieldIndex);
            v.Visit(unit);
        }
        //
        // foreach (var fn in module.Functions)
        // {
        //     if (fn.IsDeclaration) continue; // 只对有函数体的跑
        //
        //     var e = fn.TryRunPasses("mem2reg", "instcombine", "reassociate", "gvn", "simplifycfg");
        //     if (e.Failed)
        //     {
        //         throw new Exception(e.ToString());
        //     }
        // }

        Console.WriteLine(module.WriteToString());

        module.WriteToFile("a.bc");
    }

    private class LlvmVisitor(
        Context context,
        Module module,
        ConditionalWeakTable<VarDecl, Value> vars,
        ConditionalWeakTable<FuncDecl, Value> functions,
        Dictionary<QualifiedName, IStructType> classes,
        ConditionalWeakTable<VarDecl, IntWrapper> fieldIndex)
        : AstVisitor<Value>
    {
        private readonly InstructionBuilder _builder = new(context);

        private readonly Stack<ClassDecl> _classStack = new();

        private readonly Dictionary<(Ty, Ty, string), Func<Value, Value, InstructionBuilder, Value>> _operators = new()
        {
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "+")] = (x, y, builder) => builder.Add(x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "-")] = (x, y, builder) => builder.Sub(x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "*")] = (x, y, builder) => builder.Mul(x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "/")] = (x, y, builder) => builder.SDiv(x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "%")] = (x, y, builder) => builder.SRem(x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "=")] =
                (x, y, builder) =>
                {
                    var l = (x as Load)!;
                    return builder.Store(y, l.Operands[0]!);
                },
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "==")] =
                (x, y, builder) => builder.Compare(IntPredicate.Equal, x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "!=")] =
                (x, y, builder) => builder.Compare(IntPredicate.NotEqual, x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "<")] =
                (x, y, builder) => builder.Compare(IntPredicate.SignedLessThan, x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, "<=")] = (x, y, builder) =>
                builder.Compare(IntPredicate.SignedLessThanOrEqual, x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, ">")] =
                (x, y, builder) => builder.Compare(IntPredicate.SignedGreaterThan, x, y),
            [(Ty.IntTy.Int32, Ty.IntTy.Int32, ">=")] = (x, y, builder) =>
                builder.Compare(IntPredicate.SignedGreaterThanOrEqual, x, y),
        };

        private Value GetVarAlloc(VarDecl var)
        {
            return vars.GetOrCreateValue(var);
        }

        private Value GetFunc(FuncDecl func) => functions.GetOrCreateValue(func);


        private Value GetInstancePtr(Value rev)
        {
            if (rev.NativeType is IPointerType || rev is Load { NativeType: IPointerType }) return rev;

            if (rev is Load ld)
            {
                return ld.Operands[0]!;
            }

            var tmp = _builder.Alloca(rev.NativeType);
            _builder.Store(rev, tmp);
            return tmp;
        }

        private Value GEP_FieldPtr(ITypeRef type, Value thisPtr, int fieldIdx)
        {
            try
            {
                return _builder.GetStructElementPointer(type, thisPtr, (uint)fieldIdx);
            }
            catch
            {
                return _builder.GetElementPtr(thisPtr, context.CreateConstant(0), context.CreateConstant(fieldIdx));
            }
        }


        private T? VisitOrNull<T>(AstNode? node) where T : Value
        {
            return node is null ? null : (T?)Visit(node);
        }

        private T VisitOrThrow<T>(AstNode? node) where T : Value
        {
            if (node is null) throw new Exception("Node is null");
            return Visit(node) as T ?? throw new Exception($"Node is not of type {typeof(T)}");
        }

        private ITypeRef ParseType(Ty ty)
        {
            switch (ty)
            {
                case Ty.IntTy:
                    return context.Int32Type;
                case Ty.FuncTy funcTy:
                    var pty = funcTy.Args.Select(ParseType);
                    return context.GetFunctionType(funcTy.IsVarArg, ParseType(funcTy.Ret), pty);
                case Ty.VoidTy:
                    return context.VoidType;
                case Ty.ClassTy cty:
                    var st = classes[cty.Name];
                    return st;
                case Ty.PointerType py:
                    return ParseType(py.Pointee).CreatePointerType();
                default:
                    throw new NotSupportedException($"Ty {ty} is not supported");
            }
        }

        public override Value VisitClassDecl(ClassDecl node)
        {
            var qn = node.QualifiedName!.ToString();

            var structTy = context.CreateStructType(qn, false);
            classes[node.QualifiedName!] = structTy;

            var fields = node.Stmts.OfType<VarDecl>().ToArray();
            for (var i = 0; i < fields.Length; i++) fieldIndex.Add(fields[i], new IntWrapper(i));


            var fieldTypes = fields.Select(f => ParseType(f.Type!)).ToArray();
            structTy.SetBody(false, fieldTypes);

            _classStack.Push(node);
            foreach (var m in node.Methods.Values) Visit(m);
            foreach (var nc in node.Nested.Values) Visit(nc);
            _classStack.Pop();

            return null!;
        }

        public override Value VisitMemberAccess(MemberAccess node)
        {
            if (node.DeclReference is null || !node.DeclReference.TryGetTarget(out var decl))
                throw new Exception("MemberAccess missing DeclReference");

            switch (decl)
            {
                case VarDecl fld: // 字段
                {
                    var recVal = Visit(node.Parent);
                    var thisPtr = GetInstancePtr(recVal);

                    if (!fieldIndex.TryGetValue(fld, out var idx))
                        throw new Exception($"No field index for {fld.Name}");
                    var fieldPtr = GEP_FieldPtr(ParseType(node.Parent.Type!), thisPtr, idx.Value);

                    return _builder.Load(ParseType(fld.Type!), fieldPtr);
                }

                case FuncDecl m:
                    return GetFunc(m);

                default:
                    throw new NotImplementedException($"MemberAccess for {decl.GetType().Name} not implemented");
            }
        }

        public override Value VisitFuncDecl(FuncDecl node)
        {
            var name = node.QualifiedName!.ToString();
            // 做 main
            if (node.Name == "main") name = "main";
            if (node.Original) name = node.Name;

            var isMethod = _classStack.Count > 0;

            var retTy = ParseType(node.Type!.Ret);

            var paramTypes = new List<ITypeRef>();
            if (isMethod)
            {
                var owner = _classStack.Peek();
                var st = classes[owner.QualifiedName!];
                paramTypes.Add(st.CreatePointerType());
            }
            paramTypes.AddRange(node.Args.Select(a => ParseType(a.Type!)));

            var fty = context.GetFunctionType(node.Type!.IsVarArg, retTy, paramTypes);
            var func = module.CreateFunction(name, fty);
            functions.Add(node, func);

            func.AddAttribute(FunctionAttributeIndex.Function, "nounwind");
            func.AddAttribute(FunctionAttributeIndex.Function, "inlinehint");
            func.AddAttribute(FunctionAttributeIndex.Function, "hot");
            func.AddAttribute(FunctionAttributeIndex.Function, "willreturn");
            func.AddAttribute(FunctionAttributeIndex.Function, "nofree");

            if (node.Body is not null)
            {
                var entry = func.AppendBasicBlock("entry");
                _builder.PositionAtEnd(entry);


                for (var i = 0; i < node.Args.Count; i++)
                {
                    var alloca = func.Parameters[i];
                    vars.Add(node.Args[i], alloca);
                }

                foreach (var a in node.Alloc)
                {
                    var alloca = _builder.Alloca(ParseType(a.Type!));
                    vars.Add(a, alloca);
                }

                foreach (var i in node.Body)
                {
                    Visit(i);
                }

                // 合法化处理
                // if (func.BasicBlocks.ElementAt(func.BasicBlocks.Count - 1).Terminator == null)
                // {
                //     throw new Exception("At the end of the function, a return is required");
                // }
            }

            return null!;
        }

        public override Value VisitVarDecl(VarDecl node)
        {
            if (node.IsGlobal)
            {
                var value = VisitOrThrow<Constant>(node.Value);
                module.AddGlobal(ParseType(node.Type!), false, Linkage.External, value, node.QualifiedName!.ToString());
                vars.Add(node, value);
            }
            else
            {
                var value = VisitOrNull<Value>(node.Value);
                if (value is not null)
                {
                    _builder.Store(value, GetVarAlloc(node));
                }
            }

            return null!;
        }

        public override Value VisitCall(Call node)
        {
            var callee = Visit(node.Callee);
            if (node.Callee is MemberAccess { DeclReference: not null } ma &&
                ma.DeclReference.TryGetTarget(out var d) &&
                d is FuncDecl md)
            {
                var f = (Function)GetFunc(md);

                var args = node.Args.Select(Visit).ToList();

                if (md.isMethod)
                {
                    var rec = Visit(ma.Parent);
                    var thisPtr = GetInstancePtr(rec);
                    args.Insert(0, thisPtr);
                }

                return _builder.Call(f, args);
            }

            if (callee is not Function fn)
                throw new Exception("Call is not of type function");
            var a = node.Args.Select(Visit).ToList();
            return _builder.Call(fn, a);
        }

        public override Value VisitSymbol(Symbol node)
        {
            if (node.DeclReference is null || !node.DeclReference.TryGetTarget(out var d))
            {
                throw new Exception("Symbol is already declared");
            }

            return d switch
            {
                FuncParam p => GetVarAlloc(p),
                VarDecl v => v.IsGlobal ? GetVarAlloc(v) : _builder.Load(ParseType(v.Type!), GetVarAlloc(v)),
                FuncDecl f => GetFunc(f),
                _ => throw new NotImplementedException()
            };
        }

        public override Value VisitInteger(Integer node) => context.CreateConstant((int)node.Value);

        public override Value VisitBoolean(Boolean node) => context.CreateConstant(node.Value);

        public override Value VisitBinaryOp(BinaryOp node)
        {
            var k = (node.Left.Type!, node.Right.Type!, node.Op);

            if (!_operators.TryGetValue(k, out var value))
            {
                throw new Exception($"Unknown operator \'{node.Left.Type} {node.Op} {node.Right.Type} \'");
            }

            return value(Visit(node.Left), Visit(node.Right), _builder);
        }

        public override Value VisitReturn(Return node)
        {
            return node.Expr switch
            {
                null => _builder.Return(),
                _ => _builder.Return(Visit(node.Expr))
            };
        }

        public override Value VisitIf(If node)
        {
            var f = _builder.InsertFunction!;
            var cond = Visit(node.Condition);

            var thenBb = f.AppendBasicBlock("");
            var elseBb = node.Else != null ? f.AppendBasicBlock("") : null;

            // 条件跳转：不需要预先知道 merge 块
            _builder.Branch(cond, thenBb, elseBb ?? f.AppendBasicBlock(""));

            // then
            _builder.PositionAtEnd(thenBb);
            Visit(node.Then);
            var thenTerminated = _builder.InsertBlock?.Terminator != null;

            // else
            var elseTerminated = true;
            if (elseBb != null)
            {
                _builder.PositionAtEnd(elseBb);
                Visit(node.Else!);
                elseTerminated = _builder.InsertBlock?.Terminator != null;
            }

            // 只有当至少一边没终结时，才需要合流块
            if (!thenTerminated || !elseTerminated)
            {
                var mergeBb = f.AppendBasicBlock("if.end");

                if (!thenTerminated)
                {
                    _builder.PositionAtEnd(thenBb);
                    _builder.Branch(mergeBb);
                }

                if (elseBb != null && !elseTerminated)
                {
                    _builder.PositionAtEnd(elseBb);
                    _builder.Branch(mergeBb);
                }

                _builder.PositionAtEnd(mergeBb);
            }

            return null!;
        }

        public override Value VisitWhile(While node)
        {
            var nowFunc = _builder.InsertFunction!;
            var condBb = nowFunc.AppendBasicBlock("");
            var loopBb = nowFunc.AppendBasicBlock("");
            var exitBb = nowFunc.AppendBasicBlock("");
            _builder.Branch(condBb);

            _builder.PositionAtEnd(condBb);
            var cond = Visit(node.Condition);
            _builder.Branch(cond, loopBb, exitBb);

            _builder.PositionAtEnd(loopBb);
            Visit(node.Body);
            if (_builder.InsertBlock?.Terminator == null)
            {
                _builder.Branch(condBb);
            }

            _builder.PositionAtEnd(exitBb);
            return null!;
        }

        public override Value VisitStringLit(StringLit node)
        {
            var cStr = context.CreateConstantString(node.Value /*, addNullTerminator: true*/);

            // 2) 用“常量自己的类型”作为全局变量类型，避免长度不匹配
            var gv = module.AddGlobal(
                cStr.NativeType,
                true,
                linkage: Linkage.Private,
                cStr,
                name: "__str"
            );

            // 3) 可选但推荐：unnamed_addr + 对齐
            gv.UnnamedAddress = UnnamedAddressKind.Local;
            gv.Alignment = 1;

            return gv;
        }
    }
}