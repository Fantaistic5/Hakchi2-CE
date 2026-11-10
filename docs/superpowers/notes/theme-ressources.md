# Carte des ressources clover-ui (thème B1)

Source analysée : `P:\HakchiSNESUSA\ui_ref` (copie lecture seule de `/usr/share/ui/snes-eur`, build `20170418_1014-5e00951`, moteur « CLOVER2 Home Menu runtime data export »). Aucun fichier d'origine n'est copié dans le dépôt. Les vignettes de travail sont dans `P:\HakchiSNESUSA\theme_out\atlas_crops\`.

Légende des traitements : **repeindre** = nouveau dessin B1 (même taille, même nom, même position dans l'atlas) ; **recolorer** = même forme, couleurs ramenées à la palette ; **laisser** = intact ; **neutraliser** = rendre transparent ou plat pour que l'animation existante tourne à vide.

Palette (rappel) : ardoise `#1C1B22`, filet `#2C2A34`, violet `#4A3C8C` (stries `#6B5BB5` / `#5747A0`), lavande `#A79FD6`, texte `#E4E2EA`, texte secondaire `#8E8A9C`.

## 0. Constats structurants

1. **Seul le contenu de fichiers image / police / son est remplaçable.** `scripts/**/*.lua` et `animation/*.lua` sont du bytecode LuaJIT (`1B 4C 4A 01`), non modifiable. Les `.scn`, `.sceneanimation`, `.spriteanim`, `prefab/*.scn` décrivent disposition et animations : laissés tels quels.
2. **Les couleurs de texte ne sont pas dans les images mais dans le bytecode.** `scripts/env/colors_eur.lua` contient 22 clés (`font_home_gametitle`, `font_home_caption`, `font_option_*`, `font_dialog_*`, `font_resume_*`, `bg_base`, `bg_mario`, `bg_luigi`) sous forme de constantes numériques. Décodage grossier des constantes : `font_home_hud`, `font_option_item`, `font_dialog_text` = 1 (blanc) ; `font_home_gametitle`, `font_home_caption`, `font_dialog_button` = 0 (noir) ; `font_resume_title` ≈ 0,278 (gris foncé) ; `font_option_item_disable` ≈ 0,459 ; `bg_base` ≈ (0,92 ; 0,89 ; 0,84), `bg_mario` ≈ rouge, `bg_luigi` ≈ vert. **À confirmer en rendu** (décodage manuel du bytecode, pas exécuté). Conséquence : une police est multipliée par une couleur fixe, noire ou blanche. Les glyphes des `.font` sont blancs avec alpha ; pour obtenir `#E4E2EA` au lieu de blanc pur, on peint les glyphes en `#E4E2EA` dans l'atlas (multiplié par 1,0, inchangé). Pour les textes à 0 (noir), le texte reste noir : la plaque dessous (sprite) doit alors être claire (lavande / `#E4E2EA`), sinon il devient illisible.
3. **Fond du menu d'accueil : pas de couleur dans les `.scn`, c'est une image.** `prefab/bg.scn` : une caméra `CameraComponent` avec `clearColor [0,0,0,1]` et `clearScreen true` rend vers `bg.rendertarget` (JSON `{"width":320,"height":180}`), puis un nœud `render` l'affiche en 1280×720 (×4). Dans la caméra, `default_bg/RootPane/wall` affiche `layout/asset/sprite/wallpaper/wall.png` (512×184, affiché 1536×552, **défilement horizontal** par `TextureOffsetX` sur 8,53 s dans `lyt_wallpaper_default.sceneanimation`). C'est ce PNG (console et manettes néon sur noir) qui fait le fond ; le noir est son contenu, pas un champ de couleur. La variante `demo_bg` (monde Mario : `wall_far`, `wall_near`, `pole04`) sert au mode démo. `NodeBG` (bytecode) tween une couleur `colorTo` depuis `black` vers `colors.bg_*` : probablement une teinte ; sur un fond ardoise très sombre l'effet est négligeable, à vérifier en rendu.
4. **Autres fonds de page :** `defaultscene.scn` contient deux caméras (`/Main/manual/Camera`, `/Main/option_display/Camera`) avec `clearColor [0.945,0.933,0.925,1]` (= `#F1EEEC`, papier clair), `clearScreen false`. Ces champs sont de la disposition (scène JSON, hors périmètre « images / polices / sons ») : **fond clair des pages Manuel et Affichage impossible à passer en ardoise** (voir §8, I1). La caméra de `sys_boot.scn` est noire `[0,0,0,1]`, proche de l'ardoise : laissée. Aucune autre caméra n'est déclarée dans les `.scn` (celle du menu principal est créée par script).
5. **Les polices `.font` sont un conteneur maison** : 9 octets d'en-tête (`01`, longueur BMFont `u32`, longueur PNG `u32`) + BMFont binaire v3 + PNG RGBA d'atlas (vérifié : 9 + 2419 + 20360 = 22788 pour `s.font`). Chaque police est un **sous-ensemble** des glyphes réellement utilisés (voir §4) ; la police Jost générée doit couvrir au minimum les mêmes points de code (latin, accents, cyrillique pour `eur_ru`).
6. **Tous les sons sont référencés par id** dans `dependencies.json`, chemins `/sounds/hvc/*.wav`.
7. **L'atlas contient du contenu Nintendo sous droits** (jaquette Star Fox 2, capture Super Mario World dans les cadres d'aperçu) : à repeindre en plaques neutres, jamais à recopier.

## 1. Atlas `resources/sprites/packed.png` + `packed.json` (512×1024, 166 sprites)

Tous les sprites de l'atlas sont référencés par `textureFrame [x,y,w,h]` dans `packed.json`. Le hmod remplace `packed.png` en entier : repeindre région par région, **sans déplacer aucune région** (le JSON reste intact). Les tailles sont celles du JSON. Les 166 sprites sont tous listés ci-dessous.

### 1a. Grille d'accueil, cartouches, barre de menu

| Sprite(s) | Rôle à l'écran | Traitement B1 |
|---|---|---|
| `game_card` 84×92 | cadre gris d'une jaquette dans la grille (non sélectionnée) | **repeindre** : plaque `RULE`, bord `TEXT_DIM`, coins nets |
| `game_card_active` 84×92 | même cadre, jeu sélectionné (bleu) | **repeindre** : `VIOLET`, bord `LAVENDER` ; le petit interrupteur sous la jaquette : voir I2 |
| `game_card_lock` 264×288 | grande plaque blanche, ruban rouge et logo Star Fox 2 : jeu verrouillé | **repeindre** : plaque ardoise + cadenas lavande, sans logo ni ruban |
| `menubar_L` 432×40, `menubar_U` 432×40, `menubar_U_2` 8×40, `munubar_base` 32×32 | barre de menu en haut : photo gris clair d'une console avec voyant et logo 4 boutons ; `munubar_base` (rouge sombre) est la base étirable | **repeindre** : bandeau plat `SLATE` + deux filets `RULE` (la « double rainure ») ; supprimer logo et boutons colorés |
| `btn_menubar` 32×32 (rouge), `btn_menubar_active` 32×32 (bleu) | fonds de bouton de la barre de menu (inactif / actif) | **recolorer** : `RULE` / `VIOLET` |
| `btn_copyright`, `btn_display`, `btn_language`, `btn_option` (24×24) | icônes de la barre de menu : mentions, affichage, langue, réglages | **recolorer** : pictos monochromes `TEXT` / `LAVENDER` (le globe et l'écran sont aujourd'hui multicolores) |
| `caption` 48×22, `caption_edge` 4×22, `caption_arrow` 8×8, `caption_title` 348×22 | bulle de légende (noire à bord blanc) et barre-titre blanche | **repeindre** : `caption` / `edge` plaque `RULE` bord `TEXT_DIM` ; `caption_title` plaque `TEXT` (texte noir par défaut dessus, cf. constat 2) |
| `cmn_circle` 64×64, `cmn_square` 64×64 | formes blanches de base, teintées par script | **laisser** |
| `registration_frame` 74×24, `popup_warning` 150×36, `popup_present` 140×36 | bulles d'info jaune (inscription, avertissement) et rose (cadeau) | **repeindre** : bulles `LAVENDER` pleines (texte noir dessus), sans dégradé rose |
| `icon_exclamation` 24×24 | triangle d'avertissement jaune | **recolorer** : triangle `LAVENDER`, `!` `SLATE` |

### 1b. Curseur de sélection, flèches, barre de défilement

| Sprite(s) | Rôle | Traitement B1 |
|---|---|---|
| `cursor_1`, `cursor_3`, `cursor_7`, `cursor_9` (32×32) | quatre coins du curseur de la grille (cyan) | **recolorer** : `LAVENDER`, aplat |
| `cursor_game_1` à `cursor_game_9` (32×32) | segments du curseur du lanceur de jeu (cyan, coins arrondis) | **recolorer** : `LAVENDER`, aplat |
| `cursor_option` 8×8 | curseur de page Options (bleu) | **recolorer** : `VIOLET` |
| `curor_thumbnail_1..3` 12×8 (sic) | marqueurs animés sous la vignette (bleu clair) | **recolorer** : `LAVENDER` |
| `dot_cursor_h_1..3` 8×8, `dot_cursor_v_1..3` 16×8 | petits triangles / barres animés du curseur | **laisser** (blancs, teintés par script) |
| `arrow_left`, `arrow_right` 16×22 ; `arrow_Small_left`, `arrow_Small_right` 14×20 ; `allow_lateral_off/on` 10×16 | flèches vertes de changement de page et chevrons de liste | **recolorer** : `LAVENDER` (plein) / `TEXT_DIM` (inactif), bord `SLATE` |
| `scrollbar_Area` 2×112, `scrollbar_full` 10×24, `scrollbar_allowup_on/off`, `scrollbar_allowdown_on/off` 16×16 | barre de défilement : rail, poignée, flèches | **recolorer** : rail `RULE`, poignée `LAVENDER`, flèches `TEXT` / `TEXT_DIM` |

### 1c. Pastilles de manette et indications de boutons (modes « light » et « resume »)

| Sprite(s) | Rôle | Traitement B1 |
|---|---|---|
| `icon_A_light` 12×12 / `icon_A_resume` 10×10 (rouge), `icon_B_light` / `icon_B_resume` (jaune), `icon_X_light` / `icon_X_resume` (bleu), `icon_Y_light` / `icon_Y_resume` (vert) | pastilles des boutons A/B/X/Y | **repeindre** façon manette US : **A, B violet bombé** (`VIOLET` + reflet `STRIPE_LIGHT`) ; **X, Y lavande creusé** (`LAVENDER` + ombre intérieure). Lettre `TEXT` / `SLATE`. Deux couleurs au lieu de quatre |
| `icon_start_light` 28×10, `icon_select_light` 32×10, `icon_start_resume` 26×8, `icon_select_resume` 30×8 | pilules START / SELECT | **repeindre** : pilule `RULE` bord `TEXT_DIM`, texte `TEXT` (resume : `TEXT_DIM`) |
| `icon_direction_down_light`, `icon_direction_up_light`, `icon_direction_lateral_light` (12×12), `Icon_direction_light`, `Icon_direction_Lengthwise_light`, versions `_resume` (10×10), `icon_resume_direction_up/down/down_2` 22×22, `icon_resume_btn_B` 16×16 | croix directionnelle (toutes variantes) et bouton B rond | **recolorer** : croix `TEXT` / `TEXT_DIM`, bouton `RULE` |
| `icon_numer_1..4` 14×14 | pastilles numérotées 1 à 4 (emplacements de sauvegarde) | **recolorer** : `RULE` + chiffre `TEXT` |

### 1d. Icônes de cartes, modes de jeu, sauvegarde

| Sprite(s) | Rôle | Traitement B1 |
|---|---|---|
| `icon_1P`, `icon_1P-2P`, `icon_2P_simultaneous` 22×14 | nombre de joueurs sur une carte | **recolorer** : `TEXT` / `LAVENDER`, sans orange |
| `icon_casette`, `icon_casette_hvc`, `icon_casette_nes` 20×20 | cartouches SNES, Famicom, NES | **recolorer** : cartouche `TEXT` + étiquette `LAVENDER` |
| `icon_disk` 20×20 (orange), `icon_present` 20×20 (cadeau), `icon_lock` / `icon_unlock` 14×18 (jaune) | disquette, cadeau, cadenas fermé / ouvert | **recolorer** : `LAVENDER` / `TEXT`, ruban rouge en `VIOLET` |
| `Icon_Save`, `Icon_Save_Popup` 18×14, `Icon_Save_Warning` 14×14, `Icon_SRAM_on/off` 14×14 | icônes de sauvegarde (écran + crayon, SRAM) | **recolorer** : `TEXT`, accent `LAVENDER` (l'alerte devient lavande, pas rouge) |
| `icon_Resume` 24×24, `icon_Resume_on/off/lock` et `_active` (6×6), `icon_resume_wing` 26×18 | bouton « Reprise » (cube bleu), points d'état (noir, bleu, orange) et ailes | **recolorer** : cube `LAVENDER`, points `TEXT_DIM` / `LAVENDER` / `VIOLET`, ailes `TEXT` |
| `manual` 24×24 | bouton Manuel (bloc « ? » vert) | **recolorer** : `LAVENDER` |
| `qr_code1` 51×51 | QR du manuel (noir et blanc) | **laisser** (fonctionnel) |
| `icon_trash_0..4` 32×48 | poubelle gris clair et ses étapes d'animation | **recolorer** : `TEXT_DIM`, bord `RULE` |

### 1e. Cartes de reprise, ailes, plateaux

| Sprite(s) | Rôle | Traitement B1 |
|---|---|---|
| `resume_card` 64×56, `resume_card_float` 68×56, `resume_card_float_home` 54×50, `resume_card_float_mini` 38×36, `resume_card_empty` 68×60 | cartouches grises à fenêtre sombre (sauvegarde, flottante, vide) | **repeindre** : plaque `RULE`, fenêtre `SLATE`, bord `TEXT_DIM` |
| `resume_card_active` 64×56 (bleu), `resume_card_active_home` 50×46 (bleu clair) | cartes sélectionnées | **repeindre** : `VIOLET` / `STRIPE_LIGHT` |
| `resumebg` 428×118 (gris), `resumebg_overwrite` 428×118 (vert), `resumebg_tail` 32×18, `resumebg_tail_overwrite` 32×18 | bandeau de reprise et bandeau vert de confirmation d'écrasement, avec pointe | **repeindre** : `RULE` / `VIOLET` (écrasement), pointe assortie. Le vert n'a pas d'équivalent à 4 couleurs : `STRIPE_DARK` retenu |
| `resume_wingL_1/2` 24×32, `resume_wingM_1/2` 16×20, `resume_wingS_1/2` 10×16 | ailes animées autour des cartes flottantes (blanches, ombrées) | **recolorer** : `TEXT` plat (sans ombres dégradées) |

### 1f. Dialogues, boutons, cases, interrupteurs, jauge

| Sprite(s) | Rôle | Traitement B1 |
|---|---|---|
| `dialog_corner`, `dialog_corner_slim` 12×12 ; `dialog_line`, `dialog_line_slim` 8×4 | cadre de dialogue étirable, blanc à bord noir | **repeindre** : bord `LAVENDER` 1 px sur plaque `RULE` |
| `btn_dialog_off` 80×16 (blanc), `btn_dialog_on` 88×24 (bleu, bord cyan) | boutons de dialogue (repos / focalisé) | **repeindre** : repos plaque `TEXT` (texte noir), focus `VIOLET` bord `LAVENDER` |
| `btn_init_off` 144×24 (blanc), `btn_init_on` 152×32 (bleu) | gros boutons « initialiser » dans Options | **repeindre** : comme `btn_dialog_*` |
| `radiobtn_off/on` 12×12, `check_on` 20×14 | bouton radio, coche | **recolorer** : `TEXT_DIM` / `LAVENDER` (le rond orange devient lavande) |
| `switch_on` (orange), `switch_off`, `switch_disable` 30×16 | interrupteur d'option | **recolorer** : on `VIOLET` + `STRIPE_LIGHT`, off `RULE`, disable `RULE` bord `TEXT_DIM` |
| `meter_gauge` 142×24 (cyan), `meter_gauge_edge` 1×22 | jauge de réglage | **recolorer** : `LAVENDER` |
| `tab_OSS_off`, `tab_copyright_off` 8×8 | onglets mentions légales (triangles noirs) | **recolorer** : `TEXT_DIM` |

### 1g. Aperçus d'écran, fumée

| Sprite(s) | Rôle | Traitement B1 |
|---|---|---|
| `screen_analog_preview` 220×168, `screen_pixel_perfect_preview` 192×168 | captures de Super Mario World montrant le filtre d'affichage (Options > Affichage) : **image Nintendo** | **repeindre** : fausse scène géométrique neutre (rayures `RULE` / `VIOLET`) montrant la différence analogique / pixel perfect ; ne jamais conserver la capture |
| `smoke_32_1..5` 32×32, `smoke_64_1..5` 64×64 | nuage de fumée (lancement d'un jeu, poubelle) | **recolorer** : gris `TEXT_DIM` sans transparence dégradée, ou **laisser** (peu visible) |

## 2. Atlas annexes `resources/sprites/`

| Fichier | Contenu | Traitement |
|---|---|---|
| `dot_num.png/json` 32×32, 11 sprites (`Sprite1`…`Sprite11`) | chiffres en pixels (compteur) | **recolorer** en `TEXT` |
| `dummy.png/json` 256×256 | cadre vide `Default` 256×224 | **laisser** |
| `frame_preview.png/json` 320×180, `frame_thumbnail.png/json` 172×98 | surfaces cibles des aperçus (texture seule, aucun sprite) | **laisser** (remplies par la console) |

## 3. `resources/layout/asset/sprite/**` (assets des mises en page animées)

| Fichier | Rôle à l'écran | Traitement B1 |
|---|---|---|
| `wallpaper/wall.png` 512×184 | **fond du menu d'accueil** : manettes et console néon sur noir, défile en boucle horizontale (constat 3) | **repeindre** : aplat `SLATE` + deux filets `RULE` en haut, **tuilable horizontalement** (le décalage `TextureOffsetX` fait boucler l'image : un aplat est sans couture, les filets sont horizontaux donc compatibles) |
| `wallpaper/wall_far.png` 512×178 | monde Mario, fond lointain (collines bleues, nuages) : mode démo seulement | **repeindre** : aplat `SLATE`, ou **neutraliser** |
| `wallpaper/wall_near.png` 1024×100 | monde Mario, sol et tuyaux : mode démo | **neutraliser** : transparent ou bande `RULE` |
| `wallpaper/pole01..04.png` 32×64 | poteaux de décor du mode démo (« H » rayés) | **neutraliser** ou **recolorer** `RULE` |
| `mario/*.png` (11 images 24×32 : `dush01-03`, `jump01-02`, `look`, `run01-03`, `spin01-02`) | Mario qui court / saute / regarde en haut, animé par `clover_character.lua` et `lyt_mario*` | **neutraliser** (transparent, mêmes dimensions) : scripts et animations tournent sans rien afficher. Voir I3 |
| `luigi/*.png` (5 images : `jump01-02`, `run01-03`) | idem pour Luigi | **neutraliser** (idem) |
| `effect/hit_effect.png` 16×16 | étincelle blanche d'impact (Mario frappe un bloc) | **neutraliser** |
| `effect/blind.png` 1×1 noir | store de transition (ouverture / fermeture du boot) | **recolorer** en `SLATE` |
| `unlock/halo.png` 150×150, `halo2.png` 200×200, `flash.png` 24×24 | rayons lumineux de l'animation de déverrouillage (dégradé radial) | **neutraliser** (transparent) : « aucun halo, aucune lueur » prime |
| `unlock/present_box.png` 264×288 | grand cadeau : jaquette Star Fox 2, ruban rouge, médaillon | **repeindre** : plaque ardoise, ruban `VIOLET`, médaillon `LAVENDER`, **sans logo** |
| `unlock/present_white.png` 88×96 | silhouette blanche du cadeau (flash) | **recolorer** : `TEXT` plat |

## 4. `resources/fonts/*`

Format maison : 9 octets d'en-tête + BMFont binaire + PNG d'atlas blanc à alpha (constat 5). Les glyphes sont blancs et multipliés par la couleur de label.

| Fichier | Corps | Glyphes | Atlas | Rôle | Traitement B1 |
|---|---|---|---|---|---|
| `s.font` | 18 px | 117 | 256×128 | petit texte : légendes, HUD, détails (52 labels) | **repeindre** en **Jost** 18 px (glyphes `TEXT`), mêmes points de code |
| `m.font` | 24 px | 132 | 256×256 | texte courant : menus, boutons, dialogues (51 labels) | **repeindre** en Jost 24 px, mêmes points de code (latin, accents, cyrillique) |
| `l.font` | 36 px | 99 | 512×256 | grands titres (12 labels) | **repeindre** en Jost 36 px |
| `title.font` | 48 px, « Yu Gothic UI », atlas 1024×1056 | 1278 | – | titre de jaquette : **déjà remplacé par `font_remount` de hakchi** (fichier daté 2022) | **laisser** (ne pas détruire `font_remount` ; la désinstallation le rend) |
| `copyright.fnt` | 18 px | 367 | 512×256 | page « mentions légales » | **laisser**, ou Jost 18 px si les 367 glyphes sont couverts |
| `debug.font` | 18 px | 1 (espace) | 2×2 | police de débogage vide | **laisser** |

Points à tenir : chaque `.font` est un **sous-ensemble** : le générateur reprend exactement les identifiants de caractères d'origine (sinon des labels s'affichent vides) ; `lineHeight` / `base` et `scaleW` / `scaleH` doivent rester compatibles avec la mise en page figée dans les `.scn` (`wrapWidth`, ancrages).

## 5. `resources/sounds/hvc/*.wav` (21 fichiers, PCM 16 bits, 44 100 Hz)

| Fichier | Durée | Canaux | Rôle | Traitement B1 |
|---|---|---|---|---|
| `bgm_home.wav` | 52,36 s | stéréo | musique de fond du menu d'accueil, en boucle | **remplacer** par le morceau original B1 (gothique, mode mineur, ≈ 60 s sans couture) |
| `bgm_boot.wav` | 2,00 s | stéréo | jingle de démarrage | **remplacer** par un jingle original de 2 s, même ambiance ; **laisser** à défaut |
| `se_sys_cursor.wav` | 0,06 s | mono | déplacement du curseur (59 usages, le plus fréquent) | **refaire** : « tick » sec d'orgue |
| `se_sys_click.wav`, `_click_short`, `_click_game`, `_click_replay` | 0,08 / 0,03 / 0,26 / 0,08 s | mono | validations, lancement d'un jeu, reprise | **refaire** : même famille que le curseur |
| `se_sys_cancel.wav` | 0,23 s | mono | retour / annuler | **refaire** |
| `se_sys_lock.wav`, `se_sys_unlock.wav` | 0,27 / 0,18 s | mono | verrou / déverrouillage | **refaire** |
| `se_sys_open_effect.wav`, `se_sys_tremble.wav` | 1,36 / 1,99 s | mono | ouverture du cadeau, tremblement | **refaire** ou **laisser** |
| `se_sys_bounce.wav`, `_bounce_short` | 0,22 / 0,15 s | mono | rebond (carte flottante) | **refaire** (court) |
| `se_sys_up.wav`, `_push`, `_attention`, `_delete`, `_smoke` | 0,11 / 0,13 / 0,09 / 0,60 / 0,60 s | mono | pression, alerte, suppression, fumée | **refaire** ou **laisser** |
| `se_sys_blind_open.wav`, `se_sys_blind_close.wav` | 0,10 s | mono | stores de transition | **laisser** |

Contraintes de format : effets **mono**, musiques **stéréo**, WAV PCM 16 bits 44 100 Hz ; conserver noms et formats. Aucun son tiers : originaux uniquement.

## 6. Scènes JSON (`defaultscene.scn`, `sys_boot.scn`, `prefab/*.scn`)

Lisibles en JSON, **non modifiées** (disposition). Contenu utile pour le thème :

| Scène | Contenu | Couleurs / constats |
|---|---|---|
| `defaultscene.scn` (620 Ko, 1788 nœuds) | menu complet : accueil, reprise, options, affichage, manuel, légendes, popups de déverrouillage | 2 caméras `clearColor #F1EEEC` (Manuel et Affichage, `clearScreen false`) ; 126 labels blancs `[1,1,1,1]`, ombre noire ou `#202020` ; couleurs de composant `#202020`, `#4C4C4C`, `#EFEFEF`, cyan semi-transparent et bleu semi-transparent (surcouches) ; polices `s`, `m`, `l`, `title`, `debug` ; 145 composants son (cursor 59, click 45, cancel 13…) |
| `sys_boot.scn` (46 Ko) | écran de démarrage : stores (`blind`), jingle `bgm_boot` | caméra noire ; 8 labels blancs |
| `prefab/bg.scn` | fond d'accueil (constat 3) | `clearColor` noir, rendu 320×180 puis ×4 |
| `prefab/demo.scn`, `sys_dialog.scn`, `sys_game_card.scn`, `sys_game_thumbnail.scn`, `sys_option_display_frame_item.scn` | mode démo, dialogue, carte de jeu, vignette, cadre d'affichage | teintes blanches ; sprites de l'atlas |
| `bg.rendertarget` | `{"width":320,"height":180}` | **laisser** |

## 7. Autres fichiers

| Fichier | Contenu | Traitement |
|---|---|---|
| `animation/*.lua`, `scripts/**/*.lua` | scripts (bytecode LuaJIT) | **laisser** (intouchable) |
| `animation/*.spriteanim` (curseurs, fumée, ailes, poubelle, resume_direction) | listes d'images de l'atlas | **laisser** |
| `layout/lyt_*.sceneanimation` | pistes d'animation (Mario, Luigi, unlock, blind, wallpaper) | **laisser** |
| `strings/*/strings.lng` | textes des 9 langues, montés en tmpfs par hakchi | **ne pas toucher** |
| `version`, `init.json`, `dependencies.json` | identification du build, table de ressources | **laisser** |

## 8. Ce qui est impossible et l'équivalent B1 retenu

| # | Élément | Pourquoi | Équivalent B1 |
|---|---|---|---|
| I1 | **Pages Manuel / Affichage en ardoise** | fond = `clearColor #F1EEEC` dans `defaultscene.scn` (disposition), pas une image ; texte noir par `colors_eur.lua` (bytecode) | garder ces pages claires et harmoniser les sprites (dialogues, boutons, cases) ; **décision utilisateur** : modifier ces deux nombres dans `defaultscene.scn` sortirait du périmètre « ressources seules » |
| I2 | **Petit interrupteur violet strié sous le jeu sélectionné** | position du curseur et des cartes figée par scripts et disposition ; aucun sprite sous la jaquette | dessiné **dans `game_card_active`** (barre `VIOLET` striée `STRIPE_LIGHT` / `STRIPE_DARK` au bas de la plaque, `84×92`), à valider en rendu |
| I3 | **Animations Mario / Luigi** | pilotées par `clover_character.lua` et `lyt_mario*`, `lyt_luigi*` | **neutraliser** les images (transparentes, tailles inchangées) |
| I4 | **Composition alignée à gauche comme une fiche de jeu** | disposition figée par la scène et les scripts | impossible ; on reproduit palette, typo et interrupteur, pas la composition |
| I5 | **Texte exactement `#E4E2EA` / `#8E8A9C` partout** | couleur de label = multiplicateur fixe (blanc ou noir) dans le bytecode | glyphes peints `#E4E2EA` dans l'atlas ; `#8E8A9C` non atteignable : les textes secondaires restent `TEXT` |
| I6 | **Halo / lueur du déverrouillage** | `halo`, `halo2`, `flash` sont des sprites additifs animés | rendus **transparents** (conforme à « aucun halo ») |
| I7 | **Teintes `bg_mario` / `bg_luigi` / `bg_base`** | constantes du tween `NodeBG` en bytecode | effet négligeable sur fond sombre ; à vérifier en rendu, sinon repeindre le fond en compensant |
| I8 | **Police `title`** | déjà remplacée par `font_remount` de hakchi | **laisser** ; Jost sur `s`, `m`, `l` seulement |
| I9 | **Durée exacte de la boucle musicale** | `bgm_home.wav` d'origine : 52,36 s ; aucune contrainte moteur identifiée | 55 à 65 s acceptable ; à valider sur la console avant le hmod |
