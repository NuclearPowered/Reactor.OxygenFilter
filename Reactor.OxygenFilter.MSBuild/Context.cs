using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Reactor.OxygenFilter.MSBuild
{
    public static class Context
    {
        public static string RootPath { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? Path.Combine(Environment.GetEnvironmentVariable("XDG_CACHE_HOME") ?? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? ".", ".cache"), ".reactor")
            // Path#Combine is borken on visual studio
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + ".reactor";

        public static string TempPath { get; } = Path.Combine(RootPath, "tmp");
        public static string DataPath => RootPath + Path.DirectorySeparatorChar + GameVersion;
        public static string MappedPath => DataPath + Path.DirectorySeparatorChar + ComputeHash(MappingsJson);

        public static string GameVersion { get; internal set; }
        public static string MappingsJson { get; internal set; }

        public static string ComputeHash(FileInfo file)
        {
            using var md5 = MD5.Create();
            using var assemblyStream = file.OpenRead();

            var hash = md5.ComputeHash(assemblyStream);

            return ByteArrayToString(hash);
        }

        public static string ComputeHash(string text)
        {
            using var md5 = MD5.Create();

            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(text));

            return ByteArrayToString(hash);
        }

        public static string ByteArrayToString(byte[] data)
        {
            var builder = new StringBuilder(data.Length * 2);
            foreach (var b in data)
            {
                builder.AppendFormat("{0:x2}", b);
            }

            return builder.ToString();
        }
    }
}
