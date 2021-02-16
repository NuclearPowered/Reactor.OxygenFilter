using System;
using System.Text.RegularExpressions;

namespace Reactor.Greenhouse.Setup
{
    public class GameVersion
    {
        private static readonly Regex _regex = new Regex(@"^(?<major>[0-9]+)\.(?<minor>[0-9]+)\.(?<patch>[0-9]+)(?<platform>[sia])?", RegexOptions.Compiled);

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

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public GamePlatform? Platform { get; }

        public GameVersion(string version)
        {
            var match = _regex.Match(version);

            Major = int.Parse(match.Groups["major"].Value);
            Minor = int.Parse(match.Groups["minor"].Value);
            Patch = int.Parse(match.Groups["patch"].Value);

            var platform = match.Groups["platform"];
            Platform = platform.Success && !string.IsNullOrEmpty(platform.Value) ? GamePlatformFromShorthand(platform.Value) : null;
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}" + Platform switch
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

            return Major == other.Major && Minor == other.Minor && Patch == other.Patch && (ignorePlatform || Platform == other.Platform);
        }

        public override bool Equals(object obj)
        {
            return Equals((GameVersion) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Major, Minor, Patch, Platform);
        }
    }

    public enum GamePlatform
    {
        Steam,
        Itch,
        Android
    }
}
