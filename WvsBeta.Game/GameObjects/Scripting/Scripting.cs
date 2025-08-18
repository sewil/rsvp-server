using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using System.Text;
using Basic.Reference.Assemblies;
using log4net;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace WvsBeta.Game
{
    public class Scripting
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Scripting));

        private class CollectibleAssemblyLoadContext : AssemblyLoadContext, IDisposable
        {
            public CollectibleAssemblyLoadContext() : base(true)
            {
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                return null;
            }

            public void Dispose()
            {
                Unload();
            }
        }

        static CollectibleAssemblyLoadContext x = new();
        private static CSharpCompilation compiler = CSharpCompilation.CreateScriptCompilation("temp_compiler");

        static Scripting()
        {
            var references = new List<MetadataReference>();

            if (false)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    if (!assembly.IsDynamic && assembly.Location != "" && assembly.FullName != null)
                    {
                        if (assembly.FullName.Contains("WvsBeta.") || assembly.FullName.Contains("System"))
                        {
                            _log.Debug(
                                $"Adding assembly {assembly.FullName} ({assembly.GetName()}) @ {assembly.Location} to roslyn");
                            references.Add(MetadataReference.CreateFromFile(assembly.Location));
                        }
                    }
                }
            }

            references.AddRange(LoadRuntimeLibs());

            compiler = compiler.AddReferences(references);
        }

        // https://github.com/dotnet/runtime/issues/36590#issuecomment-689883856
        static IEnumerable<MetadataReference> LoadRuntimeLibs()
        {
            var domainAssemblys = AppDomain.CurrentDomain.GetAssemblies();
            var metadataReferenceList = new List<MetadataReference>();

            foreach (var assembl in domainAssemblys)
            {   
                unsafe
                {
                    assembl.TryGetRawMetadata(out byte* blob, out int length);                    
                    var moduleMetadata = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
                    var assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
                    var metadataReference = assemblyMetadata.GetReference();
                    metadataReferenceList.Add(metadataReference);
                }
            }
            unsafe
            {
                // Add extra refs
                typeof(System.Linq.Expressions.Expression).Assembly.TryGetRawMetadata(out byte* blob, out int length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
            }

            return metadataReferenceList;
        }


        // https://github.com/dotnet/runtime/issues/36590#issuecomment-689883856
        static MetadataReference GetReference(Type type)
        {
            unsafe
            {
                return type.Assembly.TryGetRawMetadata(out var blob, out var length)
                    ? AssemblyMetadata
                        .Create(ModuleMetadata.CreateFromMetadata((IntPtr) blob, length))
                        .GetReference()
                    : throw new InvalidOperationException($"Could not get raw metadata for type {type}");
            }
        }


        public static CompilerResults CompileScriptRoslyn(string path)
        {
            var cr = new CompilerResults(new TempFileCollection());

            var source = File.ReadAllText(path);

            var syntaxTree = CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(kind: SourceCodeKind.Script),
                path: path,
                encoding: Encoding.UTF8
            );

            var optimizationLevel = OptimizationLevel.Release;
#if DEBUG
            optimizationLevel = OptimizationLevel.Debug;
#endif

            using var peStream = new MemoryStream();
            using var pdbStream = new MemoryStream();

            var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);

            EmitResult result;
            lock (compiler)
            {
                result = compiler
                    .RemoveAllSyntaxTrees()
                    .AddSyntaxTrees(syntaxTree)
                    .WithOptions(new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        moduleName: Path.GetFileName(path),
                        optimizationLevel: optimizationLevel
                    ))
                    .WithAssemblyName(Path.GetFileName(path) + "_" + MasterThread.CurrentTime)
                    .Emit(peStream, pdbStream: pdbStream, options: emitOptions);
            }

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (var diagnostic in failures)
                {
                    var loc = diagnostic.Location;

                    FileLinePositionSpan lineSpan;
                    if (loc != Location.None)
                        lineSpan = loc.SourceTree.GetLineSpan(loc.SourceSpan);
                    else
                        lineSpan = new FileLinePositionSpan("", LinePosition.Zero, LinePosition.Zero);

                    var ce = new CompilerError(
                        path,
                        lineSpan.StartLinePosition.Line,
                        lineSpan.StartLinePosition.Character,
                        diagnostic.Id,
                        diagnostic.GetMessage()
                    )
                    {
                        IsWarning = diagnostic.WarningLevel > 0
                    };

                    cr.Errors.Add(ce);
                }
            }
            else
            {
                peStream.Seek(0, SeekOrigin.Begin);
                pdbStream.Seek(0, SeekOrigin.Begin);
                cr.CompiledAssembly = x.LoadFromStream(peStream, pdbStream);
            }

            return cr;
        }

        public static object FindInterfaceImplementor(Assembly DLL, params Type[] ifaces)
        {
            foreach (var iface in ifaces)
            {
                var InterfaceName = iface.Name;

                // Loop through types looking for one that implements the given interface
                foreach (var t in DLL.GetTypes())
                {
                    if (t.GetInterface(InterfaceName, true) != null ||
                        t.IsSubclassOf(iface))
                        return DLL.CreateInstance(t.FullName);
                }
            }

            _log.Debug(
                $"Unable to find interface {string.Join(", ", ifaces.Select(x => x.Name))} for dll {DLL.FullName}");
            return null;
        }

        public static T CreateClone<T>(T x)
        {
            return (T) x.GetType().GetMethod("MemberwiseClone",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(x, null);
        }
    }
}