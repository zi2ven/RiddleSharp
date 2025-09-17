using Antlr4.Runtime;
using RiddleSharp.Frontend;

const string a = """
                 var a = 1;
                 fun main()->int{
                    var a = Locals::a;
                 }
                 """;
var astLower = new CstLower();
astLower.Parse(a);