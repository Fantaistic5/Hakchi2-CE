"""Genere hakchi_gui.Tests/Data/library_forms_before.json (Task 8) depuis git :
champs declares dans les .Designer.cs de la base (noms des controles) et boutons Accept / Cancel."""
import json, re, subprocess

REPO = r"J:\AI\Claude_code_local\Hakchi2-CE"
BASE = "snes-usa/theme-b1"
FORMS = [
    "FoldersManagerForm", "ScraperForm", "SelectCoreDialog", "SaveStateManager", "SelectCoverDialog",
    "ScreenshotForm", "GameImporterForm", "GameGenieCodeForm", "GameGenieCodeAddModForm", "CoverBatchForm",
]

field_re = re.compile(r"^\s*(?:private|internal|public|protected)\s+(?:readonly\s+)?[\w.<>,\s]+?\s+(\w+)\s*;", re.M)
acc_re = re.compile(r"^\s*this\.AcceptButton\s*=\s*this\.(\w+)", re.M)
can_re = re.compile(r"^\s*this\.CancelButton\s*=\s*this\.(\w+)", re.M)


def show(rev, path):
    return subprocess.run(["git", "-C", REPO, "show", rev + ":" + path], capture_output=True, check=True).stdout.decode("utf-8-sig")


out = {}
for name in FORMS:
    designer = ".designer.cs" if name.startswith("GameGenie") else ".Designer.cs"   # casse d'origine du depot
    d = show(BASE, "hakchi_gui/" + name + designer)
    names = sorted({m.group(1) for m in field_re.finditer(d)} - {"components"})
    acc = acc_re.search(d)
    can = can_re.search(d)
    out[name] = {"names": names, "accept": acc.group(1) if acc else None, "cancel": can.group(1) if can else None}

dst = REPO + r"\hakchi_gui.Tests\Data\library_forms_before.json"
with open(dst, "w", encoding="utf-8", newline="\n") as f:
    json.dump(out, f, indent=1, ensure_ascii=False)
    f.write("\n")
print("ecrit", dst, sum(len(v["names"]) for v in out.values()), "champs")
