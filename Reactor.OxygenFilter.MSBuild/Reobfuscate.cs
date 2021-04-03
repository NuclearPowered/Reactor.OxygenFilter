using System.IO;
using System.Linq;
using System.Collections.Generic;
using AssemblyUnhollower;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Reactor.OxygenFilter.MSBuild
{
    public class Reobfuscate : Task
    {
        [Required]
        public string AmongUs { get; set; }

        [Required]
        public string Input { get; set; }

        [Required]
        public string[] ReferencedAssemblies { get; set; }

        private static string GetObfuscated(ICustomAttributeProvider cap)
        {
            var attribute = cap.GetCustomAttribute("UnhollowerBaseLib.Attributes.ObfuscatedNameAttribute");
            return attribute?.ConstructorArguments.Single().Value as string;
        }

        private const string postfix = "-Deobfuscated";

        public override bool Execute()
        {
            using var stream = File.Open(Input, FileMode.Open, FileAccess.ReadWrite);
            var resolver = new AssemblyResolver();

            var obfuscatedAssembly = AssemblyDefinition.ReadAssembly(Path.Combine(AmongUs, "BepInEx", "unhollowed", "Assembly-CSharp.dll"));

            var referencedAssemblies = ReferencedAssemblies.Select(AssemblyDefinition.ReadAssembly).ToArray();

            resolver.ResolveFailure += (_, reference) =>
            {
                foreach (var referencedAssembly in referencedAssemblies)
                {
                    if (referencedAssembly.Name.Name == reference.Name)
                    {
                        return referencedAssembly;
                    }
                }

                if (reference.FullName == obfuscatedAssembly.Name.FullName)
                {
                    return obfuscatedAssembly;
                }

                return null;
            };

            using var moduleDefinition = ModuleDefinition.ReadModule(stream, new ReaderParameters { AssemblyResolver = resolver });
            var toObfuscate = new Dictionary<MemberReference, string>();

            foreach (var type in moduleDefinition.GetTypeReferences())
            {
                var typeDefinition = type.Resolve();

                if (typeDefinition == null)
                {
                    Log.LogWarning($"Unresolved type reference: {type.FullName}, {type.Scope}");
                    continue;
                }

                var obfuscated = GetObfuscated(typeDefinition);

                if (obfuscated != null)
                {
                    toObfuscate[type] = obfuscated;
                }
            }

            foreach (var member in moduleDefinition.GetMemberReferences())
            {
                var memberDefinition = member.Resolve();

                if (memberDefinition == null)
                {
                    Log.LogWarning($"Unresolved member reference: {member.FullName}, {member.DeclaringType.Scope}");
                    continue;
                }

                var obfuscated = GetObfuscated(memberDefinition);

                if (obfuscated != null)
                {
                    toObfuscate[member] = obfuscated;
                }
            }

            var customAttributeProviders = new List<ICustomAttributeProvider>();

            foreach (var type in moduleDefinition.GetAllTypes())
            {
                customAttributeProviders.Add(type);
                customAttributeProviders.AddRange(type.Events);
                customAttributeProviders.AddRange(type.Fields);
                customAttributeProviders.AddRange(type.Methods);
                customAttributeProviders.AddRange(type.Properties);
            }

            foreach (var customAttributeProvider in customAttributeProviders)
            {
                void ReobfuscateType(TypeReference t)
                {
                    while (t != null)
                    {
                        var obfuscated = GetObfuscated(t.Resolve());

                        if (obfuscated != null)
                        {
                            toObfuscate[t] = obfuscated;
                        }

                        t = t.DeclaringType;
                    }
                }

                foreach (var customAttribute in customAttributeProvider.CustomAttributes)
                {
                    TypeReference lastType = null;

                    foreach (var argument in customAttribute.ConstructorArguments.ToList())
                    {
                        if (argument.Type.FullName == "System.Type[]")
                        {
                            foreach (var nestedArgument in (CustomAttributeArgument[]) argument.Value)
                            {
                                if (nestedArgument.Type.FullName == "System.Type")
                                {
                                    ReobfuscateType((TypeReference) nestedArgument.Value);
                                }
                            }
                        }
                        else if (argument.Type.FullName == "System.Type")
                        {
                            lastType = (TypeReference) argument.Value;
                            ReobfuscateType(lastType);
                        }
                        else
                        {
                            // obfuscate nameof
                            if (argument.Type.FullName == "System.String" && lastType != null)
                            {
                                var value = (string) argument.Value;

                                var lastTypeDef = lastType.Resolve();

                                IMemberDefinition member = null;

                                var field = lastTypeDef.Fields.SingleOrDefault(x => x.Name == value);

                                if (field != null)
                                {
                                    member = field;
                                }

                                var method = lastTypeDef.Methods.FirstOrDefault(x => x.Name == value);

                                if (method != null)
                                {
                                    member = method;
                                }

                                var property = lastTypeDef.Properties.SingleOrDefault(x => x.Name == value);

                                if (property != null)
                                {
                                    member = property;
                                }

                                if (member != null)
                                {
                                    var obfuscated = GetObfuscated(member);

                                    if (obfuscated != null)
                                    {
                                        customAttribute.ConstructorArguments[customAttribute.ConstructorArguments.IndexOf(argument)]
                                            = new CustomAttributeArgument(argument.Type, obfuscated);
                                    }
                                }
                            }

                            lastType = null;
                        }
                    }
                }
            }

            // fix generic methods
            foreach (var methodDefinition in moduleDefinition.GetAllTypes().SelectMany(x => x.Methods))
            {
                if (!methodDefinition.HasBody)
                    continue;

                foreach (var instruction in methodDefinition.Body.Instructions)
                {
                    if (instruction.Operand is MethodReference deobfuscatedCallReference)
                    {
                        var deobfuscatedCall = deobfuscatedCallReference.Resolve();
                        if (deobfuscatedCall == null)
                            continue;

                        var obfuscated = GetObfuscated(deobfuscatedCall);
                        if (obfuscated != null)
                        {
                            // get same type from obfuscated assembly

                            var hierarchy = new List<TypeDefinition>();
                            var type = deobfuscatedCall.DeclaringType;

                            while (type != null)
                            {
                                hierarchy.Insert(0, type);
                                type = type.DeclaringType;
                            }

                            var typeDefinition = obfuscatedAssembly.MainModule.GetType(GetObfuscated(hierarchy.First()) ?? hierarchy.First().FullName);

                            foreach (var element in hierarchy.Skip(1))
                            {
                                typeDefinition = typeDefinition.NestedTypes.Single(x => x.Name == (GetObfuscated(element) ?? element.Name));
                            }

                            // get same method from obfuscated assembly
                            var nameMatched = typeDefinition.GetMethods().Where(x => x.Name == obfuscated).ToArray();

                            var signature = deobfuscatedCall.GetSignature((member, original) =>
                            {
                                if (member is TypeReference typeReference && typeReference.Scope.Name == "Assembly-CSharp.dll-Deobfuscated")
                                {
                                    var resolved = typeReference.Resolve();
                                    if (resolved != null)
                                    {
                                        var s = GetObfuscated(resolved);

                                        if (s != null)
                                        {
                                            return s;
                                        }
                                    }
                                }

                                return original;
                            });

                            MethodReference definition = nameMatched.Length <= 1 ? nameMatched.Single() : nameMatched.Single(x => x.GetSignature() == signature);

                            // obfuscate generics
                            if (deobfuscatedCallReference is GenericInstanceMethod generic)
                            {
                                var genericDefinition = new GenericInstanceMethod(definition);
                                definition = genericDefinition;

                                foreach (var parameter in generic.GenericArguments)
                                {
                                    var resolved = parameter.Resolve();
                                    if (resolved != null)
                                    {
                                        var s = GetObfuscated(resolved);
                                        if (s != null)
                                        {
                                            parameter.Name = s;
                                        }
                                    }

                                    genericDefinition.GenericArguments.Add(parameter);
                                }
                            }

                            instruction.Operand = deobfuscatedCallReference.Module.ImportReference(definition);
                        }
                    }
                }
            }

            foreach (var pair in toObfuscate)
            {
                if (pair.Key is TypeReference typeReference)
                {
                    var lastIndexOf = pair.Value.LastIndexOf('.');
                    typeReference.Namespace = lastIndexOf == -1 ? null : pair.Value.Substring(0, lastIndexOf);
                    typeReference.Name = pair.Value.Substring(lastIndexOf + 1);
                }
                else
                {
                    pair.Key.Name = pair.Value;
                }
            }

            foreach (var typeReference in moduleDefinition.GetTypeReferences())
            {
                typeReference.Module.Name = typeReference.Module.Name.Replace(postfix, string.Empty);
                typeReference.Scope.Name = typeReference.Scope.Name.Replace(postfix, string.Empty);
            }

            foreach (var memberReference in moduleDefinition.GetMemberReferences())
            {
                memberReference.Module.Name = memberReference.Module.Name.Replace(postfix, string.Empty);
                memberReference.DeclaringType.Scope.Name = memberReference.DeclaringType.Scope.Name.Replace(postfix, string.Empty);
            }

            var outputDirectory = Path.Combine(Path.GetDirectoryName(Input), "reobfuscated");
            Directory.CreateDirectory(outputDirectory);
            moduleDefinition.Write(Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(Input) + $"-{Context.GameVersion}.dll"));

            return true;
        }
    }
}
