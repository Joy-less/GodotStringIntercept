#pragma warning disable RSEXPERIMENTAL002 // InterceptableLocation API is experimental but stable enough for our use

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

namespace GodotStringIntercept.Generators;

// ---------------------------------------------------------------------------
// Data model
// ---------------------------------------------------------------------------

/// <summary>
/// All data needed to generate one interceptor, captured from the syntax/semantic pipeline.
/// </summary>
internal sealed class InterceptableCallSiteInfo(InterceptableLocation interceptableLocation, (string FullName, string Name) targetType, string? stringValue, Location? argumentLocation) {
    /// <summary>
    /// An opaque location token produced by <c>GetInterceptableLocation</c>.
    /// </summary>
    public InterceptableLocation InterceptableLocation { get; } = interceptableLocation;
    /// <summary>
    /// The full name of the type to convert the string to.
    /// </summary>
    public (string FullName, string Name) TargetType { get; } = targetType;
    /// <summary>
    /// The compile-time constant value of the string, or <see langword="null"/> if the string is not a compile-time constant.
    /// </summary>
    public string? ConstantStringValue { get; } = stringValue;
    /// <summary>
    /// The location of the argument expression for <see cref="ConstantStringValue"/>, used for the diagnostic.
    /// </summary>
    public Location? ArgumentLocation { get; } = argumentLocation;
}

// ---------------------------------------------------------------------------
// Diagnostics
// ---------------------------------------------------------------------------

internal static class Diagnostics {
    public static readonly DiagnosticDescriptor NonConstantString = new(
        id: "GSI001",
        title: "Non-constant string passed to 'string.AsStringName'/'string.AsNodePath'",
        messageFormat: "The argument passed to 'string.AsStringName'/'string.AsNodePath' is not a compile-time constant. " +
                       "Pass a constant string, or use 'new StringName(string)'/'new NodePath(string)' to explicitly allocate a new 'StringName'/'NodePath'.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "'string.AsStringName'/'string.AsNodePath' should receive a compile-time string constant."
    );

    public static readonly DiagnosticDescriptor CastFromString = new(
        id: "GSI002",
        title: "Cast from 'string' to 'StringName'/'NodePath' detected",
        messageFormat: "A cast from 'string' to 'StringName'/'NodePath' was detected. " +
                       "This will cause a new 'StringName'/'NodePath' to be allocated every time. " +
                       "Use 'string.AsStringName'/'string.AsNodePath' to avoid this allocation, " +
                       "or use 'new StringName(string)'/'new NodePath(string)' to explicitly allocate a new 'StringName'/'NodePath'.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Avoid casting from 'string' to 'StringName'/'NodePath', because it allocates a new 'StringName'/'NodePath' every time. " +
                     "Use 'string.AsStringName'/'string.AsNodePath' to avoid this allocation, " +
                     "or use 'new StringName(string)'/'new NodePath(string)' to explicitly allocate a new 'StringName'/'NodePath'."
    );
}

// ---------------------------------------------------------------------------
// Generator
// ---------------------------------------------------------------------------

[Generator(LanguageNames.CSharp)]
internal sealed class GodotStringInterceptGenerator : IIncrementalGenerator {
    /// <summary>
    /// Runs the generator.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // Find interceptable call sites
        IncrementalValuesProvider<InterceptableCallSiteInfo> interceptableCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsInterceptableMethodCall(node),
                transform: static (ctx, ct) => TryGetInterceptableCallSiteInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        // Generate interceptors for each interceptable call site
        context.RegisterSourceOutput(
            interceptableCallSites.Collect(),
            static (spc, sites) => GenerateSource(spc, sites));

        // Find casts from string to StringName/NodePath
        IncrementalValuesProvider<Location> castLocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => node is ArgumentSyntax,
                transform: static (ctx, ct) => TryGetCastLocation(ctx, ct))
            .Where(static loc => loc is not null)
            .Select(static (loc, ct) => loc!);

        // Warn for each cast from string to StringName/NodePath
        context.RegisterSourceOutput(
            castLocations.Collect(),
            static (spc, locations) => {
                foreach (Location location in locations) {
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.CastFromString, location));
                }
            }
        );
    }

    // -----------------------------------------------------------------------
    // Syntax predicate — fast, no semantic model
    // -----------------------------------------------------------------------

    private static bool IsInterceptableMethodCall(SyntaxNode node) {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        // Accept both `string.AsStringName()` and `AsStringName()`
        return invocation.Expression switch {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text is ("AsStringName" or "AsNodePath"),
            IdentifierNameSyntax id => id.Identifier.Text is ("AsStringName" or "AsNodePath"),
            _ => false
        };
    }

    // -----------------------------------------------------------------------
    // Pipeline transform — semantic model available
    // -----------------------------------------------------------------------

    private static InterceptableCallSiteInfo? TryGetInterceptableCallSiteInfo(GeneratorSyntaxContext context, System.Threading.CancellationToken cancellationToken) {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;
        SemanticModel semanticModel = context.SemanticModel;

        // Get IInvocationOperation from InvocationExpressionSyntax
        if (semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation invocationOp)
            return null;

        IMethodSymbol method = invocationOp.TargetMethod;

        // Ensure calling interceptable method
        if (method.Name is not ("AsStringName" or "AsNodePath")) {
            return null;
        }
        if (method.ContainingType.Name is not ("GodotStringInterceptExtensions")) {
            return null;
        }
        if (!method.IsStatic) {
            return null;
        }
        if (method.Parameters.Length != 1) {
            return null;
        }
        if (method.Parameters[0].Type.SpecialType != SpecialType.System_String) {
            return null;
        }

        // Get interceptable location from interceptable call
        InterceptableLocation? interceptableLocation = semanticModel.GetInterceptableLocation(invocation, cancellationToken);
        if (interceptableLocation is null) {
            return null;
        }

        // Check whether string value argument is a compile-time constant
        IOperation argOperation = invocationOp.Arguments[0].Value;
        Optional<object?> argConstant = argOperation.ConstantValue;
        string? stringValue = (argConstant.HasValue && argConstant.Value is string s) ? s : null;

        // Get location of string value argument
        Location? argumentLocation = stringValue is null
            ? argOperation.Syntax.GetLocation()
            : null;

        // Get the full name of the target type
        string targetTypeFullName = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string targetTypeName = method.ReturnType.Name;

        return new InterceptableCallSiteInfo(interceptableLocation, (targetTypeFullName, targetTypeName), stringValue, argumentLocation);
    }

    /// <summary>
    /// Returns the location of a cast from 'string' to 'StringName'/'NodePath' found inside an <see cref="ArgumentSyntax"/>, or <see langword="null"/> if not found.
    /// </summary>
    private static Location? TryGetCastLocation(GeneratorSyntaxContext context, System.Threading.CancellationToken cancellationToken) {
        SemanticModel semanticModel = context.SemanticModel;

        // Get IArgumentOperation from ArgumentSyntax
        if (semanticModel.GetOperation(context.Node, cancellationToken) is not IArgumentOperation argumentOp)
            return null;

        // Ensure casting from 'string' to 'StringName'/'NodePath'
        if (argumentOp.Value is not IConversionOperation conversionOp) {
            return null;
        }
        if (!conversionOp.Conversion.IsUserDefined) {
            return null;
        }
        if (conversionOp.Operand.Type?.SpecialType != SpecialType.System_String) {
            return null;
        }
        if (conversionOp.Type?.Name is not ("StringName" or "NodePath")) {
            return null;
        }

        // Check whether cast is in addons folder
        string? argumentFilePath = context.Node.GetLocation()?.SourceTree?.FilePath;
        bool isInAddonsFolder = argumentFilePath is not null && argumentFilePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals("addons", StringComparison.OrdinalIgnoreCase));
        // Ignore casts in addons folder
        if (isInAddonsFolder) {
            return null;
        }

        // Get location of the expression being cast (not the whole argument)
        return conversionOp.Operand.Syntax.GetLocation();
    }

    // -----------------------------------------------------------------------
    // Code generation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generates interceptors for each interceptable call site.
    /// </summary>
    private static void GenerateSource(SourceProductionContext context, ImmutableArray<InterceptableCallSiteInfo> interceptableCallSites) {
        if (interceptableCallSites.IsEmpty) {
            return;
        }

        // Collect interceptable call sites with constant string values
        List<InterceptableCallSiteInfo> constantInterceptableCallSites = [];
        foreach (InterceptableCallSiteInfo interceptableCallSite in interceptableCallSites) {
            // Constant string value
            if (interceptableCallSite.ConstantStringValue is not null) {
                constantInterceptableCallSites.Add(interceptableCallSite);
            }
            // Non-constant string value
            else {
                // Emit warning diagnostic
                if (interceptableCallSite.ArgumentLocation is not null) {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NonConstantString, interceptableCallSite.ArgumentLocation));
                }
            }
        }

        if (constantInterceptableCallSites.Count == 0) {
            return;
        }

        // Deduplicate cached field values
        HashSet<(string StringValue, (string FullName, string Name) TargetType)> cachedFieldValues = [];
        foreach (InterceptableCallSiteInfo interceptableCallSite in constantInterceptableCallSites) {
            cachedFieldValues.Add((interceptableCallSite.ConstantStringValue!, interceptableCallSite.TargetType));
        }

        GenerateInterceptsLocationAttribute(context);
        GenerateFieldsForValues(context, cachedFieldValues);
        GenerateInterceptors(context, constantInterceptableCallSites);
    }

    /// <summary>
    /// Emits the <c>InterceptsLocationAttribute</c> definition required by the C# interceptors feature.
    /// The BCL does not yet ship this attribute, so generators must declare it themselves.
    /// </summary>
    private static void GenerateInterceptsLocationAttribute(SourceProductionContext spc) {
        const string source = """
// <auto-generated/>
namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class InterceptsLocationAttribute : global::System.Attribute
    {
        public InterceptsLocationAttribute(int version, string data) { }
    }
}
""";
        spc.AddSource("InterceptsLocationAttribute.g.cs", source);
    }

    /// <summary>
    /// Generates the fields caching each value.
    /// </summary>
    private static void GenerateFieldsForValues(SourceProductionContext context, HashSet<(string StringValue, (string FullName, string Name) TargetType)> cachedFieldValues) {
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine();
        sb.AppendLine("namespace GodotStringIntercept.Generators;");
        sb.AppendLine();
        sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
        sb.AppendLine("internal static class GodotStringInterceptCache");
        sb.AppendLine("{");

        foreach ((string cachedFieldValue, (string FullName, string Name) targetType) in cachedFieldValues) {
            string cachedFieldName = GetFieldNameForStringValue(cachedFieldValue, targetType.Name);

            sb.AppendLine($"    internal static readonly {targetType.FullName} {cachedFieldName} = new({SymbolDisplay.FormatLiteral(cachedFieldValue, true)});");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        context.AddSource("StringNameCache.g.cs", sb.ToString());
    }

    private static void GenerateInterceptors(SourceProductionContext context, List<InterceptableCallSiteInfo> constantInterceptableCallSites) {
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace GodotInterceptors;");
        sb.AppendLine();
        sb.AppendLine("file static class StringNameCacheInterceptors");
        sb.AppendLine("{");

        // Group call sites that share the same constant string value and target type
        // so they can share a single interceptor method body (multiple [InterceptsLocation] attributes)
        Dictionary<(string ConstantStringValue, string TargetTypeFullName), List<InterceptableCallSiteInfo>> groups = [];
        foreach (InterceptableCallSiteInfo interceptableCallSite in constantInterceptableCallSites) {
            (string ConstantStringValue, string TargetTypeFullName) GroupKey = (interceptableCallSite.ConstantStringValue!, interceptableCallSite.TargetType.FullName);

            if (!groups.TryGetValue(GroupKey, out List<InterceptableCallSiteInfo> list)) {
                groups[GroupKey] = list = [];
            }
            list.Add(interceptableCallSite);
        }

        int methodIndex = 0;
        foreach (List<InterceptableCallSiteInfo> interceptableCallSites in groups.Values) {
            string cachedFieldValue = interceptableCallSites[0].ConstantStringValue!;
            (string cachedTargetTypeFullName, string cachedTargetTypeName) = interceptableCallSites[0].TargetType;
            string cachedFieldName = GetFieldNameForStringValue(cachedFieldValue, cachedTargetTypeName);

            // Emit one [InterceptsLocation] per call site in this group
            foreach (InterceptableCallSiteInfo interceptableCallSite in interceptableCallSites) {
                sb.AppendLine($"    {interceptableCallSite.InterceptableLocation.GetInterceptsLocationAttributeSyntax()}");
            }

            // Emit interceptor method
            // (Note: interceptor signature must exactly match interceptable method)
            sb.AppendLine($"    internal static {cachedTargetTypeFullName} Intercept_{methodIndex++}(this string str)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return GodotStringIntercept.Generators.GodotStringInterceptCache.{cachedFieldName};");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        context.AddSource("StringNameInterceptors.g.cs", sb.ToString());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string GetFieldNameForStringValue(string stringValue, string targetTypeName) {
        StringBuilder sb = new(targetTypeName + "_");
        foreach (char ch in stringValue) {
            sb.Append(SyntaxFacts.IsIdentifierPartCharacter(ch) ? ch : '_');
        }
        return sb.ToString();
    }
}
