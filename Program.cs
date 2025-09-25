using Antlr4.Runtime;
using RiddleSharp.Frontend;
using RiddleSharp.Semantics;

const string a = """
                 package main;
                 import test;
                 var a = test::b;
                 fun main()->int{
                    var a = 1;
                 }
                 """;
const string b = """
                 package test;
                 var b = 1;
                 """;

var astLower = new CstLower();
var u1 = astLower.Parse(a);
var u2 = astLower.Parse(b);
var sp = new SymbolPass();

var x = sp.Run([(u2 as Unit)!,(u1 as Unit)!]);

Console.WriteLine(x);