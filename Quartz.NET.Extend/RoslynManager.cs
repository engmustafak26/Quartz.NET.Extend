using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Quartz.NET.Extend
{


    internal static class RoslynManager
    {
        private const string DynamicExecuteMethodName = "Exec";
        private const string JobJsonFileName = "jobs.json";
        private const string CodeSeperatorsBewteenDelegateAndBody = "*-*-*-*-*-*-///fhjhgffdummyDummy6445554444*-*-*-*-*-*";
        private static string globalUsings = null;
        private static Dictionary<string, string[]> TempCodeStore = null;



        public static async Task<Dictionary<string, string[]>> GetCode(string filePath)
        {
            Dictionary<string, string[]> sourceCodes = new Dictionary<string, string[]>();

            string code = File.ReadAllText(filePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);

            var root = tree.GetRoot();


            CompilationUnitSyntax usingRoot = root as CompilationUnitSyntax;


            var methodCalls = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

            var fileNameSpace = GetCurrentClassNamespace(root);
            if (!string.IsNullOrWhiteSpace(fileNameSpace))
            {
                fileNameSpace = "using " + fileNameSpace + " ;";
            }

            //Console.WriteLine("All method calls in file:");
            foreach (var call in methodCalls)
            {
                string invocationString = call.ToString();
                int indexOfFrstDot = invocationString.IndexOf('.');
                if (indexOfFrstDot != -1)
                {
                    invocationString = invocationString.Substring(indexOfFrstDot + 1);
                }
                if (Regex.IsMatch(invocationString, $"^\\s*{nameof(QuartzWrapper.addToJobs)}\\s*\\(")
                    && call.ArgumentList.Arguments.FirstOrDefault()?.ToFullString().StartsWith(nameof(QuartzWrapper.addToJobs)) == false)
                {
                    //Console.WriteLine(" - " + call.Expression);
                    //Console.WriteLine(" - " + call.ArgumentList.Arguments[0].ToFullString());

                    var usingDirectives = usingRoot.Usings;
                    string directives = string.Join(Environment.NewLine, usingDirectives.Select(x => x.ToFullString()).Concat(new string[] { fileNameSpace ?? string.Empty }));

                    var selectedArgument = call.ArgumentList.Arguments.Take(1).ToArray();
                    var callDeepCopy = call.WithExpression(call.Expression)
                                           .WithArgumentList(SyntaxFactory
                                                            .ArgumentList(SyntaxFactory.SeparatedList(selectedArgument)));

                    string funcCode = callDeepCopy.ToFullString();
                    funcCode = funcCode.Replace("QuartzWrapper.addToJob", "QuartzWrapper.ExecuteJob");


                    var arguments = new string[] { directives + Environment.NewLine + CodeSeperatorsBewteenDelegateAndBody + Environment.NewLine + funcCode }
                                       .Concat(call.ArgumentList.Arguments.Select(x => x.ToFullString()).ToArray())
                                       .ToArray();

                    string identifier = call.ArgumentList.Arguments[1].Expression.ToFullString().Trim('"');
                    sourceCodes.Add(identifier, arguments);
                }
            }

            return sourceCodes;

            string GetCurrentClassNamespace(SyntaxNode root)
            {

                // Get all type declarations (class, struct, interface, etc.)
                var typeDeclaration = root.DescendantNodes()
                    .OfType<BaseTypeDeclarationSyntax>()
                    .FirstOrDefault();

                if (typeDeclaration == null)
                    return null;

                // Handle both traditional and file-scoped namespaces
                BaseNamespaceDeclarationSyntax namespaceDeclaration = typeDeclaration.Parent as NamespaceDeclarationSyntax;
                if (namespaceDeclaration == null)
                {
                    namespaceDeclaration = typeDeclaration.Parent as FileScopedNamespaceDeclarationSyntax;
                }
                

                return namespaceDeclaration?.Name?.ToString();
            }
        }


        #region File Operations
        public static async Task WriteCodeToFile(Dictionary<string, string[]> sourceCode)
        {
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = Path.Combine(directory, JobJsonFileName);
            string code = JsonConvert.SerializeObject(sourceCode, Formatting.Indented);
            await File.WriteAllTextAsync(path, code);
        }
        internal static async Task<string> ReadJobsCodeFromFileAsync(string codeIdentifier)
        {
            if (TempCodeStore is null)
            {
                string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                string path = Path.Combine(directory, JobJsonFileName);
                string codeFile = await File.ReadAllTextAsync(path);
                TempCodeStore = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(codeFile)!;
            }
            TempCodeStore.TryGetValue(codeIdentifier, out var value);
            if (value?.Any() ?? false)
            {
                return value[0];
            }

            return null;
        }
        #endregion

        public static async Task<Type> ExecuteAsync(string code)
        {
            await ExtractGlobalUsingDirectiveIfUsedAsync();

            code = await ReadJobsCodeFromFileAsync(code);
            var codeSegments = code.Split(CodeSeperatorsBewteenDelegateAndBody, StringSplitOptions.RemoveEmptyEntries);

            string funcCode = codeSegments[1];
            int index = funcCode.LastIndexOf(')');
            funcCode = funcCode.Insert(index, ", codeCustomArgument");

            string methodCode = @$" 
                                    {globalUsings} 
                                    {codeSegments[0]}
                                "
                                +
                                @"
                                    public class DynamicClass {
                                        public static Task " + DynamicExecuteMethodName + "(object codeCustomArgument) => " + funcCode + ";}";

            // Compile and execute method
            Assembly assembly = CompileCode(methodCode);
            Type type = assembly.GetType("DynamicClass");

            return type;


            static async Task ExtractGlobalUsingDirectiveIfUsedAsync()
            {
                if (string.IsNullOrWhiteSpace(globalUsings))
                {
                    string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string pathSeparator = directoryName.Contains("\\") ? "\\" : "/";
                    directoryName = directoryName.Replace(pathSeparator + "bin" + pathSeparator, pathSeparator + "obj" + pathSeparator);
                    var globalUsingfile = Directory.GetFiles(directoryName, "*.cs")?.FirstOrDefault(x => x.Contains("GlobalUsings.g"));
                    if (!string.IsNullOrWhiteSpace(globalUsingfile))
                    {
                        globalUsings = await File.ReadAllTextAsync(globalUsingfile);
                        globalUsings = globalUsings.Replace("global ", "").Replace("global::", "");
                    }
                }
            }
        }


        public static Assembly CompileCode(string sourceCode)
        {

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            // Get references for all necessary assemblies
            var references = GetReferences();

            // Create the compilation
            var compilation = CSharpCompilation.Create("DynamicAssembly")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(references)
                .AddSyntaxTrees(syntaxTree);

            // Emit (compile) the assembly
            using var ms = new System.IO.MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                // Print compilation errors
                foreach (var diagnostic in result.Diagnostics)
                    Console.WriteLine(diagnostic.ToString());
                throw new Exception(string.Join(",", result.Diagnostics));
            }

            // Load and execute the compiled assembly
            ms.Seek(0, System.IO.SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            return assembly;

        }

        static IEnumerable<MetadataReference> GetReferences()
        {
            var dllFiles = Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.dll");

            // Convert to MetadataReference list
            var references = dllFiles.Select(dll => MetadataReference.CreateFromFile(dll)).ToList();

            try
            {

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                assemblies = assemblies.Where(x => !string.IsNullOrWhiteSpace(x.Location)).ToArray();

                references.AddRange(assemblies
                    .Select(assembly => MetadataReference.CreateFromFile(assembly.Location)));
            }
            catch (Exception ex)
            {

            }

            // Locate the .NET runtime directory (for core libraries)
            string runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();

            // Required Core .NET assemblies
            var coreAssemblies = new[]
            {
            "System.Private.CoreLib.dll",  // Core system types (e.g., System.Int32)
            "System.Runtime.dll",          // Essential runtime types
            "System.Console.dll",          // Console output
            "System.Linq.dll",             // LINQ support
            "System.Collections.dll",      // Collections (List, Dictionary, etc.)
            "System.Threading.Tasks.dll"   // Async support
        };

            // Add .NET Core references
            references.AddRange(coreAssemblies
                .Select(assembly => MetadataReference.CreateFromFile(Path.Combine(runtimeDir, assembly))));

            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(IServiceProvider).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(System.Net.Http.Json.JsonContent).Assembly.Location));

            return references;
        }


    }
}
