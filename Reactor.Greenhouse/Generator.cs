using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Reactor.OxygenFilter;

namespace Reactor.Greenhouse
{
    public static class Generator
    {
        private static TypeAttributes IgnoreVisibility(this TypeAttributes typeAttributes)
        {
            return typeAttributes & ~TypeAttributes.VisibilityMask;
        }

        public static Mappings Generate(ModuleDefinition old, ModuleDefinition latest)
        {
            var matches = new Dictionary<TypeDefinition, SortedList<double, TypeDefinition>>();

            var result = new Mappings();

            foreach (var oldType in old.GetAllTypes())
            {
                if (oldType.Name.StartsWith("<") || oldType.Namespace.StartsWith("GoogleMobileAds"))
                    continue;

                var list = new SortedList<double, TypeDefinition>(Comparer<double>.Create((x, y) => y.CompareTo(x)));
                matches[oldType] = list;

                var exact = latest.GetAllTypes().FirstOrDefault(x => x.FullName == oldType.FullName);
                if (exact != null)
                {
                    list.Add(double.MaxValue, exact);
                    continue;
                }

                if (oldType.IsEnum)
                {
                    var first = oldType.Fields.Select(x => x.Name).ToArray();
                    var type = latest.GetAllTypes().SingleOrDefault(x => x.IsEnum && x.Fields.Select(f => f.Name).SequenceEqual(first));
                    if (type != null)
                    {
                        list.Add(double.MaxValue, type);
                    }

                    continue;
                }

                static bool Test(TypeReference typeReference)
                {
                    return typeReference.IsGenericParameter || typeReference.Namespace != string.Empty || !typeReference.Name.IsObfuscated();
                }

                var methodNames = oldType.GetMethods().Select(x => x.Name).ToArray();
                var fieldNames = oldType.Fields.Select(x => x.Name).ToArray();
                var propertyNames = oldType.Properties.Select(x => x.Name).ToArray();

                var methodSignatures = oldType.GetMethods().Where(x => Test(x.ReturnType) && x.Parameters.All(p => Test(p.ParameterType))).Select(x => x.GetSignature()).ToArray();
                var fieldSignatures = oldType.Fields.Where(x => Test(x.FieldType) && (!x.FieldType.HasGenericParameters || x.FieldType.GenericParameters.All(Test))).Select(x => x.GetSignature()).ToArray();

                var types = latest.GetAllTypes()
                    .Where(t => t.Attributes.IgnoreVisibility() == oldType.Attributes.IgnoreVisibility())
                    .ToArray();

                foreach (var t in types)
                {
                    var points = 0d;
                    points += t.GetMethods().Count(m => !m.Name.IsObfuscated() && methodNames.Contains(m.Name));
                    points += t.Fields.Count(f => !f.Name.IsObfuscated() && fieldNames.Contains(f.Name)) * 2d;
                    points += t.Properties.Count(p => propertyNames.Contains((p.GetMethod?.Name ?? p.SetMethod.Name).Substring(4))) * 2d;

                    var fieldSignaturesPoints = fieldSignatures.Count(s => t.Fields.Any(f => f.GetSignature().ToString() == s.ToString()));
                    points += Math.Max(0, fieldSignaturesPoints - Math.Abs(t.Fields.Count - fieldSignaturesPoints)) / 2d;

                    var methodSignaturesPoints = methodSignatures.Count(s => t.GetMethods().Any(m => m.GetSignature().ToString() == s.ToString()));
                    points += Math.Max(0, methodSignaturesPoints - Math.Abs(t.GetMethods().Count() - methodSignaturesPoints) / 2d) / 2d;

                    if (points != 0)
                    {
                        list.TryAdd(points, t);
                    }
                }
            }

            foreach (var (oldType, list) in matches)
            {
                foreach (var (points, type) in list)
                {
                    if (matches.Where(x => x.Key != oldType).SelectMany(x => x.Value).Any(x => x.Value == type && x.Key > points))
                    {
                        continue;
                    }

                    list.Clear();
                    list.Add(double.MaxValue, type);

                    var mapped = new MappedType(new OriginalDescriptor { Name = type.FullName }, oldType.Name);

                    var i = 0;
                    foreach (var field in type.Fields)
                    {
                        var j = 0;
                        var i1 = i;
                        var oldFields = oldType.Fields.Where(x => j++ == i1 && x.GetSignature().ToString() == field.GetSignature().ToString()).ToArray();
                        if (oldFields.Length == 1)
                        {
                            var oldField = oldFields.First();
                            if (oldField.Name == field.Name || !field.Name.IsObfuscated())
                                continue;

                            mapped.Fields.Add(new MappedMember(new OriginalDescriptor { Index = i1 }, oldField.Name));
                        }

                        i++;
                    }

                    foreach (var method in type.Methods)
                    {
                        if (!method.HasParameters || method.IsSetter || method.IsGetter)
                        {
                            continue;
                        }

                        var oldMethods = oldType.Methods.Where(x => x.Name == method.Name && x.Parameters.Count == method.Parameters.Count).ToArray();
                        if (oldMethods.Length != 1)
                        {
                            continue;
                        }

                        var oldMethod = oldMethods.Single();

                        var oldParameters = oldMethod.Parameters.Select(x => x.Name).ToList();

                        if (method.Parameters.Select(x => x.Name).SequenceEqual(oldParameters))
                        {
                            continue;
                        }

                        mapped.Methods.Add(new MappedMethod(new OriginalDescriptor { Name = method.Name, Signature = method.GetSignature() }, null)
                        {
                            Parameters = oldParameters
                        });
                    }

                    if (type.Name == oldType.Name || (!type.Name.IsObfuscated() && !mapped.Fields.Any() && !mapped.Methods.Any()))
                    {
                        break;
                    }

                    result.Types.Add(mapped);

                    break;
                }
            }

            return result;
        }
    }
}
