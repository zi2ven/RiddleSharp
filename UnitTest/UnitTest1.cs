using RiddleSharp.Frontend;

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
        Assert.That(ans,Is.EqualTo(result));
    }
}