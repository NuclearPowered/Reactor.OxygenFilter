using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using DepotDownloader;
using Newtonsoft.Json;

namespace Reactor.Greenhouse.Setup.Provider
{
    public class ItchProvider : BaseProvider
    {
        private Dictionary<GameVersion, int> VersionMap { get; } = new Dictionary<GameVersion, int>
        {
            [new GameVersion("2020.11.17i")] = 1047908,
        };

        public int UploadId { get; }

        public ItchProvider(GameVersion version) : base(version)
        {
            UploadId = VersionMap[version];
        }

        private HttpClient HttpClient { get; } = new HttpClient(new HttpClientHandler
        {
            CookieContainer = new CookieContainer()
        });

        public string Username { get; set; }
        public string Password { get; set; }

        public override void Setup()
        {
            var environmentVariable = Environment.GetEnvironmentVariable("ITCH");

            if (environmentVariable != null)
            {
                var split = environmentVariable.Split(":");
                Username = split[0];
                Password = split[1];
            }
            else
            {
                Console.Write("itch.io username: ");
                Username = Console.ReadLine();

                Console.Write("itch.io password: ");
                Password = Util.ReadPassword();
                Console.WriteLine();
            }
        }

        public override async Task DownloadAsync()
        {
            var htmlParser = new HtmlParser();

            var csrfResponse = await HttpClient.GetAsync("https://itch.io/login");

            var csrfDocument = htmlParser.ParseDocument(await csrfResponse.Content.ReadAsStreamAsync());

            var loginResponse = await HttpClient.PostAsync("https://itch.io/login", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("csrf_token", ((IHtmlInputElement) csrfDocument.QuerySelector("input[name=csrf_token]")).Value),
                new KeyValuePair<string, string>("username", Username),
                new KeyValuePair<string, string>("password", Password)
            }));

            if (!loginResponse.IsSuccessStatusCode || (await loginResponse.Content.ReadAsStringAsync()).Contains("Errors"))
            {
                throw new ProviderConnectionException(this, "Authentication failed");
            }

            Console.WriteLine("Logged into itch.io");

            var pageDocument = htmlParser.ParseDocument(await HttpClient.GetStringAsync("https://innersloth.itch.io/among-us"));
            var downloadPageUrl = ((IHtmlAnchorElement) pageDocument.QuerySelector("a[class=button]")).Href;

            var downloadDocument = htmlParser.ParseDocument(await HttpClient.GetStringAsync(downloadPageUrl));
            // var uploadId = ((IHtmlAnchorElement) downloadDocument.QuerySelector("a[class='button download_btn']")).Dataset["upload_id"];

            var keyRegex = new Regex("key\":\"(.+)\",");
            var key = downloadDocument.QuerySelectorAll("script[type='text/javascript']")
                .Cast<IHtmlScriptElement>()
                .Select(x => keyRegex.Match(x.InnerHtml))
                .Single(x => x.Success)
                .Groups[1].Value;

            var json = await HttpClient.PostAsync($"https://innersloth.itch.io/among-us/file/{UploadId}?key={key}", null);
            var response = JsonConvert.DeserializeObject<DownloadResponse>(await json.Content.ReadAsStringAsync());

            Console.WriteLine($"Downloading {response.Url}");

            var files = new[] { "GameAssembly.dll", "global-metadata.dat", "globalgamemanagers" };
            using var zipArchive = new ZipArchive(await HttpClient.GetStreamAsync(response.Url), ZipArchiveMode.Read);
            foreach (var entry in zipArchive.Entries)
            {
                if (files.Contains(entry.Name))
                {
                    var path = Path.Combine(Game.Path, entry.FullName[(entry.FullName.IndexOf("/", StringComparison.Ordinal) + 1)..]);
                    entry.ForceExtractToFile(path);
                }
            }
        }

        public override bool IsUpdateNeeded()
        {
            return !Directory.Exists(Game.Path);
        }

        public class DownloadResponse
        {
            public string Url { get; set; }
            public bool External { get; set; }
        }
    }
}
