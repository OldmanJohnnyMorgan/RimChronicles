#!/usr/bin/env bash
# Regenerates public/sting.wav (placeholder news sting + drone bed, 16 s) with ffmpeg.
set -e
mkdir -p public
ffmpeg -y -loglevel error \
 -f lavfi -i "anoisesrc=d=2.2:c=pink:a=0.35" \
 -f lavfi -i "sine=f=55:d=3" -f lavfi -i "sine=f=110:d=3" \
 -f lavfi -i "sine=f=659.25:d=1.6" -f lavfi -i "sine=f=783.99:d=1.6" -f lavfi -i "sine=f=987.77:d=2.2" \
 -f lavfi -i "sine=f=73.42:d=12" -f lavfi -i "sine=f=110:d=12" \
 -filter_complex "\
[0]highpass=f=300,lowpass=f=6000,afade=t=in:d=1.8,afade=t=out:st=1.8:d=0.4,volume=0.9[whoosh];\
[1][2]amix=2,afade=t=out:st=0.05:d=2.5,volume=2.2,adelay=2000|2000[hit];\
[3]afade=t=out:st=0.1:d=1.4,volume=0.35,adelay=4200|4200[c1];\
[4]afade=t=out:st=0.1:d=1.4,volume=0.35,adelay=4500|4500[c2];\
[5]afade=t=out:st=0.2:d=1.9,volume=0.35,adelay=4800|4800[c3];\
[6][7]amix=2,tremolo=f=0.5:d=0.3,afade=t=in:d=2,afade=t=out:st=9:d=3,volume=0.5,adelay=3000|3000[bed];\
[whoosh][hit][c1][c2][c3][bed]amix=inputs=6:normalize=0,alimiter=limit=0.9,apad=whole_dur=16[out]" \
 -map "[out]" -ar 48000 -ac 2 public/sting.wav
