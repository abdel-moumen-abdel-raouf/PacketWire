using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using PacketWire;
using PacketWire.Security;

namespace PacketWire.Generator.Tests;

/// <summary>
/// Provides testing infrastructure to parse C# source code, execute the PacketWire source generator, compile output assemblies, and execute runtime harnesses.
/// </summary>
internal static class GeneratorTestHost
{
    /// <summary>
    /// Atomic sequence counter ensuring unique dynamic assembly names across test runs.
    /// </summary>
    private static int assemblySequence;

    /// <summary>
    /// Compiles the input source, executes the PacketWire source generator, and returns any reported diagnostics.
    /// </summary>
    /// <param name="source">The C# source text to compile.</param>
    /// <returns>An immutable array of reported generator diagnostics.</returns>
    internal static ImmutableArray<Diagnostic> Run(
        string source)
    {
        return RunWithOutput(source)
            .GeneratorDiagnostics;
    }

    /// <summary>
    /// Compiles the input source, executes the PacketWire source generator, and returns the full output compilation, diagnostics, and emitted sources.
    /// </summary>
    /// <param name="source">The C# source text to compile.</param>
    /// <returns>A populated <see cref="GeneratorTestResult"/>.</returns>
    internal static GeneratorTestResult RunWithOutput(
        string source)
    {
        SyntaxTree syntaxTree =
            CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(
                    LanguageVersion.Latest));

        string assemblyName =
            "PacketWireGeneratorTests.Dynamic." +
            Interlocked
                .Increment(ref assemblySequence)
                .ToString(
                    CultureInfo.InvariantCulture);

        CSharpCompilation compilation =
            CSharpCompilation.Create(
                assemblyName,
                syntaxTrees:
                [
                    syntaxTree
                ],
                references:
                    CreateMetadataReferences(),
                options:
                    new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        nullableContextOptions:
                            NullableContextOptions.Enable));

        ImmutableArray<Diagnostic> compilationErrors =
            compilation
                .GetDiagnostics()
                .Where(
                    static diagnostic =>
                        diagnostic.Severity
                        == DiagnosticSeverity.Error)
                .ToImmutableArray();

        if (!compilationErrors.IsEmpty)
        {
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    compilationErrors.Select(
                        static diagnostic =>
                            diagnostic.ToString())));
        }

        IIncrementalGenerator incrementalGenerator =
            new global::PacketWire.Generator.PacketWireGenerator();

        GeneratorDriver driver =
            CSharpGeneratorDriver.Create(
                incrementalGenerator.AsSourceGenerator());

        driver =
            driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out Compilation outputCompilation,
                out _);

        GeneratorDriverRunResult runResult =
            driver.GetRunResult();

        ImmutableArray<GeneratedSourceResult> generatedSources =
            runResult
                .Results
                .SelectMany(
                    static result =>
                        result.GeneratedSources)
                .ToImmutableArray();

        return new GeneratorTestResult(
            outputCompilation,
            runResult.Diagnostics,
            generatedSources);
    }

    /// <summary>
    /// Executes the generator incrementally across an initial compilation and an updated compilation to test caching and state preservation.
    /// </summary>
    /// <param name="initialSource">The initial C# source text.</param>
    /// <param name="updatedSource">The updated C# source text.</param>
    /// <returns>A <see cref="GeneratorIncrementalTestResult"/> containing both run results.</returns>
    internal static GeneratorIncrementalTestResult RunIncrementally(
        string initialSource,
        string updatedSource)
    {
        ArgumentNullException.ThrowIfNull(
            initialSource);

        ArgumentNullException.ThrowIfNull(
            updatedSource);

        CSharpParseOptions parseOptions =
            new(
                LanguageVersion.Latest);

        SyntaxTree initialSyntaxTree =
            CSharpSyntaxTree.ParseText(
                initialSource,
                parseOptions);

        SyntaxTree updatedSyntaxTree =
            initialSyntaxTree.WithChangedText(
                SourceText.From(
                    updatedSource));

        string assemblyName =
            "PacketWireGeneratorTests.Incremental." +
            Interlocked
                .Increment(ref assemblySequence)
                .ToString(
                    CultureInfo.InvariantCulture);

        MetadataReference[] references =
            CreateMetadataReferences()
                .ToArray();

        CSharpCompilationOptions compilationOptions =
            new(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions:
                    NullableContextOptions.Enable);

        CSharpCompilation initialCompilation =
            CSharpCompilation.Create(
                assemblyName,
                syntaxTrees:
                [
                    initialSyntaxTree
                ],
                references:
                    references,
                options:
                    compilationOptions);

        CSharpCompilation updatedCompilation =
            CSharpCompilation.Create(
                assemblyName,
                syntaxTrees:
                [
                    updatedSyntaxTree
                ],
                references:
                    references,
                options:
                    compilationOptions);

        EnsureNoCompilationErrors(
            initialCompilation,
            "initial");

        EnsureNoCompilationErrors(
            updatedCompilation,
            "updated");

        IIncrementalGenerator incrementalGenerator =
            new global::PacketWire.Generator.PacketWireGenerator();

        GeneratorDriver driver =
            CSharpGeneratorDriver.Create(
                incrementalGenerator.AsSourceGenerator());

        driver =
            driver.RunGeneratorsAndUpdateCompilation(
                initialCompilation,
                out Compilation initialOutputCompilation,
                out _);

        GeneratorDriverRunResult initialRunResult =
            driver.GetRunResult();

        ImmutableArray<GeneratedSourceResult> initialGeneratedSources =
            initialRunResult
                .Results
                .SelectMany(
                    static result =>
                        result.GeneratedSources)
                .ToImmutableArray();

        GeneratorTestResult initialResult =
            new(
                initialOutputCompilation,
                initialRunResult.Diagnostics,
                initialGeneratedSources);

        driver =
            driver.RunGeneratorsAndUpdateCompilation(
                updatedCompilation,
                out Compilation updatedOutputCompilation,
                out _);

        GeneratorDriverRunResult updatedRunResult =
            driver.GetRunResult();

        ImmutableArray<GeneratedSourceResult> updatedGeneratedSources =
            updatedRunResult
                .Results
                .SelectMany(
                    static result =>
                        result.GeneratedSources)
                .ToImmutableArray();

        GeneratorTestResult updatedResult =
            new(
                updatedOutputCompilation,
                updatedRunResult.Diagnostics,
                updatedGeneratedSources);

        return new GeneratorIncrementalTestResult(
            initialResult,
            updatedResult);

        static void EnsureNoCompilationErrors(
            Compilation compilation,
            string stage)
        {
            ImmutableArray<Diagnostic> errors =
                compilation
                    .GetDiagnostics()
                    .Where(
                        static diagnostic =>
                            diagnostic.Severity
                            == DiagnosticSeverity.Error)
                    .ToImmutableArray();

            if (errors.IsEmpty)
            {
                return;
            }

            throw new InvalidOperationException(
                $"The {stage} incremental input compilation contains errors:" +
                Environment.NewLine +
                string.Join(
                    Environment.NewLine,
                    errors.Select(
                        static diagnostic =>
                            diagnostic.ToString())));
        }
    }

    /// <summary>
    /// Compiles an execution harness into the generated assembly and invokes a parameterless static method via reflection.
    /// </summary>
    /// <typeparam name="TResult">The expected return type of the harness method.</typeparam>
    /// <param name="result">The generator test result containing the output compilation.</param>
    /// <param name="harnessSource">The C# source code for the execution harness.</param>
    /// <param name="fullyQualifiedHarnessTypeName">The fully qualified name of the harness class.</param>
    /// <param name="methodName">The name of the static method to invoke.</param>
    /// <returns>The result returned by the invoked harness method.</returns>
    internal static TResult InvokeHarness<TResult>(
        GeneratorTestResult result,
        string harnessSource,
        string fullyQualifiedHarnessTypeName,
        string methodName)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(harnessSource);
        ArgumentNullException.ThrowIfNull(
            fullyQualifiedHarnessTypeName);

        ArgumentNullException.ThrowIfNull(methodName);

        SyntaxTree harnessSyntaxTree =
            CSharpSyntaxTree.ParseText(
                harnessSource,
                new CSharpParseOptions(
                    LanguageVersion.Latest));

        string assemblyName =
            "PacketWireGeneratorTests.Execution." +
            Interlocked
                .Increment(ref assemblySequence)
                .ToString(
                    CultureInfo.InvariantCulture);

        Compilation compilation =
            result.OutputCompilation
                .WithAssemblyName(assemblyName)
                .AddSyntaxTrees(
                    harnessSyntaxTree);

        using MemoryStream assemblyStream =
            new();

        EmitResult emitResult =
            compilation.Emit(
                assemblyStream);

        if (!emitResult.Success)
        {
            ImmutableArray<Diagnostic> errors =
                emitResult.Diagnostics
                    .Where(
                        static diagnostic =>
                            diagnostic.Severity
                            == DiagnosticSeverity.Error)
                    .ToImmutableArray();

            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    errors.Select(
                        static diagnostic =>
                            diagnostic.ToString())));
        }

        Assembly assembly =
            Assembly.Load(
                assemblyStream.ToArray());

        Type harnessType =
            assembly.GetType(
                fullyQualifiedHarnessTypeName,
                throwOnError: true,
                ignoreCase: false)
            ?? throw new InvalidOperationException(
                $"Harness type '{fullyQualifiedHarnessTypeName}' was not found.");

        MethodInfo method =
            harnessType.GetMethod(
                methodName,
                BindingFlags.Public |
                BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Harness method '{methodName}' was not found.");

        object? invocationResult =
            method.Invoke(
                obj: null,
                parameters: null);

        if (invocationResult is TResult typedResult)
        {
            return typedResult;
        }

        throw new InvalidOperationException(
            $"Harness method '{methodName}' did not return the expected type '{typeof(TResult).FullName}'.");
    }

    /// <summary>
    /// Enumerates metadata references for BCL runtime assemblies and PacketWire dependencies required for test compilation.
    /// </summary>
    /// <returns>A sequence of <see cref="MetadataReference"/> instances.</returns>
    private static IEnumerable<MetadataReference>
        CreateMetadataReferences()
    {
        string trustedPlatformAssemblies =
            AppContext.GetData(
                "TRUSTED_PLATFORM_ASSEMBLIES")
            as string
            ?? throw new InvalidOperationException(
                "Trusted platform assemblies were not available.");

        foreach (
            string assemblyPath
            in trustedPlatformAssemblies.Split(
                Path.PathSeparator))
        {
            yield return
                MetadataReference.CreateFromFile(
                    assemblyPath);
        }

        yield return
            MetadataReference.CreateFromFile(
                typeof(PacketAttribute)
                    .Assembly
                    .Location);

        yield return
            MetadataReference.CreateFromFile(
                typeof(PacketWriter)
                    .Assembly
                    .Location);

        yield return
            MetadataReference.CreateFromFile(
                typeof(IPayloadProtector)
                    .Assembly
                    .Location);

        yield return
            MetadataReference.CreateFromFile(
                typeof(AesGcmPayloadProtector)
                    .Assembly
                    .Location);
    }
}
