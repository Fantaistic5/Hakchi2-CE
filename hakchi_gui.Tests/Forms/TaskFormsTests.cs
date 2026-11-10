using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Tasks;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using hakchi_gui.Tests.Grid;
using hakchi_gui.Tests.Shell;
using hakchi_gui.Tests.Theme;
using Newtonsoft.Json.Linq;
using Xunit;

namespace hakchi_gui.Tests.Forms
{
    /// <summary>
    /// Fenetres de taches, de flash et d'attente (Task 7) : APPARENCE SEULEMENT. Memes controles (nom, type, parent),
    /// fichiers de logique inchanges hormis la classe de base, memes DialogResult / Accept / Cancel, palette B1.
    /// Aucune fenetre n'est affichee, aucun peripherique USB / FEL ni la console n'est touche : les fenetres sont construites
    /// « comme le concepteur » (constructeur de B1Form + InitializeComponent), sans leur constructeur applicatif
    /// (pnputil et installeur du pilote, minuteurs de detection, abonnements a la console, lecture d'un jeu).
    /// </summary>
    [Collection("B1Static")]
    public class TaskFormsTests
    {
        // nom de type sous com.clusterrr.hakchi_gui (cle du fichier de reference)
        public static readonly string[] Names =
        {
            "Tasks.TaskerForm", "Tasks.TaskerTransferForm", "Tasks.MessageForm", "Tasks.ErrorForm", "Tasks.SDFormat",
            "WaitingFelForm", "WaitingShellForm", "WaitingShellCycleForm", "SelectNesButtonsForm", "SelectSnesButtonsForm", "SnesPresetEditor",
        };

        public static IEnumerable<object[]> All() { return Names.Select(n => new object[] { n }); }

        private static readonly Assembly Asm = typeof(NesApplication).Assembly;

        private static Type TypeOf(string name) { return Asm.GetType("com.clusterrr.hakchi_gui." + name, true); }

        private static string Short(string name) { return name.Substring(name.LastIndexOf('.') + 1); }

        private static JObject Before(string name)
        {
            var all = JObject.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "task_forms_before.json")));
            return (JObject)all["forms"][name];
        }

        /// <summary>
        /// Construit la fenetre telle que le concepteur la decrit : objet non initialise, constructeur de B1Form, puis InitializeComponent.
        /// Le constructeur applicatif n'est pas execute (WaitingFelForm : pnputil puis installeur du pilote avec elevation, minuteur
        /// de detection FEL ; WaitingShellCycleForm : abonnement a la console ; SnesPresetEditor : decompression du jeu).
        /// Le handle est cree hors ecran (habillage B1 a OnHandleCreated), la fenetre n'est jamais affichee.
        /// </summary>
        internal static Form CreateDesigned(string name)
        {
            MainFormEnv.Init();
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var t = TypeOf(name);
            var f = (Form)FormatterServices.GetUninitializedObject(t);
            typeof(B1Form).GetConstructor(Type.EmptyTypes).Invoke(f, null);
            t.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, null);
            StopTimers(f);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-30000, -30000);
            f.ShowInTaskbar = false;
            CreateHandles(f);
            f.PerformLayout();
            return f;
        }

        private static void StopTimers(Form f)
        {
            for (var t = f.GetType(); t != null && t != typeof(Form); t = t.BaseType)
                foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    if (typeof(System.Windows.Forms.Timer).IsAssignableFrom(fi.FieldType))
                    {
                        var timer = (System.Windows.Forms.Timer)fi.GetValue(f);
                        if (timer != null) timer.Enabled = false;
                    }
        }

        private static void CreateHandles(Control c)
        {
            var h = c.Handle;
            foreach (Control child in c.Controls) CreateHandles(child);
        }

        private static IEnumerable<Control> Tree(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var d in Tree(c)) yield return d;
            }
        }

        private static T Field<T>(object o, string name) where T : class
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (fi != null) return fi.GetValue(o) as T;
            }
            return null;
        }

        [StaTheory, MemberData(nameof(All))]
        public void InheritsB1Form(string name)
        {
            Assert.True(typeof(B1Form).IsAssignableFrom(TypeOf(name)), name);
        }

        // Aucun controle ajoute, retire, renomme, retype ni deplace dans un autre parent.
        [StaTheory, MemberData(nameof(All))]
        public void DesignerControlSetUnchanged(string name)
        {
            var before = Before(name)["controls"].Select(c => (string)c["parent"] + "/" + (string)c["name"] + ":" + (string)c["type"]).OrderBy(x => x, StringComparer.Ordinal).ToList();
            using (var f = CreateDesigned(name))
            {
                var now = new List<string>();
                Action<Control, string> walk = null;
                walk = (parent, parentName) =>
                {
                    foreach (Control c in parent.Controls)
                    {
                        if (string.IsNullOrEmpty(c.Name)) continue;                 // controles internes (non declares par le concepteur)
                        now.Add(parentName + "/" + c.Name + ":" + c.GetType().Name);
                        walk(c, c.Name);
                    }
                };
                walk(f, "$this");
                now.Sort(StringComparer.Ordinal);
                Assert.Equal(before, now);
            }
        }

        // Le fichier de logique (.cs) est identique a celui de la base, au changement de classe de base pres.
        private static readonly Regex BaseClass = new Regex(@"(partial class \w+ : )com\.clusterrr\.hakchi_gui\.Theme\.B1Form\b");

        [Theory, MemberData(nameof(All))]
        public void NoLogicFileChanged(string name)
        {
            var b = Before(name);
            var path = Path.Combine(Repo.Root, "hakchi_gui", name.Replace('.', Path.DirectorySeparatorChar) + ".cs");
            var text = File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿').Replace("\r\n", "\n");
            Assert.True(BaseClass.IsMatch(text), name + " : classe de base B1Form absente");
            var restored = BaseClass.Replace(text, "${1}Form", 1);
            string hash;
            using (var sha = SHA256.Create())
                hash = string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(restored)).Select(x => x.ToString("x2")));
            Assert.True((string)b["logicSha256"] == hash,
                name + ".cs differe de " + (string)b["logicRev"] + " au-dela de la classe de base (voir : git diff " + (string)b["logicRev"] + " -- " + path + ")");
        }

        [StaTheory, MemberData(nameof(All))]
        public void SameDialogResultsAndAcceptCancel(string name)
        {
            var b = Before(name);
            using (var f = CreateDesigned(name))
            {
                Assert.Equal((string)b["accept"], (f.AcceptButton as Control)?.Name);
                Assert.Equal((string)b["cancel"], (f.CancelButton as Control)?.Name);
                foreach (var p in (JObject)b["dialogResults"])
                {
                    var btn = Tree(f).OfType<Button>().Single(x => x.Name == p.Key);
                    Assert.Equal((string)p.Value, btn.DialogResult.ToString());
                }
            }
        }

        // MessageForm, construite par son vrai chemin (Get) : aucun DialogResult, ni Accept ni Cancel, chaque bouton rend toujours sa reponse.
        [StaTheory]
        [InlineData(0, MessageForm.Button.Yes)]
        [InlineData(1, MessageForm.Button.No)]
        [InlineData(2, MessageForm.Button.Cancel)]
        public void MessageFormButtonsKeepDialogResults(int index, MessageForm.Button expected)
        {
            MainFormEnv.Init();
            var answers = new[] { MessageForm.Button.Yes, MessageForm.Button.No, MessageForm.Button.Cancel };
            var f = MessageForm.Get("Titre", "Un message", null, answers, MessageForm.DefaultButton.Button2);
            try
            {
                Assert.True(f is B1Form);
                Assert.Null(f.AcceptButton);
                Assert.Null(f.CancelButton);
                var buttons = new[] { "button1", "button2", "button3" }.Select(n => Field<Button>(f, n)).ToArray();
                foreach (var btn in buttons) Assert.Equal(DialogResult.None, btn.DialogResult);
                foreach (var btn in buttons) Assert.NotEqual(B1ButtonKind.Danger, B1Skin.KindOf(btn));
                // clic : le resultat est celui du bouton, comme avant (OnClick : la fenetre n'est pas affichee, PerformClick l'ignorerait)
                typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(buttons[index], new object[] { EventArgs.Empty });
                var result = f.GetType().GetField("result", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f);
                Assert.Equal(expected, (MessageForm.Button)result);
            }
            finally { f.Dispose(); }
        }

        // Danger : seuls les boutons qui lancent un flash, un formatage ou un effacement. Ici : le formatage de la carte SD.
        [StaTheory, MemberData(nameof(All))]
        public void DangerOnlyOnDestructiveButtons(string name)
        {
            var danger = new HashSet<string> { "Tasks.SDFormat/btnOk" };
            using (var f = CreateDesigned(name))
                foreach (var b in Tree(f).OfType<ButtonBase>())
                    Assert.True((B1Skin.KindOf(b) == B1ButtonKind.Danger) == danger.Contains(name + "/" + b.Name), name + "." + b.Name + " : " + B1Skin.KindOf(b));
        }

        [StaTheory, MemberData(nameof(All))]
        public void PaletteColorProperties(string name)
        {
            using (var f = CreateDesigned(name))
            {
                Assert.True(Sample.InPalette(f.BackColor) && Sample.InPalette(f.ForeColor), name + " fenetre");
                foreach (var c in Tree(f))
                {
                    if (c.BackColor != Color.Transparent) Assert.True(Sample.InPalette(c.BackColor), name + "." + c.Name + " BackColor " + c.BackColor);
                    Assert.True(Sample.InPalette(c.ForeColor), name + "." + c.Name + " ForeColor " + c.ForeColor);
                    var bb = c as ButtonBase;
                    if (bb != null)
                    {
                        Assert.Equal(FlatStyle.Flat, bb.FlatStyle);
                        Assert.True(Sample.InPalette(bb.FlatAppearance.BorderColor), name + "." + c.Name + " BorderColor");
                        Assert.True(Sample.InPalette(bb.FlatAppearance.MouseOverBackColor), name + "." + c.Name + " MouseOver");
                    }
                    if (c.GetType().FullName == "ProgressODoom.ProgressBarEx")
                        foreach (var painter in new[] { "BackgroundPainter", "ProgressPainter", "BorderPainter" })
                        {
                            var p = c.GetType().GetProperty(painter).GetValue(c, null);
                            Assert.NotNull(p);
                            var color = p.GetType().GetProperty("Color");
                            Assert.True(color != null, name + "." + c.Name + "." + painter + " : peintre sans couleur unie (" + p.GetType().Name + ")");
                            Assert.True(Sample.InPalette((Color)color.GetValue(p, null)), name + "." + c.Name + "." + painter + " " + color.GetValue(p, null));
                        }
                }
            }
        }

        // Rendu reel (galerie) : >= 98 % des pixels de la zone cliente dans la palette, aucune tuile dense hors palette.
        // Fenetres petites et riches en texte (WaitingFelForm, SDFormat) seulement : les franges ClearType y depassent 2 % ; la mesure les
        // neutralise (moyenne horizontale sur 3 pixels), un texte ou un fond de couleur etrangere reste detecte. Les autres : mesure brute.
        private static readonly HashSet<string> ClearTypeDense = new HashSet<string> { "WaitingFelForm", "Tasks.SDFormat" };

        [StaTheory, MemberData(nameof(All))]
        public void OnlyPaletteColorsAfterSkin(string name)
        {
            int bad, total, worst;
            SimpleFormsTests.MeasurePalette(Short(name), out bad, out total, out worst, clearTypeSafe: ClearTypeDense.Contains(name));
            if (total == 0) return;
            double ratio = 1.0 - (double)bad / total;
            Assert.True(ratio >= 0.98, name + " : " + (ratio * 100).ToString("0.00") + " % des pixels dans la palette (" + bad + " hors palette sur " + total + ")");
            Assert.True(worst <= 60, name + " : tuile 32x16 avec " + worst + " pixels hors palette");
        }

        // Les fenetres de taches sont mises a jour sans cesse depuis des threads de travail (Invoke) : l'habillage n'a lieu qu'a la creation
        // du handle, jamais a chaque mise a jour, et rien ne bloque.
        [StaFact]
        public void TaskerUpdatesFromWorkerThreadDoNotReskin()
        {
            using (var f = CreateDesigned("Tasks.TaskerForm"))
            {
                var view = (TaskerForm)f;
                var label = Tree(f).Single(c => c.Name == "statusLabel");
                var bar = Tree(f).Single(c => c.Name == "progressBarEx1");
                int skins = B1Skin.ApplyCountFor(f);
                Assert.True(skins >= 1, "fenetre non habillee a la creation du handle");
                const int n = 400;
                int done = 0;
                Exception error = null;
                var worker = new Thread(() =>
                {
                    try
                    {
                        for (int i = 0; i <= n; i++)
                        {
                            view.SetProgress(i, n);
                            view.SetStatus("Etape " + i);
                            Interlocked.Increment(ref done);
                        }
                    }
                    catch (Exception ex) { error = ex; }
                }) { IsBackground = true };
                var sw = System.Diagnostics.Stopwatch.StartNew();
                worker.Start();
                Assert.True(GridFixture.PumpUntil(() => !worker.IsAlive, 20000), "mises a jour bloquees : " + done + " / " + (n + 1));
                Assert.Null(error);
                Assert.Equal(n + 1, done);
                Assert.Equal("Etape " + n, label.Text);
                Assert.Equal(skins, B1Skin.ApplyCountFor(f));     // aucun habillage pendant les 802 mises a jour
                Assert.True(sw.ElapsedMilliseconds < 15000, "trop lent : " + sw.ElapsedMilliseconds + " ms");
            }
        }
        // --- aucun chemin de test ni de galerie n'atteint le constructeur reel des fenetres qui lancent pnputil / l'installeur du pilote ---

        private static readonly MethodInfo DriverInstalledM = typeof(WaitingFelForm).GetMethod("DriverInstalled", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo InstallDriverM = typeof(WaitingFelForm).GetMethod("InstallDriver", BindingFlags.Public | BindingFlags.Static);

        /// <summary>Methodes appelees (call, callvirt, newobj) dans le corps IL de m. Lecture d'octets : un jeton mal aligne est ignore.</summary>
        private static IEnumerable<MethodBase> Callees(MethodBase m)
        {
            byte[] il;
            try { var body = m.GetMethodBody(); il = body == null ? null : body.GetILAsByteArray(); } catch { il = null; }
            if (il == null) yield break;
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x28 && il[i] != 0x6F && il[i] != 0x73) continue;
                int token = BitConverter.ToInt32(il, i + 1);
                MethodBase target = null;
                try { target = m.Module.ResolveMethod(token, m.DeclaringType != null && m.DeclaringType.IsGenericType ? m.DeclaringType.GetGenericArguments() : null, m.IsGenericMethod ? m.GetGenericArguments() : null); } catch { }
                if (target != null) yield return target;
            }
        }

        private static IEnumerable<MethodBase> AllMethods(Assembly a)
        {
            Type[] types;
            try { types = a.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(x => x != null).ToArray(); }
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var t in types)
            {
                foreach (var c in t.GetConstructors(all)) yield return c;
                foreach (var m in t.GetMethods(all)) yield return m;
            }
        }

        /// <summary>Fenetres de hakchi dont un constructeur appelle DriverInstalled() ou InstallDriver() (pnputil, installeur avec elevation).</summary>
        private static List<Type> DriverTouchingForms()
        {
            return AllMethods(Asm).OfType<ConstructorInfo>()
                .Where(c => typeof(Form).IsAssignableFrom(c.DeclaringType) && Callees(c).Any(x => x == DriverInstalledM || x == InstallDriverM))
                .Select(c => c.DeclaringType).Distinct().ToList();
        }

        [Fact]
        public void DriverTouchingFormsAreKnown()
        {
            Assert.NotNull(DriverInstalledM); Assert.NotNull(InstallDriverM);
            var names = DriverTouchingForms().Select(t => t.Name).OrderBy(x => x).ToList();
            // Si une autre fenetre se met a toucher au pilote, elle doit etre ajoutee a Host.DesignerOnlyTypes (galerie) et a cette liste.
            Assert.Equal(new[] { "WaitingFelForm", "WaitingShellForm" }, names);
        }

        // Galerie : chacune de ces fenetres est construite sans son constructeur applicatif (aucun appel a pnputil ni a l'installeur).
        [Fact]
        public void GalleryNeverRunsDriverTouchingConstructors()
        {
            var entries = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Path.Combine(SimpleFormsTests.GalleryDir.Value, "gallery.json")));
            foreach (var t in DriverTouchingForms())
            {
                var e = entries.Single(x => (string)x["Type"] == t.FullName);
                Assert.True(((string)e["Constructor"] ?? "").StartsWith("concepteur seul"), t.Name + " construite par " + (string)e["Constructor"]);
            }
        }

        // Tests : aucune methode du projet de tests ne cree ces fenetres (newobj) ni n'appelle InstallDriver ; seul DriverInstalled
        // (lecture « pnputil -e ») est appele par WaitingFelFormTests.
        [Fact]
        public void TestsNeverRunDriverTouchingConstructors()
        {
            var forbidden = DriverTouchingForms();
            foreach (var m in AllMethods(typeof(TaskFormsTests).Assembly))
                foreach (var c in Callees(m))
                {
                    Assert.False(c == InstallDriverM, m.DeclaringType.Name + "." + m.Name + " appelle InstallDriver");
                    Assert.False(c is ConstructorInfo && forbidden.Contains(c.DeclaringType), m.DeclaringType.Name + "." + m.Name + " construit " + c.DeclaringType.Name);
                }
        }
    }
}
