using Godot;

namespace GodotStringIntercept;

/// <summary>
/// Contains extension methods for allocation-free string to StringName/NodePath conversions.
/// </summary>
public static class GodotStringInterceptExtensions {
    /// <summary>
    /// Returns a <see cref="StringName"/> created from the given string.
    /// <para>
    /// When called with a <b>compile-time constant</b>, the source generator
    /// intercepts this call and replaces it with a direct field read.
    /// </para>
    /// <para>
    /// When called with a <b>non-constant</b>, the source generator cannot intercept
    /// the call; a new instance is constructed on every call.
    /// </para>
    /// </summary>
    public static StringName AsStringName(this string str) {
        GD.PushWarning($"The call to {nameof(AsStringName)} was not intercepted. A new {nameof(StringName)} will be allocated.");
        return new StringName(str);
    }
    /// <summary>
    /// Returns a <see cref="NodePath"/> created from the given string.
    /// <para>
    /// When called with a <b>compile-time constant</b>, the source generator
    /// intercepts this call and replaces it with a direct field read.
    /// </para>
    /// <para>
    /// When called with a <b>non-constant</b>, the source generator cannot intercept
    /// the call; a new instance is constructed on every call.
    /// </para>
    /// </summary>
    public static NodePath AsNodePath(this string str) {
        GD.PushWarning($"The call to {nameof(AsNodePath)} was not intercepted. A new {nameof(NodePath)} will be allocated.");
        return new NodePath(str);
    }
}