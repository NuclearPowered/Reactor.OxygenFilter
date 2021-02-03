using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Reactor.OxygenFilter;

namespace Reactor.Greenhouse.Generation
{
    public class TypeContext
    {
        public GenerationContext Context { get; }
        public double Points { get; }
        public TypeDefinition CleanType { get; }

        public string CleanFullName => CleanType.FullName;

        public TypeContext(GenerationContext context, double points, TypeDefinition cleanType)
        {
            Context = context;
            Points = points;
            CleanType = cleanType;
        }

        public override string ToString()
        {
            return CleanFullName;
        }

        private static readonly Regex CompilerGeneratedRegex = new Regex(@"^<([\w\d]+)>.__\d+$", RegexOptions.Compiled);

        public MappedType ToMappedType(TypeDefinition obfuscatedType)
        {
            var mappedType = new MappedType(obfuscatedType.FullName, CleanType.Name);

            for (var i = 0; i < CleanType.Fields.Count; i++)
            {
                var cleanField = CleanType.Fields[i];
                var obfuscatedField = obfuscatedType.Fields[i];

                if (!obfuscatedField.Name.IsObfuscated())
                    continue;

                mappedType.Fields.Add(new MappedMember(obfuscatedField.Name, cleanField.Name));
            }

            for (var i = 0; i < CleanType.Properties.Count; i++)
            {
                var cleanProperty = CleanType.Properties[i];
                var obfuscatedProperty = obfuscatedType.Properties[i];

                if (!obfuscatedProperty.Name.IsObfuscated())
                    continue;

                mappedType.Properties.Add(new MappedMember(obfuscatedProperty.Name, cleanProperty.Name));
            }

            foreach (var cleanMethod in CleanType.GetMethods())
            {
                if (obfuscatedType.GetMethods().Any(x => x.Name == cleanMethod.Name))
                    continue;

                var matching = new List<MethodDefinition>();

                foreach (var obfuscatedMethod in obfuscatedType.GetMethods().Where(x => x.Name.IsObfuscated()))
                {
                    if (cleanMethod.ReturnType.FullName != obfuscatedMethod.ReturnType.FullName)
                        continue;

                    if (cleanMethod.Attributes.IgnoreVisibility() != obfuscatedMethod.Attributes.IgnoreVisibility())
                        continue;

                    if (cleanMethod.Parameters.Count != obfuscatedMethod.Parameters.Count)
                        continue;

                    var validParameters = true;

                    for (var i = 0; i < cleanMethod.Parameters.Count; i++)
                    {
                        var cleanParameter = cleanMethod.Parameters[i];
                        var obfuscatedParameter = obfuscatedMethod.Parameters[i];

                        var obfuscatedParameterContext = Context.Map.SingleOrDefault(x => x.Key.FullName == obfuscatedParameter.ParameterType.FullName).Value;

                        if (cleanParameter.ParameterType.FullName != (obfuscatedParameterContext == null ? obfuscatedParameter.ParameterType.FullName : obfuscatedParameterContext.CleanFullName))
                        {
                            validParameters = false;
                            break;
                        }
                    }

                    if (!validParameters)
                        continue;

                    matching.Add(obfuscatedMethod);

                    if (matching.Count > 1)
                        break;
                }

                if (matching.Count == 1)
                {
                    var matched = matching.Single();

                    mappedType.Methods.Add(new MappedMethod(new OriginalDescriptor
                    {
                        Name = matched.Name,
                        Signature = matched.GetSignature()
                    }, cleanMethod.Name));
                }
            }

            for (var i = 0; i < CleanType.NestedTypes.Count; i++)
            {
                var cleanNested = CleanType.NestedTypes[i];
                var obfuscatedNested = obfuscatedType.NestedTypes[i];

                if (cleanNested.Fields.Count != obfuscatedNested.Fields.Count)
                {
                    break;
                }

                var match = CompilerGeneratedRegex.Match(cleanNested.Name);

                var nested = new MappedType(obfuscatedNested.FullName, match.Success ? (match.Groups[1].Value + "__d") : cleanNested.Name.Replace("<>", ""));

                foreach (var nestedField in obfuscatedNested.Fields)
                {
                    switch (nestedField.Name)
                    {
                        case "<>1__state":
                            nested.Fields.Add(new MappedMember(nestedField.Name, "__state"));
                            continue;
                        case "<>2__current":
                            nested.Fields.Add(new MappedMember(nestedField.Name, "__current"));
                            continue;
                        case "<>4__this":
                            nested.Fields.Add(new MappedMember(nestedField.Name, "__this"));
                            continue;
                    }

                    var fieldMatch = CompilerGeneratedRegex.Match(nestedField.Name);

                    if (fieldMatch.Success)
                    {
                        nested.Fields.Add(new MappedMember(nestedField.Name, fieldMatch.Groups[1].Value));
                    }
                }

                foreach (var nestedMethod in obfuscatedNested.Methods)
                {
                    var methodMatch = CompilerGeneratedRegex.Match(nestedMethod.Name);

                    if (methodMatch.Success)
                    {
                        nested.Methods.Add(new MappedMethod(new OriginalDescriptor
                        {
                            Name = nestedMethod.Name,
                            Signature = nestedMethod.GetSignature()
                        }, methodMatch.Groups[1].Value));
                    }
                }

                if (!obfuscatedNested.Name.IsObfuscated() && nested.Mapped == cleanNested.Name && !nested.Fields.Any() && !nested.Methods.Any())
                    continue;

                mappedType.Nested.Add(nested);
            }

            if (!obfuscatedType.Name.IsObfuscated() && !mappedType.Fields.Any() && !mappedType.Methods.Any() && !mappedType.Properties.Any() && !mappedType.Nested.Any())
                return null;

            return mappedType;
        }
    }
}
