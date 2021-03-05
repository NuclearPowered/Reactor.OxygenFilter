using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Reactor.OxygenFilter;

namespace Reactor.Greenhouse.Generation
{
    public static class Generator
    {
        public static TypeAttributes IgnoreVisibility(this TypeAttributes typeAttributes)
        {
            return typeAttributes & ~TypeAttributes.VisibilityMask;
        }

        public static FieldAttributes IgnoreVisibility(this FieldAttributes fieldAttributes)
        {
            return fieldAttributes & ~FieldAttributes.FieldAccessMask;
        }

        public static MethodAttributes IgnoreVisibility(this MethodAttributes methodAttributes)
        {
            return methodAttributes & ~MethodAttributes.MemberAccessMask;
        }

        private static bool IsAssemblyCSharp(this IMetadataScope scope)
        {
            return scope.Name != "Assembly-CSharp.dll";
        }

        public static Mappings Generate(GenerationContext context)
        {
            var result = new Mappings();

            var lookupTypes = Extensions.Time(() =>
            {
                LookupTypes(context);
            });

            Console.WriteLine("LookupTypes took " + lookupTypes);

            foreach (var (obfuscatedType, typeContext) in context.Map.ToDictionary(k => k.Key, v => v.Value))
            {
                var mappedType = typeContext.ToMappedType(
                    obfuscatedType,
                    (obfuscatedNested, cleanNested) =>
                    {
                        if (!context.Map.TryGetValue(obfuscatedNested, out var nested))
                        {
                            nested = new TypeContext(context, double.MaxValue, cleanNested);
                            context.Map[obfuscatedNested] = nested;
                        }

                        return nested;
                    });

                if (mappedType == null || obfuscatedType.DeclaringType != null)
                    continue;

                result.Types.Add(mappedType);
            }

            return result;
        }

        private static bool TestEnum(TypeDefinition cleanType, TypeDefinition obfuscatedType)
        {
            if (!obfuscatedType.IsEnum)
                return false;

            if (cleanType.Fields.Count != obfuscatedType.Fields.Count)
                return false;

            for (var i = 0; i < cleanType.Fields.Count; i++)
            {
                var cleanField = cleanType.Fields[i];
                var obfuscatedField = obfuscatedType.Fields[i];

                if (cleanField.Name != obfuscatedField.Name)
                {
                    return false;
                }

                if (cleanField.HasConstant && !cleanField.Constant.Equals(obfuscatedField.Constant))
                {
                    return false;
                }
            }

            return true;
        }

        private static double TestField(FieldDefinition cleanField, FieldDefinition obfuscatedField)
        {
            var points = 0d;

            if (cleanField.Attributes.IgnoreVisibility() != obfuscatedField.Attributes.IgnoreVisibility())
            {
                points -= 1;
            }

            if (cleanField.HasConstant)
            {
                points += cleanField.Constant.Equals(obfuscatedField.Constant) ? 1 : -1;
            }

            if (cleanField.Name == obfuscatedField.Name)
            {
                points += 1;
            }

            if (cleanField.FieldType.FullName == obfuscatedField.FieldType.FullName)
            {
                points += 1;
            }

            return points;
        }

        private static double TestMethod(MethodDefinition cleanMethod, MethodDefinition obfuscatedMethod)
        {
            var points = 0d;

            if (cleanMethod.Name == obfuscatedMethod.Name)
            {
                points += 1;
            }

            if (cleanMethod.ReturnType.FullName == obfuscatedMethod.ReturnType.FullName)
            {
                points += cleanMethod.ReturnType.FullName == "System.Void" ? 0.5 : 1;
            }
            else if (cleanMethod.ReturnType.Scope.IsAssemblyCSharp())
            {
                points -= 1;
            }

            for (var i = 0; i < cleanMethod.Parameters.Count; i++)
            {
                var cleanParameter = cleanMethod.Parameters[i];

                if (obfuscatedMethod.Parameters.Count <= i)
                {
                    points -= 1;
                    continue;
                }

                var obfuscatedParameter = obfuscatedMethod.Parameters[i];

                if (cleanParameter.ParameterType.FullName == obfuscatedParameter.ParameterType.FullName)
                {
                    points += 1;
                }
                else if (cleanParameter.ParameterType.Scope.IsAssemblyCSharp())
                {
                    points -= 1;
                }
            }

            if (obfuscatedMethod.Parameters.Count > cleanMethod.Parameters.Count)
            {
                points -= obfuscatedMethod.Parameters.Count - cleanMethod.Parameters.Count;
            }

            return points;
        }

        private static double TestType(TypeDefinition cleanType, TypeDefinition obfuscatedType)
        {
            if (cleanType.Name.StartsWith("<"))
                return 0;

            if (cleanType.Attributes.IgnoreVisibility() != obfuscatedType.Attributes.IgnoreVisibility())
                return 0;

            if (cleanType.IsEnum)
            {
                return TestEnum(cleanType, obfuscatedType) ? double.MaxValue : 0;
            }

            if (cleanType.BaseType != null && obfuscatedType.BaseType != null && cleanType.BaseType.FullName != obfuscatedType.BaseType.FullName)
            {
                if (cleanType.BaseType.Scope.IsAssemblyCSharp() && !obfuscatedType.BaseType.Name.IsObfuscated())
                {
                    return 0;
                }
            }

            var points = 0d;

            // this will only work with up to date mono dll (:fortelove:)
            if (obfuscatedType.Fields.Count != cleanType.Fields.Count || obfuscatedType.Properties.Count != cleanType.Properties.Count || obfuscatedType.NestedTypes.Count != cleanType.NestedTypes.Count)
            {
                return 0;
            }

            for (var i = 0; i < cleanType.Fields.Count; i++)
            {
                var cleanField = cleanType.Fields[i];

                if (obfuscatedType.Fields.Count <= i)
                {
                    points -= 1;
                    continue;
                }

                var obfuscatedField = obfuscatedType.Fields[i];

                points += TestField(cleanField, obfuscatedField);
            }

            if (obfuscatedType.Fields.Count > cleanType.Fields.Count)
            {
                points -= obfuscatedType.Fields.Count - cleanType.Fields.Count;
            }

            foreach (var cleanMethod in cleanType.Methods)
            {
                var winnerPoints = 0d;

                foreach (var obfuscatedMethod in obfuscatedType.Methods)
                {
                    var methodPoints = TestMethod(cleanMethod, obfuscatedMethod);

                    if (methodPoints > winnerPoints && methodPoints >= 1)
                    {
                        winnerPoints = methodPoints;
                    }
                }

                if (winnerPoints > 0)
                {
                    points += winnerPoints;
                }
            }

            return points;
        }

        private static void LookupTypes(GenerationContext context)
        {
            foreach (var cleanType in context.CleanModule.GetAllTypes())
            {
                if (cleanType.Name.StartsWith("<"))
                    continue;

                TypeDefinition winner = null;
                var winnerPoints = 0d;

                foreach (var obfuscatedType in context.ObfuscatedModule.GetAllTypes())
                {
                    if (obfuscatedType.Name.StartsWith("<") && obfuscatedType.Name.EndsWith(">"))
                        continue;

                    var points = TestType(cleanType, obfuscatedType);

                    if (points > winnerPoints)
                    {
                        winnerPoints = points;
                        winner = obfuscatedType;
                    }
                }

                if (winnerPoints != 0 && winner != null)
                {
                    var typeContext = new TypeContext(context, winnerPoints, cleanType);
                    if (!context.Map.TryAdd(winner, typeContext))
                    {
                        Console.WriteLine($"{winner.FullName} = {cleanType.FullName} - {winnerPoints}");

                        var points = context.Map[winner].Points;
                        if (points.Equals(winnerPoints))
                        {
                            Console.WriteLine("Warning: duplicate, remove all");
                            context.Map.Remove(winner);
                        }
                        else if (winnerPoints >= points)
                        {
                            Console.WriteLine("Warning: duplicate, replace");
                            context.Map[winner] = typeContext;
                        }
                        else
                        {
                            Console.WriteLine("Warning: duplicate, skip");
                        }
                    }
                }
            }
        }
    }
}
