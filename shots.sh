#!/bin/bash
# half-scale captures on DISPLAY :5 (layout depends only on aspect: CanvasScaler match width)
export DISPLAY=:5
for r in 1080x2340:540x1170:55,30 1080x2400:540x1200:55,30 1812x2176:906x1088:45,20 1600x2560:720x1152:36,18; do
 IFS=: read res win safe <<<"$r"; w=${win%x*}; h=${win#*x}; d=shots/v051/$res; mkdir -p $d
 pkill -f Galeborne.x86_64; sleep 1
 timeout 600 ./Builds/Linux/Galeborne.x86_64 -screen-fullscreen 0 -screen-width $w -screen-height $h -gbshots $d -gbtag $res -gbsafe $safe -logFile logs/shots_$res.log
 echo "$res done $(ls $d | wc -l) $(date +%T)"
done
