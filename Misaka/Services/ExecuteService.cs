using System;
using System.Collections.Generic;
using System.Text;
using Discord.WebSocket;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis.Emit;
using System.Linq;
using System.Runtime.Loader;
using Discord.Commands;
using Discord;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands.Builders;
using System.Collections;
using Misaka.Classes;

namespace Misaka.Services
{
    public struct ExecuteScriptsResult
    {
        public int SuccessfulScripts;
        public int FailedScripts;

        public ExecuteScriptsResult(int successes, int fails)
        {
            SuccessfulScripts = successes;
            FailedScripts = fails;
        }
    }

    public class ExecuteService : Service
    {
        private Dictionary<Type, ModuleInfo> ExternalModules;

        public ExecuteService(IServiceProvider provider) : base(provider)
        {
            Provider = provider;
        }

        public class ServiceGlobals
        {
            public static IServiceProvider Provider;

            public ServiceGlobals(IServiceProvider provider)
            {
                Provider = provider;
            }
        }

        protected override void Run()
        {
            ExternalModules = new Dictionary<Type, ModuleInfo>();
            StaticMethods.FireAndForget(async () =>
            {
                ExecuteScriptsResult res = await LoadScripts();
                ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"Loaded $[[Yellow]]${res.SuccessfulScripts}$[[Gray]]$ external scripts successfully, $[[Red]]${res.FailedScripts}$[[Gray]]$ scripts failed.");
            });
        }

        public bool ExternalModuleExists(Type type)
        {
            return (ExternalModules.ContainsKey(type));
        }

        public bool ExternalModuleExists(string name)
        {
            foreach(KeyValuePair<Type, ModuleInfo> module in ExternalModules)
            {
                if (module.Key.Name.ToLower() == name.ToLower())
                    return true;
            }

            return false;
        }

        public KeyValuePair<Type, ModuleInfo> GetExternalModule(string name)
        {
            foreach (KeyValuePair<Type, ModuleInfo> module in ExternalModules)
            {
                if (module.Key.Name.ToLower() == name.ToLower())
                    return module;
            }

            return new KeyValuePair<Type, ModuleInfo>(null, null);
        }

        public void AddExternalModule(Type type, ModuleInfo mdlInfo)
        {
            if (ExternalModuleExists(type.Name))
                return;

            ExternalModules.Add(type, mdlInfo);
        }

        public void RemoveExternalModule(Type type)
        {
            if (!ExternalModuleExists(type.Name))
                return;

            KeyValuePair<Type, ModuleInfo> mdlInfo = ExternalModules.FirstOrDefault(x => x.Key.Name.ToLower() == type.Name.ToLower());
            Provider.GetService<CommandService>().RemoveModuleAsync(mdlInfo.Key);
            ExternalModules.Remove(mdlInfo.Key);
        }

        public async Task<ExecuteScriptsResult> LoadScripts()
        {
            GC.Collect();
            string[] files = Directory.GetFiles($"ExternalScripts/", "*.cs");
            int successCount = 0;
            int failCount = 0;
            foreach (string file in files)
            {
                string fileText = File.ReadAllText(file);
                bool success = await ExecuteScript(fileText, file);
                if (success)
                {
                    ConsoleEx.WriteColoredLine(LogSeverity.Verbose, ConsoleTextFormat.TimeAndText, $"Loaded script $[[DarkGreen]]$\"{file}\"$[[Gray]]$ successfully.");
                    successCount++;
                }
                else
                {
                    ConsoleEx.WriteColoredLine(LogSeverity.Warning, ConsoleTextFormat.TimeAndText, $"Failed to load script $[[DarkRed]]$\"{file}\"$[[Gray]]$ successfully.");
                    failCount++;
                }
            }

            return new ExecuteScriptsResult(successCount, failCount);
        }

        public async Task<bool> ExecuteScript(string code, string fileName)
        {
            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                var assemblies = new[]
                {
                    typeof(IDiscordClient).GetTypeInfo().Assembly,
                    typeof(CommandService).GetTypeInfo().Assembly,
                    typeof(Object).GetTypeInfo().Assembly,
                    typeof(SocketCommandContext).GetTypeInfo().Assembly,
                    typeof(ExecuteService).GetTypeInfo().Assembly
                };
                var refs = from a in assemblies select MetadataReference.CreateFromFile(a.Location);
                var references = refs.ToList();

                var assemblyPath = Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location);
                references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll")));
                references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Threading.Tasks.dll")));
                references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Private.CoreLib.dll")));
                references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")));

                string scriptName = fileName.Split('/').LastOrDefault().Replace(".cs", "");
                var compilation = CSharpCompilation.Create($"{scriptName}.dll", syntaxTrees: new[] { syntaxTree }, references: references.ToArray(), options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, platform: Platform.X64));

                using (var ms = new MemoryStream())
                {
                    EmitResult result = compilation.Emit(ms);
                    if (!result.Success)
                    {
                        IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                                diagnostic.IsWarningAsError ||
                                diagnostic.Severity == DiagnosticSeverity.Error);

                        foreach (Diagnostic diagnostic in failures)
                        {
                            Console.WriteLine($"{diagnostic.Id} - {diagnostic.GetMessage()}");
                        }

                        return false;
                    }
                    else
                    {
                        ms.Seek(0, SeekOrigin.Begin);

                        DynamicAssemblyLoader context = new DynamicAssemblyLoader(); ;
                        Assembly assembly = context.LoadStream(ms);
                        foreach(Type type in assembly.GetTypes())
                        {
                            if (typeof(ModuleBase<SocketCommandContext>).IsAssignableFrom(type))
                            {
                                if (ExternalModuleExists(type.Name))
                                    RemoveExternalModule(type);

                                ModuleInfo mdlInfo = await Provider.GetService<CommandService>().AddModuleAsync(type);
                                AddExternalModule(type, mdlInfo);
                                ConsoleEx.WriteColoredLine(LogSeverity.Info, ConsoleTextFormat.TimeAndText, $"Added External Module: $[[DarkCyan]]${type.Name}");
                            }
                        }
                    }
                }
                /*MemoryStream memStream = new MemoryStream();
                var result = await CSharpScript.EvaluateAsync(code, options, globals: new ServiceGlobals(Provider));
                Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(memstream);
                await Provider.GetService<CommandService>().AddModulesAsync(AssemblyLoadContext.Default.LoadFromAssemblyPath());*/
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            return true;
        }
    }
}