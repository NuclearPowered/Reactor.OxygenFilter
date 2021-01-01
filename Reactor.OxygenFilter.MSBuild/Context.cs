using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Reactor.OxygenFilter.MSBuild
{
    public static class Context
    {
        // Path#Combine is borken on visual studio
        public static string DataPath { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + ".reactor";
        public static string TempPath { get; } = Path.Combine(DataPath, "temp");

        public static string ComputeHash(FileInfo file)
        {
            using var md5 = MD5.Create();
            using var assemblyStream = file.OpenRead();

            var hash = md5.ComputeHash(assemblyStream);

            return Encoding.UTF8.GetString(hash);
        }

        public static string ComputeHash(string text)
        {
            using var md5 = MD5.Create();

            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(text));

            return Encoding.UTF8.GetString(hash);
        }
    }
}
