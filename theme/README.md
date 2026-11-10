# theme/ : thème TV « B1 » (SNES USA) pour clover-ui

Scripts Python qui génèrent des ressources (images, polices, sons) remplaçant celles
de `/usr/share/ui/snes-eur/resources/` sur la SNES Mini EUR. Jamais de Lua, de
disposition ni d'animation. Aucun fichier Nintendo dans le dépôt : la référence
locale est `P:\HakchiSNESUSA\ui_ref\` (lecture seule) et les sorties lourdes vont
dans `P:\HakchiSNESUSA\theme_out\`.

## Environnement Python

Le Python système n'a pas pytest ; utiliser le venv dédié :

    python -m venv P:\HakchiSNESUSA\venv_theme
    P:\HakchiSNESUSA\venv_theme\Scripts\python -m pip install -r theme\requirements.txt

## Tests (depuis la racine du dépôt)

    P:\HakchiSNESUSA\venv_theme\Scripts\python -m pytest theme/tests -q

## Contenu

- `b1/palette.py` : constantes de la palette B1 (tuples RGB) et `ALL`.
- `b1/art.py` : dessins B1 des sprites (`draw(nom, taille, ref=None)`), classés
  en `DRAWN` (dessinés ici), `RECOLOR` (forme d'origine, couleurs de la palette),
  `CLEAR` (neutralisés) et `KEEP` (laissés).
- `build_theme.py` : atlas repeint, images `layout/asset`, `preview_home.png` et
  `sprites_sheet.png` :

      P:\HakchiSNESUSA\venv_theme\Scripts\python -m theme.build_theme --ref P:\HakchiSNESUSA\ui_ref --out P:\HakchiSNESUSA\theme_out
- `docs/superpowers/notes/theme-ressources.md` : carte des ressources clover-ui.
