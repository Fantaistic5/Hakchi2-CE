// Empreinte du chemin d'installation / desinstallation des mods (Task 9 de l'UI PC B1).
// Source unique de l'algorithme : compilee dans hakchi_gui.Tests (lien) ET chargee par tools/ModInstallPath/gen_mod_install_before.ps1
// (Windows PowerShell 5.1, Add-Type : C# 5 seulement, pas d'interpolation ni de membres a expression).
//
// Pour chaque gestionnaire racine (type, nom de methode) : decodage reel de l'IL (opcode par opcode, table OpCodes), puis
//  - ensemble des methodes appelees (call, callvirt, newobj, ldftn, ldvirtftn) ;
//  - ensemble des chaines litterales (ldstr : commandes shell, drapeaux) ;
//  - ensemble des champs lus / ecrits (ldfld, stfld, ldsfld, ldflda... : prefixe "field ") ;
//  - descente dans les corps generes par le compilateur (lambdas, fermetures, iterateurs) atteints depuis la racine.
// Les noms generes (<>c__DisplayClass12_0, <Methode>b__12_0, <Methode>d__7) sont normalises : leurs numeros dependent de
// l'ordre des methodes dans la classe, pas de ce qu'elles font.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace ModInstallPath
{
    public static class IlCallees
    {
        /// <summary>Gestionnaires du chemin d'installation / desinstallation des mods (type sous com.clusterrr.hakchi_gui, methode).</summary>
        public static readonly string[][] Roots =
        {
            new[] { "MainForm", "installModulesToolStripMenuItem_Click" },
            new[] { "MainForm", "installModules" },
            new[] { "MainForm", "uninstallModulesToolStripMenuItem_Click" },
            new[] { "MainForm", "InstallMods" },
            new[] { "MainForm", "UninstallMods" },
            new[] { "Hmod.SelectForm", "buttonOk_Click" },
            new[] { "ModHub.Controls.ModHubTabControl", "modDownloadButton_Click" },
            new[] { "ModHub.Controls.ModHubTabControl", "modDownloadInstallButton_Click" },
            new[] { "ModHub.Controls.ModHubTabControl", "handleModDownload" },
            new[] { "ModHub.Controls.ModHubTabControl", "GetItemDownloadTask" },
            new[] { "ModHub.ModHubForm", "ModHubForm_FormClosing" },
            // ajout / suppression de mods sur le disque depuis la selection
            new[] { "Hmod.SelectForm", "deleteModFromDiskToolStripMenuItem_Click" },
            new[] { "Hmod.SelectForm", "AddMods" },
            new[] { "Hmod.SelectForm", "SelectModsForm_DragDrop" },
            // gestion des depots (ecrit la config des depots)
            new[] { "ModHub.Repository.RepoManagementForm", "btnAddRepository_Click" },
            new[] { "ModHub.Repository.RepoManagementForm", "deleteButton_Click" },
            new[] { "ModHub.Repository.RepoManagementForm", "editButton_Click" },
            new[] { "ModHub.Repository.RepoManagementForm", "moveUpButton_Click" },
            new[] { "ModHub.Repository.RepoManagementForm", "moveDownButton_Click" },
            new[] { "ModHub.Repository.RepoManagementForm", "RepoManagementForm_FormClosing" },
            new[] { "MainForm", "repoMenuItem_Click" },
        };

        public const string RootNamespace = "com.clusterrr.hakchi_gui.";

        private static Dictionary<short, OpCode> opcodes;

        private static Dictionary<short, OpCode> OpCodeTable()
        {
            if (opcodes != null) return opcodes;
            var d = new Dictionary<short, OpCode>();
            foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var o = f.GetValue(null);
                if (o is OpCode) { var oc = (OpCode)o; d[oc.Value] = oc; }
            }
            opcodes = d;
            return d;
        }

        private static readonly Regex[] Numbered =
        {
            new Regex(@"<>c__DisplayClass\d+(_\d+)?"),
            new Regex(@">b__\d+(_\d+)?"),
            new Regex(@">d__\d+"),
            new Regex(@">g__(\w+)\|\d+(_\d+)?"),
            new Regex(@"<>9__\d+(_\d+)?"),
            new Regex(@"<>8__locals\d+"),
        };
        private static readonly string[] NumberedRepl = { "<>c__DisplayClass", ">b__", ">d__", ">g__$1", "<>9__", "<>8__locals" };

        public static string Normalize(string s)
        {
            for (int i = 0; i < Numbered.Length; i++) s = Numbered[i].Replace(s, NumberedRepl[i]);
            return s;
        }

        /// <summary>Signature stable d'une methode : type declarant + signature, sans nom d'assemblage ni version.</summary>
        public static string Describe(MethodBase m)
        {
            return Normalize((m.DeclaringType == null ? "" : m.DeclaringType.ToString()) + "::" + m.ToString());
        }

        /// <summary>Champ lu ou ecrit : "field " + type declarant + "::" + type + nom (noms generes normalises).</summary>
        public static string DescribeField(FieldInfo f)
        {
            return Normalize("field " + (f.DeclaringType == null ? "" : f.DeclaringType.ToString()) + "::" + f.FieldType.ToString() + " " + f.Name);
        }

        private static bool IsCompilerGenerated(MethodBase m)
        {
            return m.Name.IndexOf('<') >= 0 || (m.DeclaringType != null && m.DeclaringType.FullName != null && m.DeclaringType.FullName.IndexOf('<') >= 0);
        }

        private sealed class Body
        {
            public List<MethodBase> Methods = new List<MethodBase>();
            public List<string> Strings = new List<string>();
            public List<FieldInfo> Fields = new List<FieldInfo>();
        }

        private static Body Decode(MethodBase m)
        {
            var result = new Body();
            byte[] il = null;
            try { var body = m.GetMethodBody(); if (body != null) il = body.GetILAsByteArray(); } catch { il = null; }
            if (il == null) return result;
            var table = OpCodeTable();
            Type[] typeArgs = m.DeclaringType != null && m.DeclaringType.IsGenericType ? m.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = m.IsGenericMethod ? m.GetGenericArguments() : null;
            int i = 0;
            while (i < il.Length)
            {
                short value = il[i];
                int size = 1;
                if (il[i] == 0xFE && i + 1 < il.Length) { value = (short)(0xFE00 | il[i + 1]); size = 2; }
                OpCode op;
                if (!table.TryGetValue(value, out op)) throw new InvalidOperationException(m.Name + " : opcode inconnu 0x" + value.ToString("X") + " a l'offset " + i);
                i += size;
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: i += 1; break;
                    case OperandType.InlineVar: i += 2; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: i += 8; break;
                    case OperandType.InlineSwitch: { int n = BitConverter.ToInt32(il, i); i += 4 + 4 * n; break; }
                    case OperandType.InlineMethod:
                    {
                        int token = BitConverter.ToInt32(il, i);
                        result.Methods.Add(m.Module.ResolveMethod(token, typeArgs, methodArgs));
                        i += 4;
                        break;
                    }
                    case OperandType.InlineString:
                    {
                        int token = BitConverter.ToInt32(il, i);
                        result.Strings.Add(m.Module.ResolveString(token));
                        i += 4;
                        break;
                    }
                    case OperandType.InlineField:
                    {
                        int token = BitConverter.ToInt32(il, i);
                        result.Fields.Add(m.Module.ResolveField(token, typeArgs, methodArgs));
                        i += 4;
                        break;
                    }
                    default: i += 4; break;   // InlineI, InlineBrTarget, InlineSig, InlineTok, InlineType, ShortInlineR
                }
            }
            return result;
        }

        /// <summary>Empreinte d'une racine : methodes appelees et chaines, corps generes atteints compris (tries, sans doublons).</summary>
        public static void Fingerprint(Assembly asm, string typeName, string method, out List<string> callees, out List<string> strings)
        {
            var t = asm.GetType(RootNamespace + typeName, true);
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var roots = t.GetMethods(all).Where(x => x.Name == method).Cast<MethodBase>().ToList();
            if (roots.Count == 0) throw new MissingMethodException(t.FullName, method);
            var c = new SortedSet<string>(StringComparer.Ordinal);
            var s = new SortedSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<MethodBase>();
            var queue = new Queue<MethodBase>(roots);
            foreach (var r in roots) seen.Add(r);
            while (queue.Count > 0)
            {
                var m = queue.Dequeue();
                var b = Decode(m);
                foreach (var str in b.Strings) s.Add(str);
                foreach (var f in b.Fields) c.Add(DescribeField(f));
                foreach (var callee in b.Methods)
                {
                    c.Add(Describe(callee));
                    // corps generes (lambda, fermeture, MoveNext d'un iterateur) : leur contenu fait partie du gestionnaire
                    if (IsCompilerGenerated(callee) && callee.Module == m.Module && seen.Add(callee)) queue.Enqueue(callee);
                    if (callee is ConstructorInfo && callee.DeclaringType != null && callee.DeclaringType.Name.IndexOf('<') >= 0)
                        foreach (var inner in callee.DeclaringType.GetMethods(all).Where(x => x.Name == "MoveNext"))
                            if (seen.Add(inner)) queue.Enqueue(inner);
                }
            }
            callees = c.ToList();
            strings = s.ToList();
        }
    }
}
