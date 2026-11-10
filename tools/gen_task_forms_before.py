"""Genere hakchi_gui.Tests/Data/task_forms_before.json (Task 7) depuis git :
controles (nom, type court, parent) et DialogResult / Accept / Cancel lus dans les .Designer.cs de la base,
et empreinte SHA-256 des fichiers de logique (.cs) de la base (BOM retire, fins de ligne en LF)."""
import hashlib, json, re, subprocess, sys

REPO = r"J:\AI\Claude_code_local\Hakchi2-CE"
BASE = "snes-usa/theme-b1"
FORMS = {
    "Tasks.TaskerForm": "hakchi_gui/Tasks/TaskerForm",
    "Tasks.TaskerTransferForm": "hakchi_gui/Tasks/TaskerTransferForm",
    "Tasks.MessageForm": "hakchi_gui/Tasks/MessageForm",
    "Tasks.ErrorForm": "hakchi_gui/Tasks/ErrorForm",
    "Tasks.SDFormat": "hakchi_gui/Tasks/SDFormat",
    "WaitingFelForm": "hakchi_gui/WaitingFelForm",
    "WaitingShellForm": "hakchi_gui/WaitingShellForm",
    "WaitingShellCycleForm": "hakchi_gui/WaitingShellCycleForm",
    "SelectNesButtonsForm": "hakchi_gui/SelectNesButtonsForm",
    "SelectSnesButtonsForm": "hakchi_gui/SelectSnesButtonsForm",
    "SnesPresetEditor": "hakchi_gui/SnesPresetEditor",
}
# Decision du controleur : WaitingFelForm.cs a pour reference le correctif pnputil / Sysnative (330d589a), pas theme-b1.
LOGIC_BASE = {"WaitingFelForm": "330d589a"}


def show(rev, path):
    return subprocess.run(["git", "-C", REPO, "show", rev + ":" + path], capture_output=True, check=True).stdout.decode("utf-8-sig")


def norm(text):
    return text.lstrip("\ufeff").replace("\r\n", "\n")


new_re = re.compile(r"^\s*this\.(\w+)\s*=\s*new\s+([\w.]+)\s*\(", re.M)
add_re = re.compile(r"^\s*this\.(?:(\w+)\.)?Controls\.Add\(this\.(\w+)", re.M)
dr_re = re.compile(r"^\s*this\.(\w+)\.DialogResult\s*=\s*System\.Windows\.Forms\.DialogResult\.(\w+)", re.M)
acc_re = re.compile(r"^\s*this\.AcceptButton\s*=\s*this\.(\w+)", re.M)
can_re = re.compile(r"^\s*this\.CancelButton\s*=\s*this\.(\w+)", re.M)

out = {"_base": BASE, "_logic_base": LOGIC_BASE, "forms": {}}
for name, path in FORMS.items():
    d = show(BASE, path + ".Designer.cs")
    types = {m.group(1): m.group(2).split(".")[-1] for m in new_re.finditer(d)}
    controls = []
    for m in add_re.finditer(d):
        parent = m.group(1) or "$this"
        child = m.group(2)
        controls.append({"name": child, "type": types[child], "parent": parent})
    controls.sort(key=lambda c: (c["parent"], c["name"]))
    buttons = {n: "None" for n, t in types.items() if t == "Button"}
    for m in dr_re.finditer(d):
        buttons[m.group(1)] = m.group(2)
    acc = acc_re.search(d)
    can = can_re.search(d)
    rev = LOGIC_BASE.get(name.split(".")[-1], BASE)
    logic = norm(show(rev, path + ".cs"))
    out["forms"][name] = {
        "controls": controls,
        "dialogResults": dict(sorted(buttons.items())),
        "accept": acc.group(1) if acc else None,
        "cancel": can.group(1) if can else None,
        "logicRev": rev,
        "logicSha256": hashlib.sha256(logic.encode("utf-8")).hexdigest(),
        "logicLines": logic.count("\n"),
    }

dst = REPO + r"\hakchi_gui.Tests\Data\task_forms_before.json"
with open(dst, "w", encoding="utf-8", newline="\n") as f:
    json.dump(out, f, indent=1, ensure_ascii=False)
    f.write("\n")
print("ecrit", dst, sum(len(v["controls"]) for v in out["forms"].values()), "controles")
