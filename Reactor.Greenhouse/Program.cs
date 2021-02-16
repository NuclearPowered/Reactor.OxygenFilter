using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mono.Cecil;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Reactor.Greenhouse.Generation;
using Reactor.Greenhouse.Setup;
using Reactor.OxygenFilter;

namespace Reactor.Greenhouse
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            if (!args.Any())
            {
                Console.WriteLine("No game versions used!");
                return;
            }

            var gameVersions = args.Select(x => new GameVersion(x)).ToArray();

            var gameManager = new GameManager(gameVersions);

            await gameManager.SetupAsync();

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                ContractResolver = ShouldSerializeContractResolver.Instance,
            };

            var oldFile = Path.Combine("work", "Assembly-CSharp-2020.12.9s.dll");
            Console.WriteLine($"Generating mappings from {oldFile}");
            using var cleanModule = ModuleDefinition.ReadModule(File.OpenRead(oldFile));

            foreach (var game in gameManager.Games)
            {
                await GenerateAsync(game, cleanModule);
            }
        }

        private static async Task GenerateAsync(Game game, ModuleDefinition cleanModule)
        {
            var version = game.Provider.Version;
            Console.WriteLine($"Compiling mappings for {game.Name} ({version})");

            using var moduleDef = ModuleDefinition.ReadModule(File.OpenRead(game.Dll));

            var generated = Generator.Generate(new GenerationContext(cleanModule, moduleDef));

            await File.WriteAllTextAsync(Path.Combine("work", version + ".generated.json"), JsonConvert.SerializeObject(generated, Formatting.Indented));

            Apply(generated, Path.Combine("universal.json"));
            Apply(generated, Path.Combine(version + ".json"));

            generated.Compile(moduleDef);

            Directory.CreateDirectory(Path.Combine("bin"));
            await File.WriteAllTextAsync(Path.Combine("bin", version + ".json"), JsonConvert.SerializeObject(generated));
        }

        private static void Apply(Mappings generated, string file)
        {
            if (File.Exists(file))
            {
                var mappings = JsonConvert.DeserializeObject<Mappings>(File.ReadAllText(file));
                generated.Apply(mappings);
            }
        }

        public class ShouldSerializeContractResolver : CamelCasePropertyNamesContractResolver
        {
            public static ShouldSerializeContractResolver Instance { get; } = new ShouldSerializeContractResolver();

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);

                if (property.PropertyType != null && property.PropertyType != typeof(string))
                {
                    if (property.PropertyType.GetInterface(nameof(IEnumerable)) != null)
                    {
                        property.ShouldSerialize = instance => (instance?.GetType().GetProperty(property.UnderlyingName!)!.GetValue(instance) as IEnumerable<object>)?.Count() > 0;
                    }
                }

                return property;
            }
        }
    }
}
