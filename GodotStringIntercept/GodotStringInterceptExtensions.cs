using Godot;

namespace GodotStringIntercept;

/// <summary>
/// Contains extension methods for allocation-free string to StringName/NodePath conversions.
/// </summary>
public static class GodotStringInterceptExtensions {
    /// <summary>
    /// Returns a <see cref="StringName"/> matching the given string.
    /// <list type="bullet">
    /// <item>
    /// When called with a <b>compile-time constant</b>, the source generator
    /// intercepts this call and replaces it with a direct field read.
    /// </item>
    /// <item>
    /// When called with a <b>non-constant</b>, the source generator cannot intercept
    /// the call; a new instance is constructed on every call.
    /// </item>
    /// </list>
    /// </summary>
    public static StringName AsStringName(this string str) {
        GD.PushWarning($"The call to {nameof(AsStringName)} with '{str}' was not intercepted. Allocating a new {nameof(StringName)}.");
        return new StringName(str);
    }
    /// <summary>
    /// Returns a <see cref="NodePath"/> matching the given string.
    /// <list type="bullet">
    /// <item>
    /// When called with a <b>compile-time constant</b>, the source generator
    /// intercepts this call and replaces it with a direct field read.
    /// </item>
    /// <item>
    /// When called with a <b>non-constant</b>, the source generator cannot intercept
    /// the call; a new instance is constructed on every call.
    /// </item>
    /// </list>
    /// </summary>
    public static NodePath AsNodePath(this string str) {
        GD.PushWarning($"The call to {nameof(AsNodePath)} with '{str}' was not intercepted. Allocating a new {nameof(NodePath)}.");
        return new NodePath(str);
    }
}