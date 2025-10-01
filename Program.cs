using Antlr4.Runtime;
using RiddleSharp.Background.Llvm;
using RiddleSharp.Frontend;
using RiddleSharp.Semantics;

const string a = """
                 package main;
                 import test;
                 var a = 1;
                 fun main()->int{
                    var a = 1+1/1;
                 }
                 """;
const string b = """
                 package test;
                 var b = 1;
                 """;

var astLower = new CstLower();
var u1 = astLower.Parse(a);
var u2 = astLower.Parse(b);

u1 = BinaryRotate.Run(u1);
u2 = BinaryRotate.Run(u2);

var x = SymbolPass.Run([u2,u1]);

var tp = TypeInfer.Run(x);

foreach (var i in tp)
{
    Console.WriteLine(i);
}

LlvmPass.Run(tp);