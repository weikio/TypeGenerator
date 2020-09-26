using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Weikio.TypeGenerator.Delegates;

namespace Weikio.TypeGenerator.Types
{
    public class TypeToTypeWrapper
    {
        public Type CreateType(Type originalType, TypeToTypeWrapperOptions options = default)
        {
            if (originalType == null)
            {
                throw new ArgumentNullException(nameof(originalType));
            }

            if (options == null)
            {
                options = new TypeToTypeWrapperOptions();
            }

            var methods = GetMethodsToWrap(originalType, options);

            var allTypes = new List<Type> { originalType };

            foreach (var methodInfo in methods)
            {
                var parameters = methodInfo.GetParameters();
                var returnType = methodInfo.ReturnType;

                var requiredTypesForMethod = GetRequiredTypes(parameters, returnType);

                foreach (var type in requiredTypesForMethod)
                {
                    if (allTypes.Contains(type))
                    {
                        continue;
                    }

                    allTypes.Add(type);
                }
            }

            var id = TypeCache.Add(originalType, options);

            var generator = new CodeToAssemblyGenerator();
            AddReferences(generator, allTypes, options, originalType);

            var code = new StringBuilder();
            AddNamespaces(code, allTypes);

            var constructorParameterNames = new List<string>();
            var constructorParameterNamesWithTypes = new List<string>();
            var constructorFieldNamesWithTypes = new List<string>();
            var propertyNamesWithTypes = new List<string>();

            var conversionRules = options?.ConversionRules;

            if (conversionRules == null)
            {
                conversionRules = new List<ParameterConversionRule>();
            }

            var d = new Dictionary<MethodInfo, (List<string>, List<string>)>();

            foreach (var methodInfo in methods)
            {
                var parameters = methodInfo.GetParameters();

                var methodParameterNamesWithTypes = new List<string>();
                var delegateMethodParameters = new List<string>();

                DoConversions(parameters, conversionRules, constructorParameterNames, constructorParameterNamesWithTypes, constructorFieldNamesWithTypes,
                    propertyNamesWithTypes, delegateMethodParameters, methodParameterNamesWithTypes, options, originalType);

                d.Add(methodInfo, (delegateMethodParameters, methodParameterNamesWithTypes));
            }

            if (options.AdditionalConstructorParameters?.Any() == true)
            {
                foreach (var additionalConstructorParameter in options.AdditionalConstructorParameters)
                {
                    AddConstructorParameter(additionalConstructorParameter, constructorParameterNames, constructorParameterNamesWithTypes,
                        constructorFieldNamesWithTypes, options, originalType);
                }
            }

            code.AppendLine();
            code.AppendLine($"namespace {GetNamespace(options, originalType)}");
            code.AppendLine("{");
            code.AppendLine($"public class {GetTypeName(options, originalType)}");
            code.AppendLine("{");

            CreateInstance(originalType, code, id);
            CreateConstructor(options, constructorParameterNames, constructorFieldNamesWithTypes, code, constructorParameterNamesWithTypes, originalType, id);
            CreateProperties(propertyNamesWithTypes, code);

            foreach (var methodInfo in methods)
            {
                var parameters = methodInfo.GetParameters();
                var returnType = methodInfo.ReturnType;

                var p = d[methodInfo];
                CreateWrapperMethod(options, returnType, code, p.Item2, id, p.Item1, methodInfo, originalType);
            }

            if (options.CustomCodeGenerator != null)
            {
                code.AppendLine("// Custom code begins");
                code.AppendLine(options.CustomCodeGenerator(options, originalType));
                code.AppendLine("// Custom code ends");
            }

            if (options.IsSourceCodeIncluded)
            {
                code.AppendLine("##sourceplaceholder##");
            }

            code.AppendLine("}"); // Close class
            code.AppendLine("}"); // Close namespace

            var fullCode = code.ToString();

            if (options.IsSourceCodePrettified)
            {
                fullCode = CSharpSyntaxTree.ParseText(fullCode).GetRoot().NormalizeWhitespace().ToFullString();
            }
            
            if (options.IsSourceCodeIncluded)
            {
                code.AppendLine("// Source code begins");
                fullCode = fullCode.Replace("##sourceplaceholder##", $"private string _source = @\"{Environment.NewLine}{fullCode.Replace("\"", "\"\"").Replace("##sourceplaceholder##", "")}\";");
                code.AppendLine("// Source code ends");
            }

            var assembly = generator.GenerateAssembly(fullCode);

            var result = assembly.GetExportedTypes().Single();

            return result;
        }

        private void AddConstructorParameter(AdditionalParameter additionalConstructorParameter, List<string> constructorParameterNames, List<string> constructorParameterNamesWithTypes, List<string> constructorFieldNamesWithTypes, TypeToTypeWrapperOptions options, Type originalType)
        {
            constructorParameterNames.Add(additionalConstructorParameter.Name);
            constructorParameterNamesWithTypes.Add($"{GetFriendlyName(additionalConstructorParameter.Type, GetTypeName(options, originalType))} {additionalConstructorParameter.Name}");

            var fieldName = $"_{additionalConstructorParameter.Name}";
            constructorFieldNamesWithTypes.Add($"{GetFriendlyName(additionalConstructorParameter.Type, GetTypeName(options, originalType))} {fieldName}");
        }

        private List<MethodInfo> GetMethodsToWrap(Type originalType, TypeToTypeWrapperOptions options)
        {
            var result = new List<MethodInfo>();

            var allTypeMethods = originalType.GetMethods();

            foreach (var methodInfo in allTypeMethods)
            {
                if (options.IncludeMethod(options, originalType, methodInfo) == false)
                {
                    continue;
                }

                if (options.IncludedMethods?.Any() != true)
                {
                    result.Add(methodInfo);

                    continue;
                }

                if (options.IncludedMethods?.Contains(methodInfo.Name) == true)
                {
                    result.Add(methodInfo);

                    continue;
                }

                foreach (var includedMethodName in options.IncludedMethods)
                {
                    var regEx = NameToRegex(includedMethodName);

                    if (regEx.IsMatch(methodInfo.Name) == false)
                    {
                        continue;
                    }

                    result.Add(methodInfo);

                    break;
                }
            }

            return result;
        }

        private static void CreateWrapperMethod(TypeToTypeWrapperOptions options, Type returnType, StringBuilder code,
            List<string> methodParameterNamesWithTypes,
            Guid id, List<string> delegateMethodParameters, MethodInfo methodInfo, Type originalType)
        {
            var originalMethodName = methodInfo.Name;
            var generatedMethodName = options.MethodNameGenerator(options, originalType, methodInfo);

            if (typeof(void) != returnType)
            {
                code.AppendLine(
                    $"public {GetFriendlyName(returnType, GetTypeName(options, originalType))} {generatedMethodName} ({string.Join(", ", methodParameterNamesWithTypes)})");
            }
            else
            {
                code.AppendLine(
                    $"public void {generatedMethodName} ({string.Join(", ", methodParameterNamesWithTypes)})");
            }

            code.AppendLine("{");

            if (options.OnBeforeMethod != null)
            {
                code.AppendLine(
                    $"Weikio.TypeGenerator.Types.TypeCache.Details(System.Guid.Parse(\"{id.ToString()}\")).OnBeforeMethod.DynamicInvoke(_instance, _instance.GetType().GetMethod(\"{originalMethodName}\", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));");
            }
            
            if (options.OnBeforeMethodCustomCodeGenerator != null)
            {
                code.AppendLine("// Custom before method code begins");

                code.AppendLine(options.OnBeforeMethodCustomCodeGenerator(options, originalType, methodInfo));
                
                code.AppendLine("// Custom before method code ends");
            }

            if (typeof(void) != returnType)
            {
                code.AppendLine(
                    $"var result = ({GetFriendlyName(returnType, GetTypeName(options, originalType))}) _instance.{originalMethodName}({string.Join(", ", delegateMethodParameters)});");
            }
            else
            {
                code.AppendLine($"_instance.{originalMethodName}({string.Join(", ", delegateMethodParameters)});");
            }

            if (options.OnAfterMethod != null)
            {
                code.AppendLine(
                    $"Weikio.TypeGenerator.Types.TypeCache.Details(System.Guid.Parse(\"{id.ToString()}\")).OnAfterMethod.DynamicInvoke(_instance, _instance.GetType().GetMethod(\"{originalMethodName}\", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));");
            }

            if (options.OnAfterMethodCustomCodeGenerator != null)
            {
                code.AppendLine("// Custom after method code begins");

                code.AppendLine(options.OnAfterMethodCustomCodeGenerator(options, originalType, methodInfo));
                
                code.AppendLine("// Custom after method code ends");
            }

            if (typeof(void) != returnType)
            {
                code.AppendLine("return result;");
            }

            code.AppendLine("}"); // Close method
        }

        private static void CreateProperties(List<string> propertyNamesWithTypes, StringBuilder code)
        {
            if (propertyNamesWithTypes?.Any() == true)
            {
                code.AppendLine();

                foreach (var fieldNameWithType in propertyNamesWithTypes)
                {
                    code.AppendLine($"public {fieldNameWithType} {{get; set;}}");
                }

                code.AppendLine();
            }
        }

        private static void CreateInstance(Type originalType, StringBuilder code, Guid id)
        {
            code.AppendLine();

            code.AppendLine(
                $"private {originalType.FullName} _instance = ({originalType.FullName}) Weikio.TypeGenerator.Types.TypeCache.Get(System.Guid.Parse(\"{id.ToString()}\"));");

            code.AppendLine();
        }

        private static void CreateConstructor(TypeToTypeWrapperOptions options, List<string> constructorParameterNames,
            List<string> constructorFielsNamesWithTypes,
            StringBuilder code, List<string> constructorParameterNamesWithTypes, Type originalType, Guid id)
        {
            if (constructorParameterNames?.Any() == true)
            {
                foreach (var fieldNameWithType in constructorFielsNamesWithTypes)
                {
                    code.AppendLine($"private {fieldNameWithType};");
                }

                code.AppendLine($"public {GetTypeName(options, originalType)}({string.Join(", ", constructorParameterNamesWithTypes)})");
                code.AppendLine("{");

                foreach (var constructorParameterName in constructorParameterNames)
                {
                    code.AppendLine($"_{constructorParameterName} = {constructorParameterName};");
                }

                code.AppendLine(
                    $"Weikio.TypeGenerator.Types.TypeCache.Details(System.Guid.Parse(\"{id.ToString()}\")).OnConstructor.DynamicInvoke(_instance);");

                if (options.OnConstructorCustomCodeGenerator != null)
                {
                    code.AppendLine("// Custom constructor code begins");

                    code.AppendLine(options.OnConstructorCustomCodeGenerator(options, originalType));
                
                    code.AppendLine("// Custom constructor code ends");
                }
                
                code.AppendLine("}"); // Close constructor
            }
            else if (options.OnConstructor != null)
            {
                code.AppendLine($"public {GetTypeName(options, originalType)}()");
                code.AppendLine("{");

                code.AppendLine(
                    $"Weikio.TypeGenerator.Types.TypeCache.Details(System.Guid.Parse(\"{id.ToString()}\")).OnConstructor.DynamicInvoke(_instance);");

                if (options.OnConstructorCustomCodeGenerator != null)
                {
                    code.AppendLine("// Custom constructor code begins");

                    code.AppendLine(options.OnConstructorCustomCodeGenerator(options, originalType));
                
                    code.AppendLine("// Custom constructor code ends");
                }
                
                code.AppendLine("}"); // Close constructor
            }
            else
            {
                code.AppendLine($"public {GetTypeName(options, originalType)}()");
                code.AppendLine("{");

                if (options.OnConstructorCustomCodeGenerator != null)
                {
                    code.AppendLine("// Custom constructor code begins");

                    code.AppendLine(options.OnConstructorCustomCodeGenerator(options, originalType));
                
                    code.AppendLine("// Custom constructor code ends");
                }
                
                code.AppendLine("}"); // Close constructor
            }
        }

        private static void DoConversions(ParameterInfo[] parameters,
            List<ParameterConversionRule> conversionRules,
            List<string> constructorParameterNames,
            List<string> constructorParameterNamesWithTypes,
            List<string> constructorFieldNamesWithTypes,
            List<string> propertyNamesWithTypes,
            List<string> delegateMethodParameters,
            List<string> methodParameterNamesWithTypes, TypeToTypeWrapperOptions options, Type originalType)
        {
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameterInfo = parameters[index];
                var parameterType = parameterInfo.ParameterType;

                var parameterName = parameterInfo.Name ??
                                    $"param{Guid.NewGuid().ToString().ToLowerInvariant().Replace("-", "")}";

                var handled = false;

                foreach (var conversionRule in conversionRules)
                {
                    var canHandle = conversionRule.CanHandle(parameterInfo);

                    if (canHandle)
                    {
                        var conversionResult = conversionRule.Handle(parameterInfo);

                        if (!string.IsNullOrWhiteSpace(conversionResult.Name))
                        {
                            parameterName = conversionResult.Name;
                        }

                        if (conversionResult.ToConstructor)
                        {
                            constructorParameterNames.Add(parameterName);
                            constructorParameterNamesWithTypes.Add($"{GetFriendlyName(parameterType, GetTypeName(options, originalType))} {parameterName}");

                            var fieldName = $"_{parameterName}";
                            constructorFieldNamesWithTypes.Add($"{GetFriendlyName(parameterType, GetTypeName(options, originalType))} {fieldName}");
                            delegateMethodParameters.Add(fieldName);

                            handled = true;

                            break;
                        }

                        if (conversionResult.ToPublicProperty)
                        {
                            var propertyName = $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(parameterName)}";

                            if (string.Equals(parameterName, propertyName))
                            {
                                propertyName = $"{propertyName}Prop";
                            }

                            propertyNamesWithTypes.Add($"{GetFriendlyName(parameterType, GetTypeName(options, originalType))} {propertyName}");
                            delegateMethodParameters.Add(propertyName);

                            handled = true;

                            break;
                        }

                        methodParameterNamesWithTypes.Add($"{GetFriendlyName(parameterType, GetTypeName(options, originalType))} {parameterName}");

                        delegateMethodParameters.Add(parameterName);

                        handled = true;

                        break;
                    }
                }

                if (handled)
                {
                    continue;
                }

                methodParameterNamesWithTypes.Add($"{GetFriendlyName(parameterType, GetTypeName(options, originalType))} {parameterName}");
                delegateMethodParameters.Add(parameterName);
            }
        }

        private static void AddNamespaces(StringBuilder code, List<Type> allTypes)
        {
            code.AppendLine("using System;");
            code.AppendLine("using System.Diagnostics;");
            code.AppendLine("using System.Threading.Tasks;");
            code.AppendLine("using System.Text;");
            code.AppendLine("using System.Collections;");
            code.AppendLine("using System.Collections.Generic;");

            foreach (var t in allTypes)
            {
                code.AppendLine($"using {t.Namespace};");
            }
        }

        private static void AddReferences(CodeToAssemblyGenerator generator, List<Type> allTypes, TypeToTypeWrapperOptions options, Type originalType)
        {
            generator.ReferenceAssemblyContainingType<Action>();
            generator.ReferenceAssemblyContainingType<DelegateCache>();
            generator.ReferenceAssemblyContainingType<DelegateToTypeWrapper>();

            foreach (var allType in allTypes)
            {
                generator.ReferenceAssembly(allType.Assembly);
            }

            if (options?.AdditionalReferences?.Any() == true)
            {
                foreach (var additionalReference in options.AdditionalReferences)
                {
                    generator.ReferenceAssembly(additionalReference);
                }
            }

            if (options?.TypeAttributesGenerator != null)
            {
                var attrs = options.TypeAttributesGenerator(options, originalType);

                foreach (var attribute in attrs)
                {
                    generator.ReferenceAssembly(attribute.GetType().Assembly);
                }
            }
        }

        private List<Type> GetRequiredTypes(ParameterInfo[] parameters, Type returnType)
        {
            var allTypes = new List<Type>();
            allTypes.AddRange(parameters.Select(x => x.ParameterType));
            allTypes.Add(returnType);

            var genTypes = new List<Type>();

            foreach (var type in allTypes)
            {
                genTypes = GetGenericTypes(type, genTypes);
            }

            foreach (var genType in genTypes)
            {
                allTypes.Add(genType);
            }

            return allTypes;
        }

        private static string GetNamespace(TypeToTypeWrapperOptions options, Type originalType)
        {
            if (options?.NamespaceNameGenerator != null)
            {
                return options.NamespaceNameGenerator(options, originalType);
            }

            return "GeneratedNamespace";
        }

        private static string GetTypeName(TypeToTypeWrapperOptions options, Type originalType)
        {
            if (options?.TypeNameGenerator != null)
            {
                return options.TypeNameGenerator(options, originalType);
            }

            return "GeneratedType";
        }

        public List<Type> GetGenericTypes(Type type, List<Type> types)
        {
            if (types == null)
            {
                types = new List<Type>();
            }

            if (!types.Contains(type))
            {
                types.Add(type);
            }

            if (!type.IsGenericType)
            {
                return types;
            }

            var typeParameters = type.GetGenericArguments();

            foreach (var typeParameter in typeParameters)
            {
                GetGenericTypes(typeParameter, types);
            }

            return types;
        }

        /// <summary>
        ///     https://stackoverflow.com/a/26429045/66988
        /// </summary>
        public static string GetFriendlyName(Type type, string generatedTypeName)
        {
            var friendlyName = type.FullName;

            if (string.IsNullOrWhiteSpace(friendlyName) && type.IsGenericParameter &&
                string.Equals(type.Name, "T", StringComparison.InvariantCultureIgnoreCase))
            {
                return generatedTypeName;
            }
            
            if (string.IsNullOrWhiteSpace(friendlyName))
            {
                friendlyName = type.Name;
            }

            if (!type.IsGenericType)
            {
                return friendlyName;
            }

            var iBacktick = friendlyName.IndexOf('`');

            if (iBacktick > 0)
            {
                friendlyName = friendlyName.Remove(iBacktick);
            }

            friendlyName += "<";
            var typeParameters = type.GetGenericArguments();

            for (var i = 0; i < typeParameters.Length; ++i)
            {
                var typeParamName = GetFriendlyName(typeParameters[i], generatedTypeName);
                friendlyName += i == 0 ? typeParamName : "," + typeParamName;
            }

            friendlyName += ">";

            return friendlyName;
        }

        private static Regex NameToRegex(string nameFilter)
        {
            // https://stackoverflow.com/a/30300521/66988
            var regex = "^" + Regex.Escape(nameFilter).Replace("\\?", ".").Replace("\\*", ".*") + "$";

            return new Regex(regex, RegexOptions.Compiled);
        }
    }
}
