using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace hakchi_gui.Tests
{
    /// <summary>
    /// Nettoyage des dossiers jetables des tests (%TEMP%\hakchi_grid_tests_&lt;pid&gt;, sorties de la galerie...).
    /// Ces dossiers contiennent des JONCTIONS vers la sortie du build (hakchi_gui\bin\Debug\net48\art, data...) : on retire
    /// d'abord chaque point d'analyse SANS recursion (Directory.Delete(lien, false) supprime le lien, jamais la cible), puis le
    /// reste, avec un parcours qui ne descend jamais dans un point d'analyse.
    /// </summary>
    internal static class TempCleanup
    {
        private static readonly object gate = new object();
        private static readonly List<string> registered = new List<string>();
        private static bool hooked;

        /// <summary>Supprime <paramref name="dir"/> a la fin du processus (ou au dechargement du domaine de test).</summary>
        public static void Register(string dir)
        {
            lock (gate)
            {
                if (!registered.Contains(dir)) registered.Add(dir);
                if (hooked) return;
                hooked = true;
                AppDomain.CurrentDomain.ProcessExit += (s, e) => RunAll();
                AppDomain.CurrentDomain.DomainUnload += (s, e) => RunAll();
            }
        }

        private static void RunAll()
        {
            string[] dirs;
            lock (gate) { dirs = registered.ToArray(); registered.Clear(); }
            foreach (var d in dirs) DeleteTree(d);
        }

        /// <summary>
        /// Dossiers <paramref name="prefix"/>&lt;pid&gt; laisses par des processus de test qui ne tournent plus (arret brutal,
        /// fichiers verrouilles a la sortie) : supprimes au demarrage du suivant.
        /// </summary>
        public static void DeleteStale(string parent, string prefix)
        {
            string[] dirs;
            try { dirs = Directory.GetDirectories(parent, prefix + "*"); }
            catch (Exception) { return; }
            int me = Process.GetCurrentProcess().Id;
            foreach (var d in dirs)
            {
                int pid;
                if (!int.TryParse(Path.GetFileName(d).Substring(prefix.Length), out pid) || pid == me) continue;
                if (IsRunning(pid)) continue;
                DeleteTree(d);
            }
        }

        private static bool IsRunning(int pid)
        {
            try { using (var p = Process.GetProcessById(pid)) return !p.HasExited; }
            catch (ArgumentException) { return false; }
            catch (Exception) { return true; }      // dans le doute, on ne touche a rien
        }

        private static bool IsReparsePoint(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch (Exception) { return true; }      // attribut illisible : traite comme un lien (jamais parcouru)
        }

        /// <summary>Supprime l'arborescence sans jamais suivre une jonction : liens d'abord (non recursif), puis le reste.</summary>
        public static void DeleteTree(string root)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                if (IsReparsePoint(root)) { Directory.Delete(root, false); return; }
                var links = new List<string>();
                CollectReparsePoints(root, links);
                foreach (var link in links)
                    try { Directory.Delete(link, false); } catch (Exception) { }
                DeleteRest(root);
            }
            catch (Exception) { }
        }

        private static void CollectReparsePoints(string dir, List<string> links)
        {
            string[] subs;
            try { subs = Directory.GetDirectories(dir); }
            catch (Exception) { return; }
            foreach (var s in subs)
            {
                if (IsReparsePoint(s)) links.Add(s);
                else CollectReparsePoints(s, links);
            }
        }

        private static void DeleteRest(string dir)
        {
            string[] subs, files;
            try { subs = Directory.GetDirectories(dir); files = Directory.GetFiles(dir); }
            catch (Exception) { return; }
            foreach (var s in subs)
            {
                if (IsReparsePoint(s)) { try { Directory.Delete(s, false); } catch (Exception) { } }
                else DeleteRest(s);
            }
            foreach (var f in files)
            {
                try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch (Exception) { }
            }
            try { Directory.Delete(dir, false); } catch (Exception) { }
        }
    }
}
