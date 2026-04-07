# Godot String Intercept

Generates `StringName`s/`NodePath`s for Godot 4.

## Usage

```cs
// Allocates a new StringName every time.
Input.IsActionPressed("move_forward");

// Allocation free!
Input.IsActionPressed("move_forward".AsStringName());
```

You must add the following to your `.csproj` file:
```csproj
<PropertyGroup>
    <InterceptorsNamespaces>$(InterceptorsNamespaces);GodotInterceptors</InterceptorsNamespaces>
</PropertyGroup>
```

## How?

This library generates readonly fields for the values passed to `AsStringName()`/`AsNodePath()`.
The calls to those methods are then [intercepted](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md) to simply read the readonly field.

More info: https://github.com/godotengine/godot-proposals/discussions/10826
