using System;
using System.IO;
using System.Threading.Tasks;
using DepotDownloader;
using Reactor.Greenhouse.Setup.Provider;

namespace Reactor.Greenhouse.Setup
{
    public class GameManager
    {
        public string WorkPath { get; }

        public Game Steam { get; }
        public Game Itch { get; }

        public GameManager()
        {
            WorkPath = Path.GetFullPath("work");
            Steam = new Game(new SteamProvider(false), "steam", Path.Combine(WorkPath, "steam"));
            Itch = new Game(new ItchProvider(), "itch", Path.Combine(WorkPath, "itch"));
        }

        public async Task SetupAsync(bool setupSteam, bool setupItch)
        {
            var steam = setupSteam && Steam.Provider.IsUpdateNeeded();
            var itch = setupItch && Itch.Provider.IsUpdateNeeded();

            if (steam || itch)
            {
                ContentDownloader.ShutdownSteam3();

                if (steam)
                {
                    await Steam.DownloadAsync();
                    Console.WriteLine($"Downloaded {nameof(Steam)} ({Steam.Version})");
                }

                if (itch)
                {
                    await Itch.DownloadAsync();
                    Console.WriteLine($"Downloaded {nameof(Itch)} ({Itch.Version})");
                }
            }

            ContentDownloader.ShutdownSteam3();

            if (setupSteam)
            {
                Steam.UpdateVersion();
            }

            if (setupItch)
            {
                Itch.UpdateVersion();
            }

            if (setupSteam)
            {
                Steam.Dump();
            }

            if (setupItch)
            {
                Itch.Dump();
            }
        }
    }
}
