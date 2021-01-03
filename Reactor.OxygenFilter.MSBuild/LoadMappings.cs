using System;
using System.IO;
using System.Net.Http;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Reactor.OxygenFilter.MSBuild
{
    public class LoadMappings : Task
    {
        [Required]
        public string GameVersion { get; set; }

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

            var split = Mappings.Split(':');
            var repo = split[0];
            var version = split[1];

            var directory = Path.Combine(Context.TempPath, repo.Replace("/", "_"), version);
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, $"{GameVersion}.json");

            if (File.Exists(file))
            {
                MappingsJson = File.ReadAllText(file);
                return true;
            }

            var httpClient = new HttpClient();
            var json = httpClient.GetStringAsync($"https://github.com/{repo}/releases/download/{version}/{GameVersion}.json").GetAwaiter().GetResult();

            MappingsJson = json;

            try
            {
                File.WriteAllText(file, json);
            }
            catch (Exception e)
            {
                Log.LogWarning("Failed to cache " + file + "\n" + e);
            }

            return true;
        }
    }
}
