using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace FormGallery
{
    internal sealed class Options
    {
        public string Out;
        public int Dpi = 96;
        public string Only;
        public string DumpBaselines;
        public string AppDir;
        public string StateDir;
        public string Culture = "fr-FR";
        public bool List;
    }

    internal static class Program
    {
        private const string Usage =
            "FormGallery.exe --out <dir> [--dpi 96|144] [--only <FormName>] [--culture fr-FR] [--appdir <dir>] [--state <dir>]\n" +
            "FormGallery.exe --dump-baselines <dir> [--appdir <dir>] [--state <dir>]\n" +
            "FormGallery.exe --list";

        [STAThread]
        private static int Main(string[] args)
        {
            // Doublure de pnputil.exe : WaitingFelForm / WaitingShellForm lancent "pnputil -e" a la construction
            // (absent de la vue WOW64 d'un processus x86) et, si le pilote manque, l'installeur du pilote avec elevation.
            // La galerie se copie sous ce nom a cote d'elle (lancee depuis son dossier, elle passe avant System32) et repond
            // que le pilote est installe : l'installeur n'est donc jamais lance.
            if (string.Equals(Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]), "pnputil", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("USB\\VID_1F3A&PID_EFE8");
                return 0;
            }

            Options o;
            try { o = Parse(args); }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(Usage);
                return 2;
            }
            if (o.Out == null && o.DumpBaselines == null && !o.List)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            int rc = 0;
            try
            {
                Application.EnableVisualStyles();
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, ev) => Console.WriteLine("exception UI interceptee : " + ev.Exception);
                Application.SetCompatibleTextRenderingDefault(false);
                Watchdog.Start();

                var state = o.StateDir ?? Path.Combine(Path.GetTempPath(), "FormGallery_state");
                Host.Init(o.AppDir, state);
                Console.WriteLine("hakchi.exe : " + Host.Asm.Location);
                Console.WriteLine("dossier appli (lecture seule) : " + Host.AppDir);
                Console.WriteLine("dossier d'etat (config) : " + Host.StateDir);

                if (o.List) Host.PrintList();
                if (o.DumpBaselines != null) Baselines.Dump(o.DumpBaselines);
                if (o.Out != null) rc = Gallery.Run(o);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERREUR FATALE : " + ex);
                rc = 1;
            }
            Console.Out.Flush();
            // Des threads de fond de hakchi (chargement des caches) peuvent survivre : on sort net.
            Environment.Exit(rc);
            return rc;
        }

        private static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                Func<string> next = () =>
                {
                    if (i + 1 >= args.Length) throw new ArgumentException("Valeur manquante pour " + a);
                    return args[++i];
                };
                switch (a)
                {
                    case "--out": o.Out = Path.GetFullPath(next()); break;
                    case "--dpi":
                        o.Dpi = int.Parse(next(), CultureInfo.InvariantCulture);
                        if (o.Dpi != 96 && o.Dpi != 144) throw new ArgumentException("--dpi vaut 96 ou 144");
                        break;
                    case "--only": o.Only = next(); break;
                    case "--dump-baselines": o.DumpBaselines = Path.GetFullPath(next()); break;
                    case "--appdir": o.AppDir = Path.GetFullPath(next()); break;
                    case "--state": o.StateDir = Path.GetFullPath(next()); break;
                    case "--culture": o.Culture = next(); break;
                    case "--list": o.List = true; break;
                    default: throw new ArgumentException("Argument inconnu : " + a);
                }
            }
            return o;
        }
    }
}
