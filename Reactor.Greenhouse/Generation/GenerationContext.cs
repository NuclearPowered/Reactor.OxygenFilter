using System.Collections.Generic;
using Mono.Cecil;

namespace Reactor.Greenhouse.Generation
{
    public class GenerationContext
    {
        public ModuleDefinition CleanModule { get; }
        public ModuleDefinition ObfuscatedModule { get; }

        public Dictionary<TypeDefinition, TypeContext> Map { get; } = new Dictionary<TypeDefinition, TypeContext>();

        public GenerationContext(ModuleDefinition cleanModule, ModuleDefinition obfuscatedModule)
        {
            CleanModule = cleanModule;
            ObfuscatedModule = obfuscatedModule;
        }

        public TypeContext GetOrCreate(TypeDefinition obfuscated, TypeDefinition clean, TypeContext parent = null)
        {
            if (!Map.TryGetValue(obfuscated, out var typeContext))
            {
                typeContext = new TypeContext(this, double.MaxValue, clean)
                {
                    ObfuscatedType = obfuscated,
                    Declaring = parent
                };

                Map[obfuscated] = typeContext;
            }

            return typeContext;
        }
    }
}
