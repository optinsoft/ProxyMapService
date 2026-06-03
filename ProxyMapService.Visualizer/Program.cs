using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ProxyMapService.Visualizer;

internal class Program
{
    private static Dictionary<string, string> _stateTransitions = new();

    private static void Main(string[] args)
    {
        // Automatically locate the root directory of the solution relative to the executable
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string solutionDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));

        // Specify the exact directory name of your core project where handlers are stored
        string handlersProjectDir = Path.Combine(solutionDir, "ProxyMapService", "Proxy", "Handlers");

        if (!Directory.Exists(handlersProjectDir))
        {
            Console.WriteLine($"Error: Source directory not found at {handlersProjectDir}");
            return;
        }

        //  Extract the Dictionary dynamically using Reflection
        try
        {
            LoadDictionaryFromCore();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting Dictionary via reflection: {ex.Message}");
            return;
        }

        Console.WriteLine("=== GENERATING MERMAID STATE DIAGRAM ===");
        string mermaidCode = GenerateMermaidGraph(handlersProjectDir);

        // Save the generated diagram directly to the root of the solution
        string outputPath = Path.Combine(solutionDir, "state_machine.md");
        File.WriteAllText(outputPath, mermaidCode);

        Console.WriteLine($"Success! The diagram has been saved to: {outputPath}");
    }

    private static void LoadDictionaryFromCore()
    {
        string targetClassName = "ProxyMapService.Proxy.Sessions.Session";

        // Force .NET to load the assembly by referencing a known type from it
        // This resolves the "assembly not loaded" error instantly.
        var triggerType = typeof(ProxyMapService.Proxy.Sessions.Session);

        // Find the assembly containing your core logic
        Assembly coreAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ProxyMapService")
            ?? throw new Exception("ProxyMapService assembly not loaded. Make sure the project is referenced.");

        Type targetType = coreAssembly.GetType(targetClassName)
            ?? throw new Exception($"Could not find class: {targetClassName}");

        // Find the 'Handlers' field via reflection (including private and static flags)
        FieldInfo field = targetType.GetField("Handlers", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new Exception("Could not find 'Handlers' field in the specified class.");

        // Read the actual dictionary instance
        var handlersDict = field.GetValue(null) as IDictionary
            ?? throw new Exception("'Handlers' field is null or cannot be cast to IDictionary.");

        // Populate our local lookup string map
        foreach (DictionaryEntry entry in handlersDict)
        {
            string stepName = entry.Key?.ToString() ?? "";

            // Extract the real class name of the handler instance
            string handlerName = entry.Value?.GetType().Name ?? "UnknownHandler";

            _stateTransitions[stepName] = handlerName;
        }

        Console.WriteLine($"Successfully loaded {_stateTransitions.Count} state definitions via reflection.");
    }

    private static string GenerateMermaidGraph(string sourceFolder)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph TD");
        sb.AppendLine("    %% Styling for terminal (end) states");
        sb.AppendLine("    classDef terminal fill:#fcc,stroke:#333,stroke-width:2px;");

        // Use a HashSet to track unique transitions and avoid duplicates
        // Format stored: "SourceHandler -> StepName -> Target"
        var uniqueTransitions = new HashSet<string>();

        // Lists to categorize transitions into logical subgraphs
        var httpTransitions = new List<string>();
        var socks4Transitions = new List<string>();
        var socks5Transitions = new List<string>();
        var generalTransitions = new List<string>();

        // Scan for all C# files recursively within the target folder
        var files = Directory.GetFiles(sourceFolder, "*.cs", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            string code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            // Find all class declarations in the file
            var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classDecls)
            {
                string className = classDecl.Identifier.Text;

                // Filter to process only Handler classes
                if (!className.EndsWith("Handler")) continue;

                // Locate the 'Run' method inside the current Handler class
                var methodDecl = classDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == "Run");

                if (methodDecl == null) continue;

                // Extract all return statements inside the 'Run' method body
                var returnStatements = methodDecl.DescendantNodes().OfType<ReturnStatementSyntax>();

                foreach (var ret in returnStatements)
                {
                    string returnExpression = ret.Expression?.ToString() ?? "";

                    // Check if the handler returns a HandleStep state
                    if (returnExpression.Contains("HandleStep."))
                    {
                        string stepName = returnExpression.Split('.').Last();
                        string targetHandler;
                        bool isTerminal = false;

                        // Determine the target node name
                        if (_stateTransitions.TryGetValue(stepName, out string? foundHandler))
                        {
                            targetHandler = foundHandler;
                        }
                        else
                        {
                            targetHandler = $"Terminal_{stepName}";
                            isTerminal = true;
                        }

                        // Create a unique key for this transition
                        string transitionKey = $"{className}|{stepName}|{targetHandler}";

                        // Only append to Mermaid if this exact transition hasn't been processed yet
                        if (uniqueTransitions.Add(transitionKey))
                        {
                            // Generate the Mermaid line for this transition
                            string mermaidLine;
                            if (isTerminal)
                            {
                                mermaidLine = $"    {className} -- {stepName} --> {targetHandler}([{stepName}])\n    class {targetHandler} terminal;";
                            }
                            else
                            {
                                mermaidLine = $"    {className} -- {stepName} --> {targetHandler}";
                            }

                            // Categorize the transition based on the class or step name
                            if (className.StartsWith("Http") || stepName.StartsWith("Http"))
                            {
                                httpTransitions.Add(mermaidLine);
                            }
                            else if (className.StartsWith("Socks4") || stepName.StartsWith("Socks4"))
                            {
                                socks4Transitions.Add(mermaidLine);
                            }
                            else if (className.StartsWith("Socks5") || stepName.StartsWith("Socks5"))
                            {
                                socks5Transitions.Add(mermaidLine);
                            }
                            else
                            {
                                generalTransitions.Add(mermaidLine);
                            }
                        }
                    }
                }
            }
        }

        // 1. Append HTTP Subgraph
        sb.AppendLine("    subgraph HTTP [HTTP Protocol]");
        foreach (var line in httpTransitions) sb.AppendLine($"    {line}");
        sb.AppendLine("    end\n");

        // 2. Append Socks4 Subgraph
        sb.AppendLine("    subgraph Socks4 [SOCKS4 Protocol]");
        foreach (var line in socks4Transitions) sb.AppendLine($"    {line}");
        sb.AppendLine("    end\n");

        // 3. Append Socks5 Subgraph
        sb.AppendLine("    subgraph Socks5 [SOCKS5 Protocol]");
        foreach (var line in socks5Transitions) sb.AppendLine($"    {line}");
        sb.AppendLine("    end\n");

        // 4. Append General Transitions (Initialize, Tunnel, Session management, etc.)
        sb.AppendLine("    subgraph General [Core & General Logic]");
        foreach (var line in generalTransitions) sb.AppendLine($"    {line}");
        sb.AppendLine("    end");

        sb.AppendLine("```");
        return sb.ToString();
    }
}