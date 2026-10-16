for a in "$@"; do python3 tune.py $a; echo "## $a"; bash t.sh | python3 -c "
import sys,re
t=sys.stdin.read()
for blk in t.split('=== Bot: ')[1:]:
  name=blk.split('(')[0].strip(); w=re.search(r'= ([\d.]+)%',blk).group(1); d=re.search(r'won \d+ = ([\d.]+)%',blk).group(1); l=re.search(r'length ([\d.]+) s \(median (\d+), p10 (\d+), p90 (\d+)',blk).groups()
  print(f'{name:38s} win {w:>5}%  duel {d:>5}%  len {l[0]} (p90 {l[3]})')"; done
