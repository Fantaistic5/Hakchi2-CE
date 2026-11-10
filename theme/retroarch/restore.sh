#!/bin/sh
# Rend a RetroArch ses valeurs d'avant B1 : usage  restore.sh <retroarch.cfg> [b1.cfg]
# Pour chaque cle de b1.cfg : remet la ligne de retroarch.cfg.b1bak, ou retire la
# ligne si la cle n'y etait pas. Les autres lignes (reglages modifies depuis par
# RetroArch) restent telles quelles. La sauvegarde est supprimee apres succes.
cfg="$1"
b1="$2"
[ -n "$b1" ] || b1="$(dirname "$0")/b1.cfg"
bak="$cfg.b1bak"
[ -f "$cfg" ] && [ -f "$b1" ] && [ -f "$bak" ] || { echo "restore.sh: rien a restaurer" >&2; exit 0; }
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
function ok(s) { return !(s ~ /^[ \t]*#/ || s !~ /=/) }
FNR == 1 { f++ }
f == 1 { if (ok($0)) want[key($0)] = 1; next }
f == 2 { if (ok($0) && (key($0) in want) && !(key($0) in orig)) orig[key($0)] = $0; next }
{
  k = ok($0) ? key($0) : ""
  if (k != "" && (k in want)) { if ((k in orig) && !(k in done)) { print orig[k]; done[k] = 1 } }
  else print
}
END { for (k in orig) if (!(k in done)) print orig[k] }
' "$b1" "$bak" "$cfg" > "$cfg.b1tmp"
rc=$?
if [ "$rc" -eq 0 ]; then
  if b1_check; then
    cat "$cfg.b1tmp" > "$cfg"
    rc=$?
  else
    echo "restore.sh: ecriture tronquee (disque plein ?), retroarch.cfg laisse intact" >&2
    rc=2
  fi
fi
rm -f "$cfg.b1tmp"
[ "$rc" -eq 0 ] || { echo "restore.sh: ecriture impossible" >&2; exit 1; }
rm -f "$bak"
exit 0
