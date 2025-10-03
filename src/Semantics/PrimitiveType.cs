using RiddleSharp.Frontend;

namespace RiddleSharp.Semantics;

public static class PrimitiveType
{
    public static readonly Dictionary<string, BuiltinTypeDecl> Decls = new()
    {
        ["int"] = new BuiltinTypeDecl("int"),
        ["char"] = new BuiltinTypeDecl("char"),
        ["bool"] = new BuiltinTypeDecl("bool"),
        ["float"] = new BuiltinTypeDecl("float")
    };
}