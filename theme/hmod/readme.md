---
Name: SNES USA B1 Theme
Creator: YKZ Corp
Category: UI
Version: 1.0.0
---
Theme sombre "B1" pour l'interface de la SNES Mini : ardoise, violet manette et lavande, police geometrique Jost (licence OFL, voir `OFL.txt`), et une musique de menu originale.

Seules des ressources (images, polices s/m/l/copyright, musique d'accueil) sont remplacees. Aucun script, aucune disposition, aucune animation.

Au demarrage, chaque fichier du theme est lie (`mount_bind`) sur l'original de `/usr/share/ui/<console>/resources/`, uniquement si les deux existent. Les originaux ne sont jamais modifies : desinstaller le mod suffit a retrouver l'interface d'origine apres redemarrage.

La police `title` n'est pas touchee : si vous utilisez *Font Remount*, votre propre `title.fnt` reste en place.
