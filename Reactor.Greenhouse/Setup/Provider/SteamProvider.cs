using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DepotDownloader;

namespace Reactor.Greenhouse.Setup.Provider
{
    public class SteamProvider : BaseProvider
    {
        private const uint AppId = 945360;
        private const uint DepotId = 945361;

        private Dictionary<GameVersion, ulong> VersionMap { get; } = new Dictionary<GameVersion, ulong>
        {
            [new GameVersion("2019.10.10s")] = 3162069540887216240,
            [new GameVersion("2020.12.9s")] = 3306639722673334636,
            [new GameVersion("2021.3.5s")] = 5200448423569257054
        };

        public ulong Manifest { get; }

        public SteamProvider(GameVersion version) : base(version)
        {
            Manifest = VersionMap[version];
        }

        public override bool IsUpdateNeeded()
        {
            try
            {
                DepotConfigStore.LoadFromFile(Path.Combine(Game.Path, ".DepotDownloader", "depot.config"));
                if (DepotConfigStore.Instance.InstalledManifestIDs.TryGetValue(DepotId, out var installedManifest))
                {
                    if (installedManifest == Manifest)
                    {
                        return false;
                    }
                }
            }
            finally
            {
                ContentDownloader.ShutdownSteam3();
            }

            return true;
        }

        public override void Setup()
        {
            if (ContentDownloader.steam3 != null && ContentDownloader.steam3.bConnected)
            {
                return;
            }

            AccountSettingsStore.LoadFromFile("account.config");

            var environmentVariable = Environment.GetEnvironmentVariable("STEAM");

            if (environmentVariable != null)
            {
                var split = environmentVariable.Split(":");
                if (!ContentDownloader.InitializeSteam3(split[0], split[1]))
                {
                    throw new ProviderConnectionException(this, "Incorrect credentials.");
                }
            }
            else
            {
                ContentDownloader.Config.RememberPassword = true;

                Console.Write("Steam username: ");
                var username = Console.ReadLine();

                string password = null;

                if (!AccountSettingsStore.Instance.LoginKeys.ContainsKey(username))
                {
                    Console.Write("Steam password: ");
                    password = ContentDownloader.Config.SuppliedPassword = Util.ReadPassword();
                    Console.WriteLine();
                }

                if (!ContentDownloader.InitializeSteam3(username, password))
                {
                    throw new ProviderConnectionException(this, "Incorrect credentials.");
                }
            }

            if (ContentDownloader.steam3 == null || !ContentDownloader.steam3.bConnected)
            {
                throw new ProviderConnectionException(this, "Unable to initialize Steam3 session.");
            }

            ContentDownloader.Config.UsingFileList = true;
            ContentDownloader.Config.FilesToDownload = new List<string>
            {
                "GameAssembly.dll"
            };
            ContentDownloader.Config.FilesToDownloadRegex = new List<Regex>
            {
                new Regex("^Among Us_Data/il2cpp_data/Metadata/global-metadata.dat$".Replace("/", "[\\\\|/]"), RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex("^Among Us_Data/globalgamemanagers$".Replace("/", "[\\\\|/]"), RegexOptions.Compiled | RegexOptions.IgnoreCase)
            };
        }

        public override Task DownloadAsync()
        {
            ContentDownloader.Config.InstallDirectory = Game.Path;
            return ContentDownloader.DownloadAppAsync(AppId, DepotId, Manifest);
        }
    }
}
