using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using log4net;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace WvsBeta.Game
{
    public class Scripting
    {
        private static ILog _log = LogManager.GetLogger(typeof(Scripting));

        private class CollectibleAssemblyLoadContext : AssemblyLoadContext, IDisposable
        {
            public CollectibleAssemblyLoadContext() : base(true)
            { }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                return null;
            }

            public void Dispose()
            {
                Unload();
            }
        }

        static CollectibleAssemblyLoadContext x = new CollectibleAssemblyLoadContext();
        private static CSharpCompilation compiler = CSharpCompilation.CreateScriptCompilation("temp_compiler");

        static Scripting()
        {
            var currentExecutable = Assembly.GetExecutingAssembly().Location;
            var mainPath = Path.GetDirectoryName(currentExecutable);

            var assemblyLocation = typeof(object).Assembly.Location;
            var sdkPath = Path.GetDirectoryName(assemblyLocation);

            var references = new List<MetadataReference>();

            references.AddRange(new [] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(sdkPath, "System.Private.CoreLib.dll")),
                MetadataReference.CreateFromFile(Path.Combine(sdkPath, "System.Console.dll")),
                MetadataReference.CreateFromFile(Path.Combine(sdkPath, "System.Runtime.dll")),
                #if DEBUG
                // We don't want file IO in production (For exploitation reasons)
                MetadataReference.CreateFromFile(Path.Combine(sdkPath, "System.IO.FileSystem.dll")),
                #endif
                MetadataReference.CreateFromFile(Path.Combine(sdkPath, "System.Collections.dll")),
                MetadataReference.CreateFromFile(Path.Combine(sdkPath, "System.Linq.dll"))
            });

            foreach (var r in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
            {
                if (File.Exists(Path.Combine(mainPath, r.Name + ".dll")))
                    references.Add(MetadataReference.CreateFromFile(Path.Combine(mainPath, r.Name + ".dll")));
                //else
                //    references.Add(MetadataReference.CreateFromFile(Path.Join(sdkPath, r.Name + ".dll")));
            }

            references.Add(MetadataReference.CreateFromFile(currentExecutable));

            compiler = compiler.AddReferences(references);
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
                    .Emit(peStream ,pdbStream: pdbStream);
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

            _log.Debug($"Unable to find interface {string.Join(", ", ifaces.Select(x => x.Name))} for dll {DLL.FullName}");
            return null;
        }

        public static T CreateClone<T>(T x)
        {
            return (T)x.GetType().GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(x, null);
        }
    }
}