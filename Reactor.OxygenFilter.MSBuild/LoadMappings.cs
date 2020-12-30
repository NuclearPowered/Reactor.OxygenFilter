using System.IO;
using System.Net.Http;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Reactor.OxygenFilter.MSBuild
{
    public class LoadMappings : Task
    {
        public string TargetGamePlatform { get; set; } = "Steam";

        [Required]
        public string Mappings { get; set; }

        [Output]
        public string MappingsJson { get; set; }

        public override bool Execute()
        {
            if (File.Exists(Mappings))
            {
                MappingsJson = File.ReadAllText(Mappings);
                return true;
            }

            Directory.CreateDirectory(Context.TempPath);
            var file = Path.Combine(Context.TempPath, Mappings.Replace("/", "_") + $"-{TargetGamePlatform.ToLower()}" + ".json");

            if (File.Exists(file))
            {
                MappingsJson = File.ReadAllText(file);
                return true;
            }

            var split = Mappings.Split(':');
            var repo = split[0];
            var version = split[1];

            var httpClient = new HttpClient();
            var json = httpClient.GetStringAsync($"https://github.com/{repo}/releases/download/{version}/{TargetGamePlatform.ToLower()}.json").GetAwaiter().GetResult();

            MappingsJson = json;
            File.WriteAllText(file, json);

            return true;
        }
    }
}
