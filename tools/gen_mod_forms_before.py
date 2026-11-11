"""Genere hakchi_gui.Tests/Data/mod_forms_before.json (Task 9) depuis git :
champs declares dans les .Designer.cs de la base (noms des controles) et boutons Accept / Cancel,
pour les fenetres ModHub / mods et leurs controles utilisateur."""
import json, re, subprocess

REPO = r"J:\AI\Claude_code_local\Hakchi2-CE"
BASE = "snes-usa/theme-b1"
# nom de type sous com.clusterrr.hakchi_gui -> fichier Designer de la base
FORMS = {
    "ModHub.ModHubForm": "hakchi_gui/ModHub/ModHubForm.Designer.cs",
    "ModHub.Repository.RepoManagementForm": "hakchi_gui/ModHub/Repository/RepoManagementForm.Designer.cs",
    "Hmod.SelectForm": "hakchi_gui/Hmod/SelectForm.Designer.cs",
    "ModHub.Controls.ModHubTabControl": "hakchi_gui/ModHub/Controls/ModHubTabControl.Designer.cs",
    "ModHub.Controls.ModInfoControl": "hakchi_gui/ModHub/Controls/ModInfoControl.Designer.cs",
    "Hmod.Controls.ReadmeControl": "hakchi_gui/Hmod/Controls/ReadmeControl.Designer.cs",
    "Hmod.Controls.BrowserReadmeControl": "hakchi_gui/Hmod/Controls/BrowserReadmeControl.Designer.cs",
    "Hmod.Controls.TextReadmeControl": "hakchi_gui/Hmod/Controls/TextReadmeControl.Designer.cs",
}

field_re = re.compile(r"^\s*(?:private|internal|public|protected)\s+(?:readonly\s+)?[\w.<>,\s]+?\s+(\w+)\s*;", re.M)
acc_re = re.compile(r"^\s*this\.AcceptButton\s*=\s*this\.(\w+)", re.M)
can_re = re.compile(r"^\s*this\.CancelButton\s*=\s*this\.(\w+)", re.M)


def show(rev, path):
    return subprocess.run(["git", "-C", REPO, "show", rev + ":" + path], capture_output=True, check=True).stdout.decode("utf-8-sig")


out = {"_base": BASE}
for name, path in FORMS.items():
    d = show(BASE, path)
    names = sorted({m.group(1) for m in field_re.finditer(d)} - {"components"})
    acc = acc_re.search(d)
    can = can_re.search(d)
    out[name] = {"names": names, "accept": acc.group(1) if acc else None, "cancel": can.group(1) if can else None}

dst = REPO + r"\hakchi_gui.Tests\Data\mod_forms_before.json"
with open(dst, "w", encoding="utf-8", newline="\n") as f:
    json.dump(out, f, indent=1, ensure_ascii=False)
    f.write("\n")
print("ecrit", dst, sum(len(v["names"]) for k, v in out.items() if not k.startswith("_")), "champs")
