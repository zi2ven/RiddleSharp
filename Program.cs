using Antlr4.Runtime;
using RiddleSharp.Frontend;

const string a = """
                 var a = 1;
                 fun main()->int{
                    var a: = 1;
                 }
                 """;
var astLower = new CstLower();
astLower.Parse(a);