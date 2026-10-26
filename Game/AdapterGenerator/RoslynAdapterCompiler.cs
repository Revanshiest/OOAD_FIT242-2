using System.Collections.Frozen;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Game.AdapterGenerator;

public sealed class RoslynAdapterCompiler : IAdapterCompiler
{
    private static readonly FrozenSet<string> RequiredRuntimeAssemblies = new[]
    {
        "System.Runtime",
        "System.Collections",
        "System.Linq",
        "System.Private.CoreLib",
        "netstandard",
        "System.ObjectModel"
    }.ToFrozenSet();

    public Assembly Compile(string sourceCode, IEnumerable<Assembly> sourceAssemblies)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var references = CollectReferences(sourceAssemblies);
        var compilation = CreateCompilation(syntaxTree, references);

        return EmitAndLoad(compilation, sourceCode);
    }

    private static CSharpCompilation CreateCompilation(SyntaxTree syntaxTree, List<MetadataReference> references)
    {
        return CSharpCompilation.Create(
            assemblyName: $"GeneratedAdapters_{Guid.NewGuid():N}",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static Assembly EmitAndLoad(CSharpCompilation compilation, string sourceCode)
    {
        using var memoryStream = new MemoryStream();
        var emitResult = compilation.Emit(memoryStream);

        return emitResult.Success
            ? LoadAssemblyFromStream(memoryStream)
            : throw new InvalidOperationException(FormatCompilationErrors(emitResult.Diagnostics, sourceCode));
    }

    private static Assembly LoadAssemblyFromStream(MemoryStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);

        return Assembly.Load(stream.ToArray());
    }

    private static string FormatCompilationErrors(IEnumerable<Diagnostic> diagnostics, string sourceCode)
    {
        var errors = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString());

        return $"Adapter compilation failed:\n{string.Join("\n", errors)}\n\nSource code:\n{sourceCode}";
    }

    private static List<MetadataReference> CollectReferences(IEnumerable<Assembly> sourceAssemblies)
    {
        var allAssemblies = CollectAllAssemblies(sourceAssemblies);

        return GetRuntimeReferencePaths()
            .Concat(GetProjectReferencePaths(allAssemblies))
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    }

    private static HashSet<Assembly> CollectAllAssemblies(IEnumerable<Assembly> sourceAssemblies)
    {
        var coreAssemblies = new[] { typeof(Ioc).Assembly, typeof(ICommand).Assembly };

        return sourceAssemblies
            .SelectMany(assembly => LoadReferencedAssemblies(assembly).Prepend(assembly))
            .Concat(coreAssemblies)
            .ToHashSet();
    }

    private static IEnumerable<Assembly> LoadReferencedAssemblies(Assembly assembly)
    {
        return assembly.GetReferencedAssemblies().SelectMany(TryLoadAssembly);
    }

    private static IEnumerable<Assembly> TryLoadAssembly(AssemblyName name)
    {
        try { return new[] { Assembly.Load(name) }; }
        catch { return Enumerable.Empty<Assembly>(); }
    }

    private static IEnumerable<string> GetRuntimeReferencePaths()
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator) ?? Array.Empty<string>();

        return trustedAssemblies
            .Where(path => RequiredRuntimeAssemblies.Contains(Path.GetFileNameWithoutExtension(path)));
    }

    private static IEnumerable<string> GetProjectReferencePaths(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => assembly.Location);
    }
}
