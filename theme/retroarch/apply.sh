#!/bin/sh
# Habille RetroArch en B1 : usage  apply.sh <retroarch.cfg> [b1.cfg]
# - sauvegarde retroarch.cfg en retroarch.cfg.b1bak si la sauvegarde n'existe pas ;
# - remplace en place chaque cle listee dans b1.cfg (ajoutee a la fin si absente) ;
# - ne touche a aucune autre ligne. Idempotent. busybox sh, sans bashisme.
cfg="$1"
b1="$2"
[ -n "$b1" ] || b1="$(dirname "$0")/b1.cfg"
[ -f "$cfg" ] && [ -f "$b1" ] || { echo "apply.sh: fichier manquant" >&2; exit 1; }
[ -f "$cfg.b1bak" ] || cp "$cfg" "$cfg.b1bak" || { echo "apply.sh: sauvegarde impossible" >&2; exit 1; }
# garde : une ecriture tronquee (NAND pleine) ne doit jamais remplacer la config.
# Le resultat ne peut pas avoir moins de lignes que l'original moins les cles de b1.cfg.
# (B1_TEST_KEEP_LINES : tests seulement, simule une ecriture tronquee.)
b1_check(){
  [ -n "$B1_TEST_KEEP_LINES" ] && { head -n "$B1_TEST_KEEP_LINES" "$cfg.b1tmp" > "$cfg.b1tmp2" && cat "$cfg.b1tmp2" > "$cfg.b1tmp"; rm -f "$cfg.b1tmp2"; }
  nkeys=$(awk '!/^[ 	]*#/ && /=/ { n++ } END { print n + 0 }' "$b1")
  have=$(wc -l < "$cfg.b1tmp")
  want=$(( $(wc -l < "$cfg") - nkeys ))
  [ "$have" -ge "$want" ]
}
awk '
function key(s,   k) { k = s; sub(/[ \t]*=.*/, "", k); sub(/^[ \t]+/, "", k); return k }
FNR == 1 { f++ }
f == 1 {
  if ($0 ~ /^[ \t]*#/ || $0 !~ /=/) next
  k = key($0); line[k] = $0; order[++n] = k; next
}
{
  k = ($0 ~ /^[ \t]*#/ || $0 !~ /=/) ? "" : key($0)
  if (k != "" && (k in line)) { if (!(k in done)) { print line[k]; done[k] = 1 } }
  else print
}
END { for (i = 1; i <= n; i++) if (!(order[i] in done)) print line[order[i]] }
' "$b1" "$cfg" > "$cfg.b1tmp"
rc=$?
if [ "$rc" -eq 0 ]; then
  if b1_check; then
    cat "$cfg.b1tmp" > "$cfg"
    rc=$?
  else
    echo "apply.sh: ecriture tronquee (disque plein ?), retroarch.cfg laisse intact" >&2
    rc=2
  fi
fi
rm -f "$cfg.b1tmp"
[ "$rc" -eq 0 ] || { echo "apply.sh: ecriture impossible" >&2; exit 1; }
exit 0
