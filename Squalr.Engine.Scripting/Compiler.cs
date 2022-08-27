namespace Squalr.Engine.Scripting
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Emit;
    using Squalr.Engine.Common.Logging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Class for compiling scripts.
    /// </summary>
    public static class Compiler
    {
        /// <summary>
        /// Compiles the given ModScript.
        /// </summary>
        /// <param name="scriptPath"></param>
        /// <param name="isRelease"></param>
        /// <returns></returns>
        public static Assembly Compile(String scriptPath, String scriptContents, Boolean isRelease)
        {
            try
            {
                String buildPath = Path.Combine(Path.GetDirectoryName(scriptPath), isRelease ? "Release" : "Debug");

                if (!Directory.Exists(buildPath))
                {
                    Directory.CreateDirectory(buildPath);
                }

                String fileName = Path.GetFileNameWithoutExtension(scriptPath);
                String dllPath = Path.Combine(buildPath, fileName + ".dll");
                String pdbPath = Path.Combine(buildPath, fileName + ".pdb");
                String sourceCode = scriptContents;

                CSharpParseOptions cSharpParseOptions = new CSharpParseOptions(kind: SourceCodeKind.Regular, languageVersion: LanguageVersion.Latest);
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cSharpParseOptions);

                IReadOnlyCollection<MetadataReference> references = new[] {
                        MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(ValueTuple<>).GetTypeInfo().Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(Script).GetTypeInfo().Assembly.Location)
                };

                CSharpCompilationOptions cSharpCompilationOptions = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: isRelease ? OptimizationLevel.Release : OptimizationLevel.Debug,
                    allowUnsafe: true);

                CSharpCompilation compilation = CSharpCompilation.Create(fileName, options: cSharpCompilationOptions, references: references);

                using (FileStream dllStream = new FileStream(dllPath, FileMode.OpenOrCreate))
                {
                    using (FileStream pdbStream = new FileStream(pdbPath, FileMode.OpenOrCreate))
                    {
                        EmitResult result = compilation.Emit(peStream: dllStream, pdbStream: pdbStream);

                        if (!result.Success)
                        {
                            throw new Exception(result.Diagnostics.ToString());
                        }
                    }
                }

                return Assembly.LoadFrom(dllPath);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Error compiling script", ex);
            }

            return null;
        }
    }
    //// End class
}
//// End namespace