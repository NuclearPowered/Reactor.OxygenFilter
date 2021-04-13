using System;
using System.Collections.Generic;
using System.Linq;
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

        public TypeDefinition ObfuscatedType { get; set; }

        public TypeContext Declaring { get; set; }
        public HashSet<TypeContext> Nested { get; } = new HashSet<TypeContext>();

        public string CleanFullName => CleanType.FullName;

        public TypeContext(GenerationContext context, double points, TypeDefinition cleanType)
        {
            Context = context;
            Points = points;
            CleanType = cleanType ?? throw new ArgumentNullException(nameof(cleanType));
        }

        public override string ToString()
        {
            return CleanFullName;
        }

        public void UpdateNested()
        {
            if (ObfuscatedType.NestedTypes.Count == CleanType.NestedTypes.Count)
            {
                for (var i = 0; i < CleanType.NestedTypes.Count; i++)
                {
                    var cleanNested = CleanType.NestedTypes[i];
                    var obfuscatedNested = ObfuscatedType.NestedTypes[i];

                    Nested.Add(Context.GetOrCreate(obfuscatedNested, cleanNested, this));
                }
            }
        }

        public MappedType ToMappedType()
        {
            var mappedType = new MappedType(ObfuscatedType.FullName, CleanType.Name.Clean());

            if (!string.IsNullOrEmpty(CleanType.Namespace))
            {
                mappedType.Mapped = CleanType.Namespace + "." + mappedType.Mapped;
            }

            if (ObfuscatedType.FullName == CleanFullName)
            {
                mappedType.Mapped = null;
            }

            if (ObfuscatedType.Fields.Count == CleanType.Fields.Count)
            {
                var sortedCleanFields = CleanType.Fields.OrderBy(x => x.HasConstant && x.Constant is string).ToArray();

                for (var i = 0; i < CleanType.Fields.Count; i++)
                {
                    var cleanField = sortedCleanFields[i];
                    var obfuscatedField = ObfuscatedType.Fields[i];

                    if (ObfuscatedType.DeclaringType != null)
                    {
                        var clean = obfuscatedField.Name.Clean();

                        if (clean != obfuscatedField.Name)
                        {
                            mappedType.Fields.Add(new MappedMember(obfuscatedField.Name, clean));
                            continue;
                        }
                    }

                    if (!obfuscatedField.Name.IsObfuscated())
                        continue;

                    mappedType.Fields.Add(new MappedMember(obfuscatedField.Name, cleanField.Name));
                }
            }

            if (ObfuscatedType.Properties.Count == CleanType.Properties.Count)
            {
                for (var i = 0; i < CleanType.Properties.Count; i++)
                {
                    var cleanProperty = CleanType.Properties[i];
                    var obfuscatedProperty = ObfuscatedType.Properties[i];

                    if (!obfuscatedProperty.Name.IsObfuscated())
                        continue;

                    mappedType.Properties.Add(new MappedMember(obfuscatedProperty.Name, cleanProperty.Name));
                }
            }

            foreach (var cleanMethod in CleanType.GetMethods())
            {
                if (ObfuscatedType.GetMethods().Any(x => x.Name == cleanMethod.Name))
                    continue;

                var matching = new List<MethodDefinition>();

                foreach (var obfuscatedMethod in ObfuscatedType.GetMethods().Where(x => x.Name.IsObfuscated()))
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

                    mappedType.Methods.Add(new MappedMethod(matched, cleanMethod.Name));
                }
            }

            if (ObfuscatedType.DeclaringType != null)
            {
                foreach (var obfuscatedMethod in ObfuscatedType.Methods)
                {
                    var clean = obfuscatedMethod.Name.Clean();

                    if (clean != obfuscatedMethod.Name)
                    {
                        mappedType.Methods.Add(new MappedMethod(obfuscatedMethod, clean));
                    }
                }
            }

            foreach (var nestedTypeContext in Nested)
            {
                var nestedType = nestedTypeContext.ToMappedType();
                if (nestedType != null)
                {
                    mappedType.Nested.Add(nestedType);
                }
            }

            if (!ObfuscatedType.Name.IsObfuscated() && !mappedType.Fields.Any() && !mappedType.Methods.Any() && !mappedType.Properties.Any() && !mappedType.Nested.Any())
                return null;

            return mappedType;
        }
    }
}
