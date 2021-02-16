using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DepotDownloader;
using Reactor.Greenhouse.Setup.Provider;

namespace Reactor.Greenhouse.Setup
{
    public class GameManager
    {
        public string WorkPath { get; }

        public Game[] Games { get; }

        public GameManager(GameVersion[] gameVersions)
        {
            WorkPath = Path.GetFullPath("work");

            Games = gameVersions.Select(gameVersion => new Game(
                gameVersion.Platform switch
                {
                    GamePlatform.Steam => new SteamProvider(gameVersion),
                    GamePlatform.Itch => new ItchProvider(gameVersion),
                    GamePlatform.Android => new AndroidProvider(gameVersion),
                    _ => throw new ArgumentOutOfRangeException(nameof(gameVersions))
                }, gameVersion.ToString(), Path.Combine(WorkPath, gameVersion.ToString())
            )).ToArray();
        }

        public async Task SetupAsync()
        {
            foreach (var game in Games)
            {
                if (game.Provider.IsUpdateNeeded())
                {
                    await game.DownloadAsync();
                    Console.WriteLine($"Downloaded {game.Provider.Version}");
                }
            }

            ContentDownloader.ShutdownSteam3();

            foreach (var game in Games)
            {
                game.CheckVersion();
                game.Dump();
            }
        }
    }
}
