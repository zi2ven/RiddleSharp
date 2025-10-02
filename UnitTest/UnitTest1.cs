using RiddleSharp.Frontend;
using RiddleSharp.Semantics;

namespace UnitTest;

public class AstTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        const string text = """
                            package test;
                            var b = 1;
                            """;

        var lower = new CstLower();
        var result = lower.Parse(text);
        var ans = new Unit([new VarDecl("b", null, new Integer(1))], QualifiedName.Parse("test"));
        Assert.That(ans, Is.EqualTo(result));
    }

    [Test]
    public void TestTypeInference()
    {
        const string text = """
                            package test;
                            var a = 1;
                            fun main(x: int) -> int {
                                return x + a;
                            }
                            """;

        var lower = new CstLower();
        var unit = lower.Parse(text);
        unit = SymbolPass.Run([unit])[0];
        var inferredUnits = TypeInfer.Run([unit]);

        var varDecl = (VarDecl)inferredUnits[0].Decls.Value[QualifiedName.Parse("test::a")];
        var funcDecl = (FuncDecl)inferredUnits[0].Decls.Value[QualifiedName.Parse("test::main")];

        Assert.Multiple(() =>
        {
            Assert.That(varDecl.Type, Is.EqualTo(Ty.IntTy.Instance));
            Assert.That(funcDecl.Type, Is.EqualTo(new Ty.FuncTy(new List<Ty> { Ty.IntTy.Instance }, Ty.IntTy.Instance)));
        });
    }
}