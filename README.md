# Type Generator for .NET

Provides functionality for generating .NET types and assemblies runtime. 

[![NuGet Version](https://img.shields.io/nuget/v/Weikio.TypeGenerator.svg?style=flat&label=Weikio.TypeGenerator)](https://www.nuget.org/packages/Weikio.TypeGenerator/)

## Features

#### Generate types and assemblies from source code
#### Wrap delegates (System.Func and System.Action) inside new types
#### Wrap existing types inside new types

When wrapping delegates and types, the delegate and method parameters can be converted to constructor parameters and properties.

Note: The intendent use case is not proxy classes and the generated types don't inherit the original types.
 
## Usage

### Generating assembly from a source code

````
var code = @"public class MyClass
       {
           public void RunThings()
           {
               var y = 0;
               var a = 1;

               a = y + 10;
           }
       }";

var assembly = _generator.GenerateAssembly(code);

var type = assembly.GetExportedTypes().Single();
````

### Generating wrapping type from a delegate

````
var converter = new DelegateToTypeConverter();

var type = converter.CreateType(new Func<int, bool>(i =>
{
    System.Console.WriteLine($"Hello from delegate. I is {i}");

    return true;
}));

dynamic instance = Activator.CreateInstance(type);
var result = instance.Run(25);
````

## License

This template is MIT licensed.