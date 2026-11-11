"""Instantane des cles des resx localises des fenetres redessinees (Tasks 5, 6, 8, 9), au commit de base de la Task 10.

Sortie : hakchi_gui.Tests/Data/resx_keys_before.json
  { "_base": <sha>, "files": { "<chemin relatif a hakchi_gui>": { "<cle>": "<sha1 de la valeur, 12 hex>" } } }
Les cles de mise en page (I18n/ResxTests.LayoutKey) sont exclues : la Task 10 les retire.
Les valeurs binaires (images) sont notees "bin" : seule la presence de la cle compte.
ResxTests.AllTextKeysPreserved exige que chaque fichier garde exactement ces cles et ces valeurs.

Usage : python tools/gen_resx_keys_before.py [<sha>]   (lu par `git show`, sans toucher la copie de travail)
"""
import hashlib, json, os, re, subprocess, sys
import xml.etree.ElementTree as ET

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = sys.argv[1] if len(sys.argv) > 1 else "f208ba77"
# doit rester identique a hakchi_gui.Tests/I18n/ResxTests.cs (Redesigned + ResourceFiles)
REDESIGNED = [
    "MainForm",
    "AboutBox", "Motd", "TextInfo", "StringInputForm", "SelectFileForm", "SearchForm", "SettingsForm", "TechInfo",
    "SelectIconForm", "SelectSystemDialog", "SelectFirmwareDialog", "ExportGamesDialog", "NewCustomGameForm", "SpineForm",
    "ImagesForm", "ImageGooglerForm", "DumperForm",
    "FoldersManagerForm", "ScraperForm", "SelectCoreDialog", "SaveStateManager", "SelectCoverDialog", "ScreenshotForm",
    "GameImporterForm", "GameGenieCodeForm", "GameGenieCodeAddModForm", "CoverBatchForm",
    "ModHub/ModHubForm", "ModHub/Repository/RepoManagementForm", "Hmod/SelectForm", "ModHub/Controls/ModHubTabControl",
    "ModHub/Controls/ModInfoControl", "Hmod/Controls/ReadmeControl", "Hmod/Controls/BrowserReadmeControl",
    "Hmod/Controls/TextReadmeControl",
    "Properties/Resources", "ModHub/ModHubResources",
]
LAYOUT = re.compile(r"^(>>.*|.+\.(Location|Size|ClientSize|Anchor|Dock|Margin|Padding|TabIndex|AutoSize|MinimumSize|MaximumSize|AutoScaleDimensions|LayoutSettings))$")
CULTURE = re.compile(r"^[a-z]{2}(-[A-Z][a-z]{3})?(-[A-Z]{2})?$")


def git(*args):
    return subprocess.run(["git", "-C", REPO] + list(args), capture_output=True, check=True).stdout


def main():
    sha = git("rev-parse", BASE).decode().strip()
    tree = git("ls-tree", "-r", "--name-only", sha, "hakchi_gui").decode().splitlines()
    files = {}
    for form in REDESIGNED:
        prefix = "hakchi_gui/" + form + "."
        for path in tree:
            if not path.startswith(prefix) or not path.endswith(".resx"):
                continue
            culture = path[len(prefix):-len(".resx")]
            if not CULTURE.match(culture):
                continue
            root = ET.fromstring(git("show", sha + ":" + path))
            keys = {}
            for d in root.findall("data"):
                name = d.get("name")
                if LAYOUT.match(name):
                    continue
                if d.get("mimetype") is not None or (d.get("type") or "").startswith("System.Drawing.Bitmap") or (d.get("type") or "").startswith("System.Drawing.Icon"):
                    keys[name] = "bin"
                else:
                    keys[name] = hashlib.sha1((d.findtext("value") or "").encode("utf-8")).hexdigest()[:12]
            files[path[len("hakchi_gui/"):]] = dict(sorted(keys.items()))
    out = os.path.join(REPO, "hakchi_gui.Tests", "Data", "resx_keys_before.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"_base": sha, "files": dict(sorted(files.items()))}, f, indent=1, ensure_ascii=False)
        f.write("\n")
    print(out, len(files), "fichiers", sum(len(v) for v in files.values()), "cles")


if __name__ == "__main__":
    main()
