using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FormGallery
{
    /// <summary>
    /// References machine-lisibles de l'interface AVANT refonte, prises par reflexion :
    ///   menu_items_before.json  : chaque ToolStripMenuItem de MainForm.
    ///   controls_before.json    : pour chaque Form / UserControl constructible, ses controles nommes.
    /// </summary>
    internal static class Baselines
    {
        private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static void Dump(string dir)
        {
            Directory.CreateDirectory(dir);
            DumpMenus(Path.Combine(dir, "menu_items_before.json"));
            DumpControls(Path.Combine(dir, "controls_before.json"));
        }

        // ---------------------------------------------------------------- menus

        private sealed class MenuCapture
        {
            public string Field, Name, Text, Owner, Shortcut;
            public List<string> Click = new List<string>();
        }

        private static void SetCulture(string name)
        {
            var c = string.IsNullOrEmpty(name) ? CultureInfo.InvariantCulture : new CultureInfo(name);
            Thread.CurrentThread.CurrentUICulture = c;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = c;
        }

        private static List<MenuCapture> CaptureMenus(string culture)
        {
            SetCulture(culture);
            var mainType = Host.Asm.GetType(Host.RootNamespace + ".MainForm", true);
            string ctor, reason;
            var form = Host.Construct(mainType, out ctor, out reason);
            if (form == null) throw new InvalidOperationException("MainForm non constructible : " + reason);
            try
            {
                var names = NameMap(form);
                var clickKey = typeof(ToolStripItem).GetField("EventClick", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                var eventsProp = typeof(Component).GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance);

                var list = new List<MenuCapture>();
                foreach (var f in mainType.GetFields(AllInstance).Where(f => f.FieldType == typeof(ToolStripMenuItem)))
                {
                    var item = (ToolStripMenuItem)f.GetValue(form);
                    if (item == null) continue;
                    var cap = new MenuCapture
                    {
                        Field = f.Name,
                        Name = item.Name,
                        Text = item.Text ?? "",
                        Owner = OwnerPath(item, names),
                        Shortcut = item.ShortcutKeys == Keys.None ? "" : item.ShortcutKeys.ToString(),
                    };
                    var handlers = ((EventHandlerList)eventsProp.GetValue(item, null))[clickKey] as Delegate;
                    if (handlers != null)
                        foreach (var d in handlers.GetInvocationList())
                            cap.Click.Add(d.Method.DeclaringType.Name + "." + d.Method.Name);
                    list.Add(cap);
                }
                return list;
            }
            finally { form.Dispose(); }
        }

        private static void DumpMenus(string path)
        {
            var fr = CaptureMenus("fr-FR");
            var inv = CaptureMenus("");
            var invBy = inv.ToDictionary(x => x.Field);

            var arr = new JArray();
            foreach (var m in fr.OrderBy(x => x.Field, StringComparer.Ordinal))
            {
                MenuCapture i;
                invBy.TryGetValue(m.Field, out i);
                arr.Add(new JObject
                {
                    ["field"] = m.Field,
                    ["name"] = m.Name,
                    ["textFr"] = m.Text,
                    ["textInvariant"] = i != null ? i.Text : "",
                    ["owner"] = m.Owner,
                    ["clickHandlers"] = new JArray(m.Click),
                    ["shortcut"] = m.Shortcut,
                });
            }
            File.WriteAllText(path, arr.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
            Console.WriteLine("menu_items_before.json : " + arr.Count + " ToolStripMenuItem de MainForm -> " + path);
        }

        private static Dictionary<object, string> NameMap(Control form)
        {
            var map = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
            for (var t = form.GetType(); t != null && t != typeof(Form); t = t.BaseType)
                foreach (var f in t.GetFields(AllInstance))
                {
                    if (!typeof(Component).IsAssignableFrom(f.FieldType)) continue;
                    var v = f.GetValue(form);
                    if (v != null && !map.ContainsKey(v)) map[v] = f.Name;
                }
            return map;
        }

        private static string NameOf(object o, Dictionary<object, string> names)
        {
            if (o == null) return "(aucun)";
            string n;
            if (names.TryGetValue(o, out n)) return n;
            var item = o as ToolStripItem;
            if (item != null) return string.IsNullOrEmpty(item.Name) ? item.Text : item.Name;
            var c = o as Control;
            return c != null && !string.IsNullOrEmpty(c.Name) ? c.Name : o.GetType().Name;
        }

        /// <summary>Chemin des proprietaires, de la barre racine jusqu'au parent direct, separe par " > ".</summary>
        private static string OwnerPath(ToolStripItem item, Dictionary<object, string> names)
        {
            var chain = new List<string>();
            var cur = item;
            int guard = 0;
            while (guard++ < 32)
            {
                var oi = cur.OwnerItem;
                if (oi == null)
                {
                    var strip = cur.Owner;
                    // un menu deroulant detache est identifie par son item parent ; sinon la barre / le menu contextuel
                    chain.Insert(0, NameOf(strip, names));
                    break;
                }
                chain.Insert(0, NameOf(oi, names));
                cur = oi;
            }
            return string.Join(" > ", chain);
        }

        // ------------------------------------------------------------- controles

        private static void DumpControls(string path)
        {
            SetCulture("");
            var root = new JObject();
            int built = 0, skipped = 0;
            foreach (var t in Host.UiTypes())
            {
                string ctor, reason;
                Watchdog.TakeDismissed();
                Control c = null;
                try { c = Host.Construct(t, out ctor, out reason); }
                catch (Exception ex) { ctor = null; reason = ex.GetType().Name + " : " + ex.Message; }
                var obj = new JObject { ["type"] = t.FullName, ["kind"] = Host.KindOf(t) };
                if (c == null)
                {
                    obj["constructed"] = false;
                    obj["reason"] = reason;
                    skipped++;
                }
                else
                {
                    try
                    {
                        obj["constructed"] = true;
                        var names = NameMap(c);
                        var controls = new JArray();
                        string rootName = string.IsNullOrEmpty(c.Name) ? t.Name : c.Name;
                        Walk(c, rootName, controls);
                        obj["controls"] = controls;
                        obj["toolStripItems"] = ToolStripItems(c, names);
                        obj["components"] = Components(c);
                        built++;
                    }
                    finally { c.Dispose(); }
                }
                var dismissed = Watchdog.TakeDismissed();
                if (dismissed.Count > 0) obj["dismissedDialogs"] = new JArray(dismissed);
                root[t.Name] = obj;
            }
            File.WriteAllText(path, root.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
            Console.WriteLine("controls_before.json : " + built + " fenetres construites, " + skipped + " non constructibles -> " + path);
        }

        private static void Walk(Control parent, string parentName, JArray into)
        {
            foreach (Control child in parent.Controls)
            {
                string name = child.Name;
                if (!string.IsNullOrEmpty(name))
                    into.Add(new JObject { ["name"] = name, ["type"] = child.GetType().FullName, ["parent"] = parentName });
                Walk(child, string.IsNullOrEmpty(name) ? parentName : name, into);
            }
        }

        private static JArray ToolStripItems(Control form, Dictionary<object, string> names)
        {
            var arr = new JArray();
            foreach (var f in AllFields(form).Where(f => typeof(ToolStripItem).IsAssignableFrom(f.FieldType)).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                var item = f.GetValue(form) as ToolStripItem;
                if (item == null) continue;
                var owner = OwnerPath(item, names);
                var parent = owner.Contains(" > ") ? owner.Substring(owner.LastIndexOf(" > ", StringComparison.Ordinal) + 3) : owner;
                arr.Add(new JObject { ["name"] = f.Name, ["type"] = item.GetType().FullName, ["parent"] = parent });
            }
            return arr;
        }

        /// <summary>Composants declares par le designer qui ne sont ni des controles enfants ni des items (menus contextuels, timers, listes d'images...).</summary>
        private static JArray Components(Control form)
        {
            var arr = new JArray();
            var inTree = new HashSet<object>(ReferenceEqualityComparer.Instance);
            Collect(form, inTree);
            foreach (var f in AllFields(form).Where(f => typeof(Component).IsAssignableFrom(f.FieldType)
                                                         && !typeof(ToolStripItem).IsAssignableFrom(f.FieldType)).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                var v = f.GetValue(form);
                if (v == null || inTree.Contains(v)) continue;
                arr.Add(new JObject { ["name"] = f.Name, ["type"] = v.GetType().FullName, ["parent"] = "" });
            }
            return arr;
        }

        private static void Collect(Control c, HashSet<object> set)
        {
            set.Add(c);
            foreach (Control ch in c.Controls) Collect(ch, set);
        }

        private static IEnumerable<FieldInfo> AllFields(Control form)
        {
            for (var t = form.GetType(); t != null && t != typeof(Form) && t != typeof(UserControl) && t != typeof(Control); t = t.BaseType)
                foreach (var f in t.GetFields(AllInstance)) yield return f;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }
    }
}
