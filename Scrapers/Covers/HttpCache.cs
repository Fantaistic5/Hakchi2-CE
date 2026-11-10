using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TeamShinkansen.Scrapers.Covers
{
    /// <summary>Cache disque (texte et résultats HEAD), 4 requêtes simultanées au plus par hôte.</summary>
    public sealed class HttpCache
    {
        private const int MaxPerHost = 4;
        private readonly string _dir;
        private readonly TimeSpan _maxAge;
        private readonly HttpClient _client;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _hosts =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        public HttpCache(string cacheDir, HttpMessageHandler handler = null, TimeSpan? maxAge = null)
        {
            if (string.IsNullOrEmpty(cacheDir)) throw new ArgumentNullException("cacheDir");
            _dir = cacheDir;
            _maxAge = maxAge ?? TimeSpan.FromDays(30);
            _client = handler != null ? new HttpClient(handler, false) : new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(60);
            _client.DefaultRequestHeaders.UserAgent.TryParseAdd("Hakchi2-CE-CoverScraper/1.0");
        }

        /// <summary>Contenu texte de l'URL ; null si 404. Les autres erreurs lèvent une exception.</summary>
        public async Task<string> GetTextAsync(string url, CancellationToken ct)
        {
            string path = PathFor(url, ".txt");
            string cached = ReadFresh(path);
            if (cached != null) return cached;

            var sem = SemaphoreFor(url);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                using (var resp = await _client.GetAsync(url, ct).ConfigureAwait(false))
                {
                    if (resp.StatusCode == HttpStatusCode.NotFound) return null;
                    resp.EnsureSuccessStatusCode();
                    string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Write(path, text);
                    return text;
                }
            }
            finally { sem.Release(); }
        }

        /// <summary>HEAD sur l'URL : vrai si 2xx, faux si 404. Résultat mis en cache.</summary>
        public async Task<bool> ExistsAsync(string url, CancellationToken ct)
        {
            string path = PathFor(url, ".head");
            string cached = ReadFresh(path);
            if (cached != null) return cached == "1";

            var sem = SemaphoreFor(url);
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Head, url))
                using (var resp = await _client.SendAsync(req, ct).ConfigureAwait(false))
                {
                    if (resp.StatusCode == HttpStatusCode.NotFound) { Write(path, "0"); return false; }
                    resp.EnsureSuccessStatusCode();
                    Write(path, "1");
                    return true;
                }
            }
            finally { sem.Release(); }
        }

        private SemaphoreSlim SemaphoreFor(string url)
        {
            string host = new Uri(url).Host;
            return _hosts.GetOrAdd(host, h => new SemaphoreSlim(MaxPerHost, MaxPerHost));
        }

        private string PathFor(string url, string ext)
        {
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return Path.Combine(_dir, sb + ext);
            }
        }

        private string ReadFresh(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > _maxAge) return null;
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private void Write(string path, string content)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(tmp, content, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (IOException) { /* cache au mieux */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
