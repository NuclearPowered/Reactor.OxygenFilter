using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Reactor.Greenhouse.Generation
{
    public class GenerationContext
    {
        public ModuleDefinition CleanModule { get; }
        public ModuleDefinition ObfuscatedModule { get; }

        public IEnumerable<TypeDefinition> AllCleanTypes { get; }
        public IEnumerable<TypeDefinition> AllObfuscatedTypes { get; }

        public Dictionary<TypeDefinition, TypeContext> Map { get; } = new Dictionary<TypeDefinition, TypeContext>();

        public GenerationContext(ModuleDefinition cleanModule, ModuleDefinition obfuscatedModule)
        {
            CleanModule = cleanModule;
            ObfuscatedModule = obfuscatedModule;

            AllCleanTypes = CleanModule.GetAllTypes();
            AllObfuscatedTypes = ObfuscatedModule.GetAllTypes();
        }
    }
}
