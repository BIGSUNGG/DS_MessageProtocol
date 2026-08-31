using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using MessageProtocol;
using MessageProtocol.CodeGenerator.Generate;
using MessageProtocol.Serialize;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MessageProtocol.Tests.Generator
{
    internal static class GeneratorTestHelper
    {
        public static (GeneratorDriverRunResult RunResult, Compilation OutputCompilation, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(string source)
        {
            var inputCompilation = CreateCompilation(source);
            var generator = new MessageCodeGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            return (driver.GetRunResult(), outputCompilation, diagnostics);
        }

        public static void AssertNoCompilationErrors(Compilation compilation)
        {
            var errors = compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.Empty(errors);
        }

        static Compilation CreateCompilation(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path))
                .Concat(new[]
                {
            MetadataReference.CreateFromFile(typeof(GroupRootMessageAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(MessageSerializer).Assembly.Location),
                })
                .GroupBy(reference => reference.Display, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Cast<MetadataReference>()
                .ToArray();

            return CSharpCompilation.Create(
                assemblyName: "compilation",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }
}
