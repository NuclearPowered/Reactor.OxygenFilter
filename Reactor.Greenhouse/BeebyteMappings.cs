using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Reactor.OxygenFilter;

namespace Reactor.Greenhouse
{
    public class BeebyteMappings
    {
        public Dictionary<string, string> Names { get; }

        public BeebyteMappings(Dictionary<string, string> names)
        {
            Names = names;
        }

        public string GetOrDefault(string name)
        {
            if (name.IsObfuscated() && Names.TryGetValue(name, out var value))
            {
                return value.Clean();
            }

            return null;
        }

        public void Compile(Mappings result, ModuleDefinition module)
        {
            foreach (var typeDefinition in module.GetAllTypes())
            {
                var type = new MappedType(typeDefinition.FullName, GetOrDefault(typeDefinition.Name));

                foreach (var methodDefinition in typeDefinition.Methods)
                {
                    if (methodDefinition.Name.IsObfuscated() && !Names.ContainsKey(methodDefinition.Name))
                        continue; // dead method

                    var method = new MappedMethod(methodDefinition, GetOrDefault(methodDefinition.Name));

                    foreach (var parameterDefinition in methodDefinition.Parameters)
                    {
                        method.Parameters.Add(GetOrDefault(parameterDefinition.Name) ?? parameterDefinition.Name);
                    }

                    if (!methodDefinition.Name.IsObfuscated() && !method.Parameters.Any())
                        continue;

                    type.Methods.Add(method);
                }

                foreach (var fieldDefinition in typeDefinition.Fields)
                {
                    var field = new MappedMember(fieldDefinition.Name, GetOrDefault(fieldDefinition.Name) ?? fieldDefinition.Name.Clean());

                    if (!fieldDefinition.Name.IsObfuscated() && fieldDefinition.Name == field.Mapped)
                        continue;

                    type.Fields.Add(field);
                }

                foreach (var propertyDefinition in typeDefinition.Properties)
                {
                    var field = new MappedMember(propertyDefinition.Name, GetOrDefault(propertyDefinition.Name));

                    if (!propertyDefinition.Name.IsObfuscated())
                        continue;

                    type.Properties.Add(field);
                }

                if (!typeDefinition.Name.IsObfuscated() && !type.Fields.Any() && !type.Methods.Any() && !type.Properties.Any() && !type.Nested.Any())
                    continue;

                result.Types.Add(type);
            }
        }

        public static BeebyteMappings Parse(string[] text)
        {
            var names = new Dictionary<string, string>();
            foreach (var line in text)
            {
                // comment
                if (line.StartsWith("#"))
                {
                    continue;
                }

                var values = line.Split("⇨");
                if (values.Length != 2)
                {
                    throw new FormatException();
                }

                var preobfuscated = values[1];

                names.Add(values[0], preobfuscated.Contains("/") ? preobfuscated[(preobfuscated.IndexOf("/", StringComparison.Ordinal) + 1)..] : preobfuscated);
            }

            return new BeebyteMappings(names);
        }
    }
}
