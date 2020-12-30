using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Reactor.OxygenFilter.MSBuild
{
    public class Deobfuscate : Task
    {
        [Required]
        public string AmongUs { get; set; }

        [Required]
        public string[] Input { get; set; }

        [Required]
        public string Mappings { get; set; }

        [Output]
        public string[] Deobfuscated { get; set; }

        public override bool Execute()
        {
            var path = Path.Combine(Context.DataPath, "mapped");

            Directory.CreateDirectory(path);

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var mappings = JsonConvert.DeserializeObject<Mappings>(Mappings);

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.Combine(Context.DataPath, "references"));
            resolver.AddSearchDirectory(Path.Combine(AmongUs, "BepInEx", "core"));
            resolver.AddSearchDirectory(Path.Combine(AmongUs, "BepInEx", "plugins"));
            resolver.AddSearchDirectory(Path.Combine(AmongUs, "BepInEx", "unhollowed"));

            var deobfuscated = new List<string>();

            foreach (var input in Input)
            {
                var fileName = Path.Combine(path, Path.GetFileName(input));
                deobfuscated.Add(fileName);

                var hash = Context.ComputeHash(new FileInfo(input));
                var hashFile = fileName + ".md5";

                if (File.Exists(hashFile) && File.ReadAllText(hashFile) == hash)
                {
                    continue;
                }

                using var stream = File.Open(input, FileMode.Open, FileAccess.Read, FileShare.Read);

                using var moduleDefinition = ModuleDefinition.ReadModule(stream, new ReaderParameters { AssemblyResolver = resolver });
                var toDeobfuscate = new Dictionary<MemberReference, string>();

                foreach (var type in moduleDefinition.GetTypeReferences())
                {
                    var typeDefinition = type.Resolve();

                    if (typeDefinition == null)
                    {
                        Log.LogWarning($"Unresolved type reference: {type.FullName}, {type.Scope}");
                        continue;
                    }

                    var mapped = mappings.FindByOriginal(typeDefinition.FullName);

                    if (mapped?.Mapped != null)
                    {
                        toDeobfuscate[type] = mapped.Mapped;
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

                    var mappedType = mappings.FindByOriginal(memberDefinition.DeclaringType.FullName);

                    if (mappedType != null)
                    {
                        var mappedMembers = mappedType.Fields.Concat(mappedType.Properties).Concat(mappedType.Methods);

                        var mapped = mappedMembers.FirstOrDefault(x => x.Original.Name == member.Name);

                        if (mapped?.Mapped != null)
                        {
                            toDeobfuscate[member] = mapped.Mapped;
                        }
                        else if (memberDefinition is MethodDefinition methodDefinition)
                        {
                            if (methodDefinition.IsGetter)
                            {
                                var property = memberDefinition.DeclaringType.Properties.FirstOrDefault(x => x.GetMethod?.Name == memberDefinition.Name);
                                if (property != null)
                                {
                                    mapped = mappedType.Properties.FirstOrDefault(x => x.Original.Name == property.Name);
                                    if (mapped?.Mapped != null)
                                    {
                                        toDeobfuscate[property] = mapped.Mapped;
                                        toDeobfuscate[member] = "get_" + mapped.Mapped;
                                    }
                                }
                            }
                            else if (methodDefinition.IsSetter)
                            {
                                var property = memberDefinition.DeclaringType.Properties.FirstOrDefault(x => x.SetMethod?.Name == memberDefinition.Name);

                                if (property != null)
                                {
                                    mapped = mappedType.Properties.FirstOrDefault(x => x.Original.Name == property.Name);

                                    if (mapped?.Mapped != null)
                                    {
                                        toDeobfuscate[property] = mapped.Mapped;
                                        toDeobfuscate[member] = "set_" + mapped.Mapped;
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (var pair in toDeobfuscate)
                {
                    pair.Key.Name = pair.Value;
                }

                foreach (var typeReference in moduleDefinition.GetTypeReferences())
                {
                    if (typeReference.Scope.Name == "Assembly-CSharp")
                    {
                        typeReference.Scope.Name += "-Deobfuscated";
                    }
                }

                moduleDefinition.Write(fileName);
                File.WriteAllText(hashFile, hash);
            }

            Deobfuscated = deobfuscated.ToArray();

            return true;
        }
    }
}
