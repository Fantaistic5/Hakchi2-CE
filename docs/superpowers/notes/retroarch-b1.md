# RetroArch en B1 : etude et choix

Source : lecture seule de la console le 2026-11-10 (`task-7-console.txt`), RetroArch 1.8.4 (fev. 2020, `/bin/retroarch`).
Config vivante : `/etc/libretro/retroarch.cfg` (rootfs hakchi, overlay de `/`), `config_save_on_exit = "true"`.

## Etat actuel

| cle | valeur |
|---|---|
| `menu_driver` | `ozone` |
| `menu_wallpaper` | `~/wallpapers/retrocat/retrocat_04.png` |
| `menu_wallpaper_opacity` | `1.000000` |
| `menu_dynamic_wallpaper_enable` | `false` |
| `xmb_font` | `~/fonts/_Default.ttf` |
| `xmb_theme` | `0` (icones monochromes) |
| `xmb_menu_color_theme` | `4` |
| `xmb_shadows_enable` | `true` |
| `menu_shader_pipeline` | `0` (pas de ruban anime) |

Assets presents : `/etc/libretro/assets/ozone` et `/etc/libretro/assets/xmb/monochrome` : aucune icone a embarquer.
Overrides par coeur a ne jamais toucher : `config/PCSX-ReARMed` (PS1) et `config/4DO` (3DO). Dossier `backup_claude_20261110/` : intouchable.

## Pilotes compares (1.8.4)

- **Ozone** : seulement des themes predefinis (clair / sombre), couleurs et police imposees, pas de fond personnalise. Impossible d'y mettre l'ardoise B1.
- **RGUI** : accepte un preset de couleurs personnalise, mais c'est une grille de pixels 8 bits sans police vectorielle ; pas de Jost.
- **XMB** : fond (`menu_wallpaper`) et police (`xmb_font`) libres, icones monochromes, ombres desactivables.

## Choix (validation utilisateur donnee) : XMB + fond B1

- `menu_driver = "xmb"`.
- Fond 1280x720 genere par `theme/retroarch/wallpaper.py` : ardoise `#1C1B22`, deux filets `#2C2A34` en haut (comme le menu TV), un petit interrupteur raye violet (`#4A3C8C`, stries `#6B5BB5` / `#5747A0`) en bas a droite. Rien d'autre : ni logo, ni degrade, ni lueur. Aplats exacts, sans anti-crenelage.
- Police : Jost, instance statique wght 500 (`Jost-Medium.ttf`), car le freetype de RetroArch 1.8.4 ne gere pas necessairement les polices variables.
- `xmb_shadows_enable = "false"` (a plat), `menu_dynamic_wallpaper_enable = "false"`, `menu_wallpaper_opacity = "1.000000"`, `menu_shader_pipeline = "0"` (garde le ruban eteint).
- Chemins absolus (`/usr/share/snes_usa_b1/retroarch/...`) : le `~` de RetroArch n'est pas fiable ici.
- Cles non touchees : `xmb_theme` (deja monochrome) et `xmb_menu_color_theme` (le fond opaque masque la couleur de fond).

## Rendu attendu

Ecran ardoise uni, filets discrets sous le titre, interrupteur violet discret en bas a droite ; texte en Jost ; icones monochromes d'origine, listes XMB classiques (colonne horizontale de categories, entrees verticales).

## Ecarts par rapport a B1 (a accepter)

- Couleurs du texte XMB fixes (blanc / gris), non reglables : pas le `#E4E2EA` / `#8E8A9C` exacts, pas de lavande pour la selection.
- Icones XMB monochromes blanches d'origine (non recolorees en palette B1).
- Le ruban XMB est desactive par `menu_shader_pipeline = 0` ; si le pilote GL le reactive, le fond reste prioritaire.
- Animations et disposition XMB inchangees (mouvement horizontal/vertical, halo de survol propre au pilote).
- Police : seul le menu RetroArch passe en Jost ; les notifications a l'ecran en jeu (`video_font_*`) ne sont pas touchees.

## Installation / retrait

Via le hmod `snes_usa_b1` :
- `install` (source par preinit, jamais `exit`) lance `apply.sh` : sauvegarde `retroarch.cfg.b1bak` si absente, puis remplace seulement les cles de `b1.cfg` (ajout en fin si absentes).
- `uninstall` (source avant les suppressions automatiques de hakchi) lance `restore.sh` : remet les valeurs d'origine de ces cles seulement (les autres reglages, y compris ceux reecrits par RetroArch a la sortie, sont conserves), puis supprime la sauvegarde.
- Aucune ecriture n'est faite sur la console sans accord explicite de l'utilisateur et sauvegarde prealable.
