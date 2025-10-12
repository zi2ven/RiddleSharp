using RiddleSharp.Background.Llvm;
using RiddleSharp.Frontend;
using RiddleSharp.Semantics;

namespace RiddleSharp;

public static class Program
{
    private static string[] ReadFiles(string[] path)
    {
        return path.Select(File.ReadAllText).ToArray();
    }

    public static void Main(string[] args)
    {
        // var settings = ArgParser.Parse(args);

        // var files = ReadFiles(settings.Files);

        const string a = """
                         package main;
                         @extern
                         fun printf(fmt: char*, ...)->void;
                         @extern
                         fun fib(x: int)->int{
                            if(x<2){
                                return x;
                            }else{
                                return fib(x-1)+fib(x-2);
                            }
                         }
                         fun main(){
                            var t = fib(50);
                            printf("%d", t);
                            return;
                         }
                         """;

        var astLower = new CstLower();

        var u1 = astLower.Parse(a);


        u1 = BinaryRotate.Run(u1);

        var x = SymbolPass.Run([u1]);

        var tp = TypeInfer.Run(x);

        LlvmPass.Run(tp);
        // LgPass.Run(tp);
    }
}