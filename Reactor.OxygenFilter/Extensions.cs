using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Mono.Cecil;

namespace Reactor.OxygenFilter
{
    public static class Extensions
    {
        private static readonly PropertyInfo _suffix = typeof(ArrayType).GetProperty("Suffix", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Func<TypeSpecification, string> _callBaseFullName;

        static Extensions()
        {
            var dynamicMethod = new DynamicMethod("CallBaseFullname", typeof(string), new[] { typeof(TypeSpecification) }, typeof(Extensions));
            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(TypeSpecification).GetProperty(nameof(TypeSpecification.FullName))!.GetMethod);
            il.Emit(OpCodes.Ret);

            _callBaseFullName = (Func<TypeSpecification, string>) dynamicMethod.CreateDelegate(typeof(Func<TypeSpecification, string>));
        }

        public delegate string MapDelegate(MemberReference member, string original);

        public static void AppendMappedFullName(this StringBuilder sb, MemberReference member, MapDelegate map = null)
        {
            map ??= (_, original) => original;

            if (member is TypeSpecification typeSpecification)
            {
                sb.Append(map(typeSpecification, _callBaseFullName(typeSpecification)));

                if (typeSpecification is GenericInstanceType genericInstanceType)
                {
                    sb.Append("<");
                    var arguments = genericInstanceType.GenericArguments;
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(",");

                        sb.AppendMappedFullName(arguments[i], map);
                    }

                    sb.Append(">");
                }

                if (typeSpecification is ArrayType arrayType)
                {
                    sb.Append((string) _suffix.GetValue(arrayType));
                }

                if (typeSpecification is ByReferenceType)
                {
                    sb.Append("&");
                }
            }
            else
            {
                sb.Append(map(member, member.FullName));
            }
        }

        public static bool IsObfuscated(this string text)
        {
            return text.Length == 11 && text.All(char.IsUpper);
        }

        public static string GetSignature(this MethodDefinition methodDefinition, MapDelegate map = null)
        {
            var sb = new StringBuilder();

            sb.AppendMappedFullName(methodDefinition.ReturnType, map);
            sb.Append(" ");

            sb.Append("(");
            if (methodDefinition.HasParameters)
            {
                for (var i = 0; i < methodDefinition.Parameters.Count; i++)
                {
                    var parameterType = methodDefinition.Parameters[i].ParameterType;

                    if (i > 0)
                        sb.Append(",");

                    if (parameterType is SentinelType)
                        sb.Append("...,");

                    sb.AppendMappedFullName(parameterType, map);
                }
            }

            sb.Append(")");

            return sb.ToString();
        }

        public static string GetSignature(this FieldDefinition fieldDefinition)
        {
            return fieldDefinition.FieldType.FullName;
        }

        public static string GetSignature(this PropertyDefinition propertyDefinition)
        {
            return propertyDefinition.PropertyType.FullName;
        }
    }
}
