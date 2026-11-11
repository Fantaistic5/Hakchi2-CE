using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using com.clusterrr.hakchi_gui.Properties;
using TeamShinkansen.Scrapers.Covers;

namespace com.clusterrr.hakchi_gui.Covers
{
    /// <summary>
    /// Construction paresseuse des sources de jaquettes : rien n'est cree au demarrage de hakchi,
    /// tout est cree a l'ouverture de la fenetre « Chercher les jaquettes ».
    /// </summary>
    internal static class CoverServices
    {
        // Sous net48, ServicePointManager.DefaultConnectionLimit vaut 2 par defaut et bride les requetes paralleles.
        private const int ConnectionLimit = 8;
        private static readonly object Sync = new object();
        private static HttpCache _cache;       // instance unique (cache disque partage)
        private static HttpClient _images;

        public static CoverSearchService CreateSearchService()
        {
            lock (Sync)
            {
                if (ServicePointManager.DefaultConnectionLimit < ConnectionLimit)
                    ServicePointManager.DefaultConnectionLimit = ConnectionLimit;
                if (_cache == null)
                    _cache = new HttpCache(Path.Combine(Program.BaseDirectoryExternal, "cache", "covers"));
            }
            return new CoverSearchService(new ICoverSource[]
            {
                new LibretroThumbnailsSource(_cache, LibretroNames.DatUrl),
                new TheGamesDbCoverSource(Resources.TheGamesDBKey),
                new GoogleCoverSource()
            });
        }

        /// <summary>Telecharge et decode une image. Leve une exception en cas d'echec reseau ou d'image invalide.</summary>
        public static async Task<Bitmap> DownloadImageAsync(string url, CancellationToken ct)
        {
            byte[] data = await DownloadBytesAsync(url, ct);
            using (var ms = new MemoryStream(data))
            using (var img = Image.FromStream(ms))
                return new Bitmap(img); // copie detachee du flux
        }

        /// <summary>
        /// Telecharge une image sans la decoder (octets du fichier). Leve une exception en cas d'echec reseau.
        /// La recherche groupee garde ces octets (quelques centaines de Ko) plutot qu'une image decodee pleine taille (plusieurs Mo).
        /// </summary>
        public static async Task<byte[]> DownloadBytesAsync(string url, CancellationToken ct)
        {
            HttpClient client;
            lock (Sync)
            {
                if (_images == null)
                {
                    _images = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    _images.DefaultRequestHeaders.UserAgent.TryParseAdd("Hakchi2-CE-CoverScraper/1.0");
                }
                client = _images;
            }
            using (var resp = await client.GetAsync(url, ct))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsByteArrayAsync();
            }
        }
    }
}
