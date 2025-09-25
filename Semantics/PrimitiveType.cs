using RiddleSharp.Frontend;

namespace RiddleSharp.Semantics;

public static class PrimitiveType
{
    public static Dictionary<string, BuiltinTypeDecl> Decls = new()
    {
        ["int"] = new BuiltinTypeDecl("int"),
        ["bool"] = new BuiltinTypeDecl("bool"),
        ["float"] = new BuiltinTypeDecl("float")
    };
}