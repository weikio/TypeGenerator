using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

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
        private AssemblyLoadContext _assemblyLoadContext;
        public AssemblyLoadContext LoadContext => _assemblyLoadContext;
        public Action<LogLevelEnum, string, Exception> Log { get; set; } = Logger; 

        public CodeToAssemblyGenerator(bool persist = true, string workingFolder = default, List<Assembly> assemblies = null) : this(persist, workingFolder, assemblies, null)
        {
        }
        
        public CodeToAssemblyGenerator(bool persist, string workingFolder, List<Assembly> assemblies, AssemblyLoadContext assemblyLoadContext)
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var name = entryAssembly?.GetName().Name ?? Guid.NewGuid().ToString();
            var version = entryAssembly?.GetName().Version.ToString() ?? "1.0.0";

            if (string.IsNullOrEmpty(workingFolder))
            {
                workingFolder = Path.Combine(Path.GetTempPath(), "Weikio.TypeGenerator", name, version);
            }

            _workingFolder = workingFolder;
            _assemblyLoadContext = assemblyLoadContext;

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
                    Log(LogLevelEnum.Information, "Could not make an assembly reference to " + assembly.FullName, null);
                }
                else
                {
                    if (_references.Any(
                        x => x.Display == referencePath))
                    {
                        return;
                    }

                    var metadataReference = MetadataReference.CreateFromFile(referencePath);
                    _references.Add(metadataReference);

                    foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
                    {
                        if (_assemblies.Any(x => x.GetName() == referencedAssembly))
                        {
                            continue;
                        }
                        
                        Assembly loadedAssembly;
                        if (_assemblyLoadContext != null)
                        {
                            loadedAssembly = _assemblyLoadContext.LoadFromAssemblyName(referencedAssembly);
                        }
                        else
                        {
                            loadedAssembly = Assembly.Load(referencedAssembly);
                        }

                        ReferenceAssembly(loadedAssembly);
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevelEnum.Error, $"Could not make an assembly reference to {assembly.FullName}. Try to continue without the assembly.", ex);
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
            var assemblyName = AssemblyName ?? Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
            var text = CSharpSyntaxTree.ParseText(code);
            var array = _references.ToArray();
            var syntaxTreeArray = new SyntaxTree[1] { text };

            if (!Directory.Exists(_workingFolder))
            {
                Directory.CreateDirectory(_workingFolder);
            }

            var compilation = CSharpCompilation
                .Create(assemblyName, syntaxTreeArray, array,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, false, null,
                        null, null, null, OptimizationLevel.Debug, false,
                        false, null, null, new ImmutableArray<byte>(), new bool?()));

            var fullPath = Path.Combine(_workingFolder, assemblyName);
            var assemblies = _assemblies.Where(x => !string.IsNullOrWhiteSpace(x.Location)).Select(x => x).ToList();

            if (_assemblyLoadContext == null)
            {
                _assemblyLoadContext = new CustomAssemblyLoadContext(assemblies);
            }

            if (!_persist)
            {
                using (var memoryStream = new MemoryStream())
                {
                    var emitResult = compilation.Emit(memoryStream);

                    if (!emitResult.Success)
                    {
                        ThrowError(code, emitResult);
                    }

                    memoryStream.Seek(0L, SeekOrigin.Begin);
                    var assembly = LoadContext.LoadFromStream(memoryStream);

                    return assembly;
                }
            }
            
            using (var dllStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            using (var win32resStream = compilation.CreateDefaultWin32Resources(
                true,
                false,
                null,
                null))
            {
                var emitResult = compilation.Emit(
                    dllStream,
                    pdbStream,
                    win32Resources: win32resStream);

                if (!emitResult.Success)
                {
                    ThrowError(code, emitResult);
                }
                
                File.WriteAllBytes(fullPath, dllStream.ToArray());
                
                var assembly = _assemblyLoadContext.LoadFromAssemblyPath(fullPath);

                return assembly;
            }
        }

        private static void ThrowError(string code, EmitResult emitResult)
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

        private static void Logger(LogLevelEnum logLevel, string message, Exception exception = null)
        {
            Console.WriteLine($"{logLevel}: {message}{Environment.NewLine}{Environment.NewLine}Exception: {exception}");
        }
    }
}
