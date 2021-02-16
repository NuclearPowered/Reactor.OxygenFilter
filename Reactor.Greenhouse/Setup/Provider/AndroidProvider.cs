using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Reactor.Greenhouse.Setup.Provider
{
    public class AndroidProvider : BaseProvider
    {
        private Dictionary<GameVersion, string> VersionMap { get; } = new Dictionary<GameVersion, string>
        {
            [new GameVersion("2020.11.17a")] = "https://www.apkmirror.com/wp-content/themes/APKMirror/download.php?id=1753109"
        };

        public string DownloadUrl { get; }

        public AndroidProvider(GameVersion version) : base(version)
        {
            DownloadUrl = VersionMap[version];
        }

        private HttpClient HttpClient { get; } = new HttpClient();

        public override void Setup()
        {
        }

        public override async Task DownloadAsync()
        {
            using var zipArchive = new ZipArchive(await HttpClient.GetStreamAsync(DownloadUrl), ZipArchiveMode.Read);

            zipArchive.GetEntry("lib/arm64-v8a/libil2cpp.so")!
                .ForceExtractToFile(Path.Combine(Game.Path, "libil2cpp.so"));

            zipArchive.GetEntry("assets/bin/Data/Managed/Metadata/global-metadata.dat")!
                .ForceExtractToFile(Path.Combine(Game.Path, "Among Us_Data", "il2cpp_data", "Metadata", "global-metadata.dat"));
        }

        public override bool IsUpdateNeeded()
        {
            return !Directory.Exists(Game.Path);
        }
    }
}
