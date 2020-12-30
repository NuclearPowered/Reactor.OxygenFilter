using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Reactor.OxygenFilter.MSBuild
{
    public class GenerateReferences : Task
    {
        [Required]
        public string AmongUs { get; set; }

        [Required]
        public string Mappings { get; set; }

        [Output]
        public string ReferencesPath { get; set; }

        public override bool Execute()
        {
            ReferencesPath = Path.Combine(Context.DataPath, "references");

            Directory.CreateDirectory(Context.DataPath);

            var skip = true;

            var gameAssemblyPath = Path.Combine(AmongUs, "GameAssembly.dll");
            var hash = Context.ComputeHash(new FileInfo(gameAssemblyPath));
            var hashPath = Path.Combine(Context.DataPath, "GameAssembly.dll.md5");

            if (!File.Exists(hashPath) || hash != File.ReadAllText(hashPath))
            {
                skip = false;
            }

            var mappingsHash = Context.ComputeHash(Mappings);
            var mappingsHashPath = Path.Combine(Context.DataPath, "mappings.md5");

            if (!File.Exists(mappingsHashPath) || mappingsHash != File.ReadAllText(mappingsHashPath))
            {
                skip = false;
            }

            if (skip)
            {
                return true;
            }

            if (Directory.Exists(Context.TempPath))
            {
                Directory.Delete(Context.TempPath, true);
            }

            var dumperConfig = new Il2CppDumper.Config
            {
                GenerateScript = false,
                GenerateDummyDll = true
            };

            Log.LogMessage(MessageImportance.High, "Generating Il2CppDumper intermediate assemblies");

            Il2CppDumper.Il2CppDumper.PerformDump(
                gameAssemblyPath,
                Path.Combine(AmongUs, "Among Us_Data", "il2cpp_data", "Metadata", "global-metadata.dat"),
                Context.TempPath, dumperConfig, _ =>
                {
                }
            );

            Log.LogMessage(MessageImportance.High, "Executing Reactor.OxygenFilter");

            var oxygenFilter = new OxygenFilter();

            var dumpedDll = new FileInfo(Path.Combine(Context.TempPath, "DummyDll", "Assembly-CSharp.dll"));
            oxygenFilter.Start(Mappings, dumpedDll, dumpedDll);

            Log.LogMessage(MessageImportance.High, "Executing Il2CppUnhollower generator");

            UnhollowerBaseLib.LogSupport.WarningHandler += s => Log.LogWarning(s);
            UnhollowerBaseLib.LogSupport.ErrorHandler += s => Log.LogError(s);

            var unityBaseLibDir = Path.Combine(AmongUs, "BepInEx", "unhollowed", "base");

            var unhollowerOptions = new AssemblyUnhollower.UnhollowerOptions
            {
                GameAssemblyPath = gameAssemblyPath,
                MscorlibPath = Path.Combine(AmongUs, "mono", "Managed", "mscorlib.dll"),
                SourceDir = Path.Combine(Context.TempPath, "DummyDll"),
                OutputDir = Path.Combine(Context.TempPath, "unhollowed"),
                UnityBaseLibsDir = unityBaseLibDir,
                NoCopyUnhollowerLibs = true
            };

            AssemblyUnhollower.Program.Main(unhollowerOptions);

            Directory.CreateDirectory(ReferencesPath);

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(Path.Combine(unhollowerOptions.OutputDir, "Assembly-CSharp.dll"));

            assemblyDefinition.Name = new AssemblyNameDefinition(assemblyDefinition.Name.Name + "-Deobfuscated", assemblyDefinition.Name.Version);
            assemblyDefinition.MainModule.Name += "-Deobfuscated";

            assemblyDefinition.Write(Path.Combine(ReferencesPath, "Assembly-CSharp-Deobfuscated.dll"));

            File.WriteAllText(hashPath, hash);
            File.WriteAllText(mappingsHashPath, mappingsHash);

            return true;
        }
    }
}
