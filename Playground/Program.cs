using Godot;
using GodotStringIntercept;

// User Code
public class Program {
    public static void Main() {
        LogHeader(
            "Implicit/explicit cast from string to StringName/NodePath (when passing to method)",
            "Displays warning from analyzer"
        );
        MethodAcceptingStringName("test1", 67);
        MethodAcceptingNodePath("test1", 67);
        MethodAcceptingStringName((StringName)"test2", 67);
        MethodAcceptingNodePath((NodePath)"test2", 67);

        LogHeader(
            "Construction from string to StringName/NodePath (when passing to method)",
            "OK"
        );
        MethodAcceptingStringName(new StringName("test3"), 67);
        MethodAcceptingNodePath(new NodePath("test3"), 67);

        LogHeader(
            "Extension method conversion from string to StringName/NodePath",
            "OK"
        );
        StringName test4_SN = "test4".AsStringName();
        NodePath test4_NP = "test4".AsNodePath();
        StringName test5_SN = GodotStringInterceptExtensions.AsStringName("test5");
        NodePath test5_NP = GodotStringInterceptExtensions.AsNodePath("test5");

        LogHeader(
            "Extension method conversion from non-constant string to StringName/NodePath",
            "Displays warning from analyzer"
        );
        StringName test6_SN = ("test" + new string('6', 1)).AsStringName();
        NodePath test6_NP = ("test" + new string('6', 1)).AsNodePath();
        StringName test7_SN = GodotStringInterceptExtensions.AsStringName("test" + new string('7', 1));
        NodePath test7_NP = GodotStringInterceptExtensions.AsNodePath("test" + new string('7', 1));
    }

    private static void MethodAcceptingStringName(StringName stringName, int otherParam) {
        _ = stringName;
        _ = otherParam;
    }
    private static void MethodAcceptingNodePath(NodePath nodePath, int otherParam) {
        _ = nodePath;
        _ = otherParam;
    }

    private static void LogHeader(params IEnumerable<string> lines) {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine();
        foreach (string line in lines) {
            Console.WriteLine("> " + line + " <");
        }
        Console.WriteLine();
        Console.ResetColor();
    }
}