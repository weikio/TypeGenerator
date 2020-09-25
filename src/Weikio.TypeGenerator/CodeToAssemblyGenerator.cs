using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Weikio.TypeGenerator
{
    /// <summary>
    ///     Note: Heavily based on the work done by Jeremy Miller in Lamar: https://github.com/jasperfx/Lamar
    /// </summary>
    public class CodeToAssemblyGenerator
    {
        private readonly IList<Assembly> _assemblies = new List<Assembly>();
        private readonly IList<MetadataReference> _references = new List<MetadataReference>();
        private readonly string _workingFolder;
        private readonly bool _persist;
        
        public CodeToAssemblyGenerator(bool persist = true, string workingFolder = default, List<Assembly> assemblies = null)
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var name = entryAssembly?.GetName().Name ?? Guid.NewGuid().ToString();
            var version = entryAssembly?.GetName().Version.ToString() ?? "1.0.0";

            if (string.IsNullOrEmpty(workingFolder))
            {
                workingFolder = Path.Combine(Path.GetTempPath(), "Weikio.TypeGenerator", name, version);
            }
            
            _workingFolder = workingFolder;

            _persist = persist;

            if (assemblies?.Any() == true)
            {
                foreach (var assembly in assemblies)
                {
                    ReferenceAssembly(assembly);
                }
            }
            
            ReferenceAssemblyContainingType<object>();
            ReferenceAssembly(typeof(Enumerable).GetTypeInfo().Assembly);
        }

        public string AssemblyName { get; set; }

        public void ReferenceAssembly(Assembly assembly)
        {
            if (assembly == null || _assemblies.Contains(assembly))
            {
                return;
            }

            _assemblies.Add(assembly);

            try
            {
                var referencePath = CreateAssemblyReference(assembly);

                if (referencePath == null)
                {
                    Console.WriteLine("Could not make an assembly reference to " + assembly.FullName);
                }
                else
                {
                    if (_references.Any(
                        x => x.Display == referencePath))
                    {
                        return;
                    }

                    _references.Add(MetadataReference.CreateFromFile(referencePath));

                    foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
                    {
                        ReferenceAssembly(Assembly.Load(referencedAssembly));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not make an assembly reference to {assembly.FullName}\n\n{ex}");
            }
        }

        private static string CreateAssemblyReference(Assembly assembly)
        {
            if (assembly.IsDynamic)
            {
                return null;
            }

            return assembly.Location;
        }

        public void ReferenceAssemblyContainingType<T>()
        {
            ReferenceAssembly(typeof(T).GetTypeInfo().Assembly);
        }

        public Assembly GenerateAssembly(string code)
        {
            var assemblyName = AssemblyName ?? Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + ".dll";
            var text = CSharpSyntaxTree.ParseText(code);
            var array = _references.ToArray();
            var syntaxTreeArray = new SyntaxTree[1] { text };

            if (!Directory.Exists(_workingFolder))
            {
                Directory.CreateDirectory(_workingFolder);
            }

            var fullPath = Path.Combine(_workingFolder, assemblyName);
            
            using (var memoryStream = new MemoryStream())
            {
                var emitResult = CSharpCompilation
                    .Create(assemblyName, syntaxTreeArray, array,
                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, false, null,
                            null, null, null, OptimizationLevel.Debug, false,
                            false, null, null, new ImmutableArray<byte>(), new bool?())).Emit(memoryStream);

                if (!emitResult.Success)
                {
                    var errors = emitResult.Diagnostics
                        .Where(diagnostic =>
                            diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                    var errorsMsg = string.Join("\n", errors.Select(x => x.Id + ": " + x.GetMessage()));

                    var errorMsgWithCode = "Compilation failures!" + Environment.NewLine + Environment.NewLine +
                                           errorsMsg + Environment.NewLine + Environment.NewLine + "Code:" +
                                           Environment.NewLine + Environment.NewLine + code;

                    throw new InvalidOperationException(errorMsgWithCode);
                }

                memoryStream.Seek(0L, SeekOrigin.Begin);

                var assemblyPaths = _assemblies.Where(x => !string.IsNullOrWhiteSpace(x.Location)).Select(x => x).ToList();
                var loadContext = new CustomAssemblyLoadContext(assemblyPaths);

                if (_persist)
                {
                    using (var file = new FileStream(fullPath, FileMode.Create, System.IO.FileAccess.Write))
                    {
                        var bytes = new byte[memoryStream.Length];
                        memoryStream.Read(bytes, 0, (int) memoryStream.Length);
                        file.Write(bytes, 0, bytes.Length);

                        memoryStream.Close();
                    }

                    var assembly = loadContext.LoadFromAssemblyPath(fullPath);

                    return assembly;
                }
                else
                {
                    var assembly = loadContext.LoadFromStream(memoryStream);

                    return assembly;
                }
            }
        }
    }
}
