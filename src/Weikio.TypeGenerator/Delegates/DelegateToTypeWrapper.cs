using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Weikio.TypeGenerator.Delegates
{
    /// <summary>
    /// Allows the creation of a Type from a delegate (System.Action, System.Func)
    /// </summary>
    public class DelegateToTypeWrapper
    {
        public Type CreateType(MulticastDelegate multicastDelegate,
            DelegateToTypeWrapperOptions options = default)
        {
            var methodInfo = multicastDelegate.GetMethodInfo();

            if (methodInfo == null)
            {
                throw new Exception("Couldn't get method info from delegate");
            }
            
            var parameters = methodInfo.GetParameters();
            var returnType = methodInfo.ReturnType;

            var id = DelegateCache.Add(multicastDelegate);

            var allTypes = GetRequiredTypes(parameters, returnType);
            
            var generator = new CodeToAssemblyGenerator();
            AddReferences(generator, allTypes);

            var code = new StringBuilder();
            AddNamespaces(code, allTypes);

            var constructorParameterNames = new List<string>();
            var methodParameterNamesWithTypes = new List<string>();
            var constructorParameterNamesWithTypes = new List<string>();
            var constructorFielsNamesWithTypes = new List<string>();
            var propertyNamesWithTypes = new List<string>();
            var delegateMethodParameters = new List<string>();

            var conversionRules = options?.ConversionRules;
            if (conversionRules == null)
            {
                conversionRules = new List<ParameterConversionRule>();
            }

            DoConversions(parameters, conversionRules, constructorParameterNames, constructorParameterNamesWithTypes, constructorFielsNamesWithTypes, delegateMethodParameters, propertyNamesWithTypes, methodParameterNamesWithTypes);

            code.AppendLine();
            code.AppendLine($"namespace {GetNamespace(options)}");
            code.AppendLine("{");
            code.AppendLine($"public class {GetTypeName(options)}");
            code.AppendLine("{");

            CreateConstructor(options, constructorParameterNames, constructorFielsNamesWithTypes, code, constructorParameterNamesWithTypes);
            CreateProperties(propertyNamesWithTypes, code);
            CreateDelegateWrapperMethod(options, returnType, code, methodParameterNamesWithTypes, id, delegateMethodParameters);

            code.AppendLine("}"); // Close class
            code.AppendLine("}"); // Close namespace

            var s = code.ToString();
            var assembly = generator.GenerateAssembly(s);

            var result = assembly.GetExportedTypes().Single();

            return result;
        }

        private static void CreateDelegateWrapperMethod(DelegateToTypeWrapperOptions options, Type returnType, StringBuilder code, List<string> methodParameterNamesWithTypes,
            Guid id, List<string> delegateMethodParameters)
        {
            if (typeof(void) != returnType)
            {
                code.AppendLine(
                    $"public {GetFriendlyName(returnType)} {GetMethodName(options)} ({string.Join(", ", methodParameterNamesWithTypes)})");
            }
            else
            {
                code.AppendLine(
                    $"public void Run ({string.Join(", ", methodParameterNamesWithTypes)})");
            }

            code.AppendLine("{");
            code.AppendLine($"var deleg = Weikio.TypeGenerator.Delegates.DelegateCache.Get(System.Guid.Parse(\"{id.ToString()}\"));");

            if (typeof(void) != returnType)
            {
                code.AppendLine(
                    $"return ({GetFriendlyName(returnType)}) deleg.DynamicInvoke({string.Join(", ", delegateMethodParameters)});");
            }
            else
            {
                code.AppendLine(
                    $"deleg.DynamicInvoke({string.Join(", ", delegateMethodParameters)});");
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

        private static void CreateConstructor(DelegateToTypeWrapperOptions options, List<string> constructorParameterNames, List<string> constructorFielsNamesWithTypes,
            StringBuilder code, List<string> constructorParameterNamesWithTypes)
        {
            if (constructorParameterNames?.Any() == true)
            {
                foreach (var fieldNameWithType in constructorFielsNamesWithTypes)
                {
                    code.AppendLine($"private {fieldNameWithType};");
                }

                code.AppendLine($"public {GetTypeName(options)}({string.Join(", ", constructorParameterNamesWithTypes)})");
                code.AppendLine("{");

                foreach (var constructorParameterName in constructorParameterNames)
                {
                    code.AppendLine($"_{constructorParameterName} = {constructorParameterName};");
                }

                code.AppendLine("}"); // Close constructor
            }
        }

        private static void DoConversions(ParameterInfo[] parameters, List<ParameterConversionRule> conversionRules, List<string> constructorParameterNames, List<string> constructorParameterNamesWithTypes,
            List<string> constructorFielsNamesWithTypes, List<string> delegateMethodParameters, List<string> propertyNamesWithTypes, List<string> methodParameterNamesWithTypes)
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
                            constructorParameterNamesWithTypes.Add($"{GetFriendlyName(parameterType)} {parameterName}");

                            var fieldName = $"_{parameterName}";
                            constructorFielsNamesWithTypes.Add($"{GetFriendlyName(parameterType)} {fieldName}");
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

                            propertyNamesWithTypes.Add($"{GetFriendlyName(parameterType)} {propertyName}");
                            delegateMethodParameters.Add(propertyName);

                            handled = true;

                            break;
                        }

                        methodParameterNamesWithTypes.Add($"{GetFriendlyName(parameterType)} {parameterName}");

                        delegateMethodParameters.Add(parameterName);

                        handled = true;

                        break;
                    }
                }

                if (handled)
                {
                    continue;
                }

                methodParameterNamesWithTypes.Add($"{GetFriendlyName(parameterType)} {parameterName}");

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

        private static void AddReferences(CodeToAssemblyGenerator generator, List<Type> allTypes)
        {
            generator.ReferenceAssemblyContainingType<Action>();
            generator.ReferenceAssemblyContainingType<DelegateCache>();
            generator.ReferenceAssemblyContainingType<DelegateToTypeWrapper>();

            foreach (var allType in allTypes)
            {
                generator.ReferenceAssembly(allType.Assembly);
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

        private static string GetMethodName(DelegateToTypeWrapperOptions options)
        {
            if (options?.MethodNameGenerator != null)
            {
                return options.MethodNameGenerator(options);
            }

            return "Run";
        }
        
        private static string GetNamespace(DelegateToTypeWrapperOptions options)
        {
            if (options?.NamespaceNameGenerator != null)
            {
                return options.NamespaceNameGenerator(options);
            }

            return "GeneratedNamespace";
        }
        
        private static string GetTypeName(DelegateToTypeWrapperOptions options)
        {
            if (options?.TypeNameGenerator != null)
            {
                return options.TypeNameGenerator(options);
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
        public static string GetFriendlyName(Type type)
        {
            var friendlyName = type.FullName;

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
                var typeParamName = GetFriendlyName(typeParameters[i]);
                friendlyName += i == 0 ? typeParamName : "," + typeParamName;
            }

            friendlyName += ">";

            return friendlyName;
        }
    }
}
