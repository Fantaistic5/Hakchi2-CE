using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace FormGallery
{
    /// <summary>Charge hakchi.exe par reflexion, le configure sans console, construit ses fenetres.</summary>
    internal static class Host
    {
        public static Assembly Asm;
        public static string AppDir;
        public static string StateDir;
        public const string RootNamespace = "com.clusterrr.hakchi_gui";

        public static void Init(string appDirOverride, string stateDir)
        {
            var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hakchi.exe");
            if (!File.Exists(local)) throw new FileNotFoundException("hakchi.exe absent a cote de FormGallery.exe (la reference n'a pas ete copiee)", local);
            Asm = Assembly.LoadFrom(local);

            AppDir = appDirOverride ?? FindBuildOutput();
            StateDir = stateDir;
            Directory.CreateDirectory(StateDir);

            // Program.Main n'est pas execute : on pose a la main ce qu'il initialiserait.
            var prog = Asm.GetType(RootNamespace + ".Program", true);
            SetStatic(prog, "BaseDirectoryInternal", AppDir); // ressources (mods, data, languages) : lecture seule
            SetStatic(prog, "BaseDirectoryExternal", StateDir); // config.json, cache : dossier jetable
            SetStatic(prog, "isPortable", true);
            var spine = prog.GetField("_SpineTemplates", BindingFlags.NonPublic | BindingFlags.Static);
            if (spine != null && spine.GetValue(null) == null)
                spine.SetValue(null, Activator.CreateInstance(spine.FieldType));

            LinkExternalDirs();
            LoadSamples();

            Directory.SetCurrentDirectory(AppDir);
#pragma warning disable 618
            AppDomain.CurrentDomain.AppendPrivatePath("languages"); // relatif au dossier de FormGallery.exe (satellites copies a la compilation)
#pragma warning restore 618
        }

        // Dossiers que hakchi relit depuis BaseDirectoryExternal : jonctions vers le build (lecture), config jetable a part.
        private static readonly string[] ExternalDirs = { "art", "folder_images", "info", "patches", "sfrom_tool", "user_mods", "spine_templates", "data" };

        private static void LinkExternalDirs()
        {
            foreach (var d in ExternalDirs)
            {
                var link = Path.Combine(StateDir, d);
                var target = Path.Combine(AppDir, d);
                if (Directory.Exists(link) || !Directory.Exists(target)) continue;
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var p = System.Diagnostics.Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(); }
            }
        }

        // Jeux d'exemple copies depuis la copie de test (lecture seule) pour fournir de vrais NesApplication aux fenetres qui en exigent un.
        public static object SampleNes, SampleSnes;
        public static string SamplesSource = "P:" + Path.DirectorySeparatorChar + "HakchiSNESUSA" + Path.DirectorySeparatorChar + "app" + Path.DirectorySeparatorChar + "games";

        private static void LoadSamples()
        {
            try
            {
                var appType = Asm.GetType(RootNamespace + ".NesApplication", false);
                if (appType == null) return;
                // MainForm le fait dans FormInitialize ; NesApplication.FromDirectory en depend.
                var core = Asm.GetType(RootNamespace + ".CoreCollection", false);
                var load = core == null ? null : core.GetMethod("Load", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (load != null) load.Invoke(null, null);
                var from = appType.GetMethod("FromDirectory", BindingFlags.Public | BindingFlags.Static);
                SampleNes = LoadSample(from, "CLV-Z-CKTTS");
                SampleSnes = LoadSample(from, "CLV-U-YABAN");
            }
            catch (Exception ex) { Console.WriteLine("jeux d'exemple indisponibles : " + (ex.InnerException ?? ex).ToString()); }
        }

        private static object LoadSample(MethodInfo fromDirectory, string code)
        {
            var src = Path.Combine(SamplesSource, code);
            if (!Directory.Exists(src)) return null;
            var dst = Path.Combine(StateDir, "games", code);
            CopyDir(src, dst);
            return fromDirectory.Invoke(null, new object[] { dst, false });
        }

        private static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        private static void SetStatic(Type t, string field, object value)
        {
            var f = t.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) throw new MissingFieldException(t.FullName, field);
            f.SetValue(null, value);
        }

        private static string FindBuildOutput()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var cand = Path.Combine(dir.FullName, "hakchi_gui", "bin", "Debug", "net48");
                if (File.Exists(Path.Combine(cand, "hakchi.exe"))) return cand;
                dir = dir.Parent;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>Tous les Form et UserControl concrets de hakchi.exe.</summary>
        public static List<Type> UiTypes()
        {
            Type[] all;
            try { all = Asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { all = ex.Types.Where(t => t != null).ToArray(); }
            return all
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition && !t.IsNested
                            && t.Namespace != null && t.Namespace.StartsWith(RootNamespace, StringComparison.Ordinal)
                            && (typeof(Form).IsAssignableFrom(t) || typeof(UserControl).IsAssignableFrom(t)))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();
        }

        public static string KindOf(Type t) => typeof(Form).IsAssignableFrom(t) ? "Form" : "UserControl";

        public static void PrintList()
        {
            foreach (var t in UiTypes())
            {
                Console.WriteLine(KindOf(t) + "\t" + t.FullName);
                foreach (var c in t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    Console.WriteLine("\t(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
            }
        }

        /// <summary>Construit une fenetre avec des arguments factices simples. reason est renseigne en cas d'echec.</summary>
        public static Control Construct(Type t, out string ctorDescription, out string reason)
        {
            ctorDescription = null;
            reason = null;
            var errors = new List<string>();
            var ctors = t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .OrderBy(c => c.GetParameters().Length).ToList();
            if (ctors.Count == 0) { reason = "aucun constructeur d'instance"; return null; }
            if (DesignerOnlyTypes.Contains(t.Name))
            {
                ctorDescription = DesignerOnlyDescription;
                return ConstructDesignerOnly(t);
            }
            if (t.Name == "CoverBatchForm")
            {
                // jeux factices, sources et telechargements factices : jamais le reseau, rien n'est ecrit (voir CoverBatchSample)
                ctorDescription = "CoverBatchSample (jeux, sources et images factices)";
                return CoverBatchSample.Build();
            }
            if (t.Name == "Motd")
            {
                // vrai constructeur, message d'exemple local (Markdown sans image ni lien distant charge) : la page est posee comme
                // dans Motd_Shown (retire par la galerie), voir FormSamples ; aucun acces reseau
                ctorDescription = "Motd(message d'exemple)";
                var motd = (Control)t.GetConstructor(new[] { typeof(string) }).Invoke(new object[] { MotdSample });
                StopTimers(motd);
                return motd;
            }
            if (t.Name == "MessageForm")
            {
                // vrai chemin d'affichage (MessageForm.Get) : textes et boutons Oui / Non / Annuler poses, sans ShowDialog
                var buttonType = t.GetNestedType("Button");
                var answers = Array.CreateInstance(buttonType, 3);
                answers.SetValue(Enum.Parse(buttonType, "Yes"), 0); answers.SetValue(Enum.Parse(buttonType, "No"), 1); answers.SetValue(Enum.Parse(buttonType, "Cancel"), 2);
                var icon = Asm.GetType(RootNamespace + ".Properties.Resources", true).GetProperty("sign_warning", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null);
                var get = t.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                var defaultButton = Enum.Parse(t.GetNestedType("DefaultButton"), "Button2");
                ctorDescription = "MessageForm.Get(titre, message, sign_warning, Oui/Non/Annuler, Button2)";
                return (Control)get.Invoke(null, new object[] { "Etes-vous sur ?", "Voulez-vous vraiment annuler ? La console pourrait rester dans un etat instable.", icon, answers, defaultButton });
            }

            foreach (var c in ctors)
            {
                var ps = c.GetParameters();
                var args = new object[ps.Length];
                string unsupported = null;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (!TryDummy(ps[i].ParameterType, out args[i])) { unsupported = ps[i].ParameterType.Name + " " + ps[i].Name; break; }
                }
                var sig = "(" + string.Join(", ", ps.Select(p => p.ParameterType.Name + " " + p.Name)) + ")";
                if (unsupported != null) { errors.Add(sig + " : parametre sans valeur factice (" + unsupported + ")"); continue; }
                try
                {
                    var ctrl = (Control)c.Invoke(args);
                    StopTimers(ctrl);
                    ctorDescription = sig + (ps.Length > 0 ? " avec valeurs factices" : "");
                    return ctrl;
                }
                catch (TargetInvocationException ex)
                {
                    var inner = ex.InnerException ?? ex;
                    errors.Add(sig + " : " + inner.GetType().Name + " : " + FirstLine(inner.Message));
                }
                catch (Exception ex)
                {
                    errors.Add(sig + " : " + ex.GetType().Name + " : " + FirstLine(ex.Message));
                }
            }
            reason = string.Join(" | ", errors);
            return null;
        }

        /// <summary>
        /// Fenetres dont le constructeur applicatif ne doit JAMAIS s'executer dans la galerie : WaitingFelForm appelle
        /// DriverInstalled() || InstallDriver() (installeur du pilote avec elevation "runas"), WaitingShellForm interroge pnputil.
        /// Elles sont construites comme le concepteur : objet non initialise, constructeur de B1Form, InitializeComponent, minuteurs coupes.
        /// Aucun chemin ne mene a pnputil ni a l'installeur (pas de controle prealable du pilote, donc pas de « verifier puis agir »).
        /// SaveStateManager (fil qui attend la console : WaitingShellForm.WaitForDevice) et ScreenshotForm (fil de capture qui parle a
        /// la console, dossier de captures cree) : jamais construites par leur constructeur applicatif non plus.
        /// ModHubForm (Hmod.GetMods : liste des mods installes, interroge la console si elle est en ligne) et SelectForm (GetMods,
        /// renommage dans user_mods, CoreCollection rechargee, ConfigIni) : concepteur seul, puis donnees d'exemple (ModHubSample).
        /// </summary>
        public static readonly string[] DesignerOnlyTypes = { "WaitingFelForm", "WaitingShellForm", "SaveStateManager", "ScreenshotForm", "ModHubForm", "SelectForm" };
        public const string DesignerOnlyDescription = "concepteur seul (B1Form + InitializeComponent, sans constructeur applicatif)";

        public static Control ConstructDesignerOnly(Type t)
        {
            var b1 = Asm.GetType(RootNamespace + ".Theme.B1Form", true);
            if (!b1.IsAssignableFrom(t)) throw new InvalidOperationException(t.Name + " n'herite pas de B1Form : construction concepteur impossible");
            var f = (Form)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
            b1.GetConstructor(Type.EmptyTypes).Invoke(f, null);
            t.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(f, null);
            StopTimers(f);
            return f;
        }

        /// <summary>
        /// Les fenetres d'attente demarrent un minuteur a la construction (WaitingFelForm : detection USB puis poignee de main FEL).
        /// On l'arrete avant tout traitement de messages : la galerie ne touche jamais un peripherique USB / FEL ni la console.
        /// </summary>
        public static void StopTimers(Control c)
        {
            for (var t = c.GetType(); t != null && t != typeof(Form) && t != typeof(Control); t = t.BaseType)
                foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (typeof(Timer).IsAssignableFrom(fi.FieldType))
                    {
                        var timer = fi.GetValue(c) as Timer;
                        if (timer != null) timer.Enabled = false;
                    }
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var i = s.IndexOfAny(new[] { '\r', '\n' });
            return i < 0 ? s : s.Substring(0, i);
        }

        internal const string MotdSample =
            "## Bienvenue dans hakchi\n\n" +
            "Nouveautes de cette version :\n\n" +
            "- interface **B1** pour toutes les fenetres ;\n" +
            "- gestion des depots de mods ;\n" +
            "- jaquettes en lot.\n\n" +
            "> Pensez a sauvegarder votre NAND avant tout flash.\n\n" +
            "Plus d'informations sur [le wiki](https://example.invalid/wiki).\n";

        private static bool TryDummy(Type t, out object value)
        {
            value = null;
            if (t.Name == "NesApplication" || t.Name == "SnesGame" || t.Name == "NesGame")
            {
                var sample = t.Name == "SnesGame" ? SampleSnes : SampleNes;
                if (sample != null && t.IsInstanceOfType(sample)) { value = sample; return true; }
                return false;
            }
            if (t.Name == "Tasker") { value = Activator.CreateInstance(t, new Form()); return true; }
            if (t.IsGenericType && t.GetGenericArguments().Length == 1 &&
                (t.GetGenericTypeDefinition() == typeof(IEnumerable<>) || t.GetGenericTypeDefinition() == typeof(List<>)))
            {
                var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(t.GetGenericArguments()[0]));
                if (t.GetGenericArguments()[0].Name == "NesApplication" && SampleNes != null) list.Add(SampleNes);
                value = list;
                return true;
            }
            if (t == typeof(string)) { value = ""; return true; }
            if (t.IsEnum) { var vals = Enum.GetValues(t); value = vals.Length > 0 ? vals.GetValue(0) : Activator.CreateInstance(t); return true; }
            if (t.IsValueType) { value = Activator.CreateInstance(t); return true; }
            if (t.IsArray) { value = Array.CreateInstance(t.GetElementType(), 0); return true; }
            if (t.IsInterface || t.IsAbstract) { value = null; return t.IsInterface; } // null pour une interface (ex. IWin32Window)
            if (typeof(Control).IsAssignableFrom(t)) { value = null; return true; }
            if (t.GetConstructor(Type.EmptyTypes) != null)
            {
                try { value = Activator.CreateInstance(t); return true; } catch { }
            }
            return false;
        }

        public static string Name(Type t) => t.Name;
    }
}
