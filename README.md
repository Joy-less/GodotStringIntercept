# Godot String Intercept

[![NuGet](https://img.shields.io/nuget/v/GodotStringIntercept.svg)](https://www.nuget.org/packages/GodotStringIntercept)

Generators for `StringName` and `NodePath` in Godot 4.

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

This library also warns whenever you implicitly or explicitly cast a `string` to a `StringName`/`NodePath`.
You should use `new StringName()`/`new NodePath()` instead to explicitly allocate a new object.
The warnings are disabled for code in an `addons` folder.

See the [proposal](https://github.com/godotengine/godot-proposals/discussions/10826) for more info.
