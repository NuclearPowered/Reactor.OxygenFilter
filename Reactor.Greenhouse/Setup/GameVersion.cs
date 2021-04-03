using System;
using System.Text.RegularExpressions;

namespace Reactor.Greenhouse.Setup
{
    public class GameVersion
    {
        private static readonly Regex _regex = new Regex(@"^(?<year>[0-9]+)\.(?<month>[0-9]+)\.(?<day>[0-9]+)(\.(?<patch>[0-9]+))?(?<platform>[sia])?", RegexOptions.Compiled);

        public static GamePlatform GamePlatformFromShorthand(string shorthand)
        {
            return shorthand switch
            {
                "s" => GamePlatform.Steam,
                "i" => GamePlatform.Itch,
                "a" => GamePlatform.Android,
                _ => throw new ArgumentOutOfRangeException(nameof(shorthand))
            };
        }

        public int Year { get; }
        public int Month { get; }
        public int Day { get; }
        public int Patch { get; }
        public GamePlatform? Platform { get; }

        public GameVersion(string version)
        {
            var match = _regex.Match(version);

            Year = int.Parse(match.Groups["year"].Value);
            Month = int.Parse(match.Groups["month"].Value);
            Day = int.Parse(match.Groups["day"].Value);
            Patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0;

            var platform = match.Groups["platform"];
            Platform = platform.Success && !string.IsNullOrEmpty(platform.Value) ? GamePlatformFromShorthand(platform.Value) : null;
        }

        public override string ToString()
        {
            return $"{Year}.{Month}.{Day}{(Patch == 0 ? string.Empty : $".{Patch}")}" + Platform switch
            {
                GamePlatform.Steam => "s",
                GamePlatform.Itch => "i",
                GamePlatform.Android => "a",
                null => string.Empty,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public bool Equals(GameVersion other, bool ignorePlatform = false)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return Year == other.Year && Month == other.Month && Day == other.Day && Patch == other.Patch && (ignorePlatform || Platform == other.Platform);
        }

        public override bool Equals(object obj)
        {
            return Equals((GameVersion) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Year, Month, Day, Patch, Platform);
        }
    }

    public enum GamePlatform
    {
        Steam,
        Itch,
        Android
    }
}
