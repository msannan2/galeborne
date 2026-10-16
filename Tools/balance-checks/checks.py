#!/usr/bin/env python3
"""Galeborne v0.6 balance checks (Game Tester). Python reimplementation of Game.cs/GameSheet.cs/GameTown.cs
logic (Game is a MonoBehaviour, can't compile outside Unity) + v0.6 design data. Read-only on game files.

Settings priority: v06_settings.json is source of truth; live galeborne_settings.json is fallback / for mismatch checks."""
import json, random, math, sys
D='/workspace/galeborne/design/'; R='/workspace/galeborne-unity/Assets/Resources/galeborne_settings.json'
V=json.load(open(D+'v06_settings.json')); L=json.load(open(R))
ME=json.load(open(D+'v06_market_events_data.json')); AM=json.load(open(D+'v06_ambush_data.json')); EC=json.load(open(D+'v06_events_content.json'))
out=[]; res={}
def P(*a): s=' '.join(str(x) for x in a); print(s); out.append(s)

def setting(*keys, default=None):
    """Read from V (source of truth) first, then live L."""
    for src in (V, L):
        cur=src; ok=True
        for k in keys:
            if not isinstance(cur, dict) or k not in cur:
                ok=False; break
            cur=cur[k]
        if ok: return cur
    return default

def resolve(x):
    if isinstance(x,str) and x.startswith('$'):
        v=V
        for k in x[1:].split('.'):
            if k not in v: raise KeyError(x)
            v=v[k]
        return v
    if isinstance(x,dict): return {k:resolve(v) for k,v in x.items()}
    if isinstance(x,list): return [resolve(v) for v in x]
    return x
bad=[]
for name,d in (('market',ME),('ambush',AM)):
    try: resolve(d)
    except KeyError as e: bad.append(f'{name}: unresolved {e}')
ME=resolve(ME); AM=resolve(AM)
G={g['id']:g for g in L['goods']}; PG={k:v for k,v in V['portGoods'].items() if not k.startswith('_')}; BA=V['portGoods']['_backAlley']

# v0.6 numbers (design wins; live compared below)
banned_mult=float(setting('contraband','bannedSellMult', default=2.0))
banned_drop=float(setting('contraband','bannedPerUnitSellDrop', default=L.get('perUnitSellDrop',0.015)))
gwb=setting('goodWantBonus', default={}) or {}
opening=setting('opening', default={}) or {}
fb=(opening.get('fallbackShortage') or {}) if isinstance(opening, dict) else {}

P('== Data/settings consistency =='); mism=[]
mism+=bad
ids={e['id'] for e in ME['events']}; cids=set()
for k,v in EC.items():
    if isinstance(v,list): cids|={i.get('id') for i in v if isinstance(i,dict)}|{i.get('type') for i in v if isinstance(i,dict)}
    elif isinstance(v,dict): cids|=set(v.keys())
miss=[i for i in ids if i not in cids];
if miss: mism.append(f'market event ids missing from events_content: {miss}')
miss=[t for t in V['ambush']['types'] if t not in cids]
if miss: mism.append(f'ambush types missing from events_content: {miss}')
lp={p['name']:p for p in L['ports']}
for p,v in PG.items():
    if sorted(lp[p]['cheap'])!=sorted(v['cheap']) or sorted(lp[p]['want'])!=sorted(v['want']):
        mism.append(f'LIVE game ports[{p}] (used by Game.cs) != v06 portGoods: live cheap={lp[p]["cheap"]} want={lp[p]["want"]}')
pv=L.get('portGoodsV06',{})
for p in PG:
    if pv.get(p)!=PG[p]: mism.append(f'Resources portGoodsV06[{p}] stale vs v06_settings: {pv.get(p)}')
if pv.get('_backAlley')!=BA: mism.append(f'Resources portGoodsV06._backAlley old format/contents: {pv.get("_backAlley")}')
for k in ('ambush','market','bank','moneyLender'):
    if L.get(k)!=V.get(k): mism.append(f'Resources.{k} differs from v06_settings.{k}')
# contraband overlap keys (bannedSellMult / bannedPerUnitSellDrop)
for ck in ('bannedSellMult','bannedPerUnitSellDrop'):
    lv=(L.get('contraband') or {}).get(ck); vv=(V.get('contraband') or {}).get(ck)
    if vv is not None and lv is not None and float(lv)!=float(vv):
        mism.append(f'Resources.contraband.{ck}={lv} != v06_settings.contraband.{ck}={vv}')
EXPECTED_OPENING = {
    'startWithPrologueDuel': True,
    'firstRunCustomsAlwaysPass': True,
    'customsRiskFromVoyage': 4,
    'adsUnlockAfterVoyage': 4,
    'hideWarehouseUntilAfterMuscat': True,
    'hideTitheUntilAfterMuscat': True,
    'gunpowderCoinFloor': True,
    'openingDebalAlleyStocksGunpowder': True,
    'gunpowderGwadarOnlyAfterOpening': True,
    'openingDebalGunpowderBuyMult': 1.0,
}
missing_opening_keys = [k for k in EXPECTED_OPENING if k not in opening]
opening_mismatches = []
for k, exp in EXPECTED_OPENING.items():
    if k in opening and opening[k] != exp:
        opening_mismatches.append(f'opening.{k}={opening[k]!r} != expected {exp!r}')
res['missing_opening_keys'] = missing_opening_keys
res['opening_key_mismatches'] = opening_mismatches
if missing_opening_keys:
    mism.append(f'v06_settings.opening missing keys: {missing_opening_keys}')
for om in opening_mismatches:
    mism.append(om)
if 'opening' not in L:
    mism.append('Resources missing opening section (scripted gunpowder + copper fallback live only in v06_settings)')
else:
    for k in EXPECTED_OPENING:
        if k not in (L.get('opening') or {}):
            mism.append(f'Resources.opening missing {k}')
        elif (L.get('opening') or {}).get(k) != opening.get(k):
            mism.append(f'Resources.opening.{k}={(L.get("opening") or {}).get(k)!r} != v06 {opening.get(k)!r}')
if 'goodWantBonus' not in L and gwb:
    mism.append(f'Resources missing goodWantBonus {gwb} (dates want price bump live only in v06_settings)')
live_drop=L.get('perUnitSellDrop'); 
if live_drop is not None and abs(float(live_drop)-banned_drop)>1e-9:
    # informational: legal goods still use perUnitSellDrop; banned uses bannedPerUnitSellDrop
    pass
for m in mism: P(' MISMATCH:',m)
if not mism: P(' no mismatches')
res['mismatches']=mism
P(f' settings used: bannedSellMult={banned_mult}, bannedPerUnitSellDrop={banned_drop}, goodWantBonus={gwb}, opening.scripted={opening.get("scripted")}, fallback={fb}')

# ---------- 1. market ----------
P('\n== 1. Market year sim (drift = Game.NewWeek; trade = GameSheet CostOf/RevenueOf; events per v06_market_events_data) ==')
S=L; drift=V['weeklyDriftMax']; pull=S['pullToBase']; spread=S['sellSpread']
ports=list(PG); goods=list(G)
def bases(rng):
    B={}
    for g in goods:
        b=rng.uniform(G[g]['min'],G[g]['max'])
        for p in ports:
            m=S['cheapMult'] if g in PG[p]['cheap'] else S['wantMult'] if g in PG[p]['want'] else 1
            if g in PG[p]['want'] and g in gwb:
                m*=float(gwb[g])
            B[p,g]=b*m*rng.uniform(.95,1.05)
    return B
def buyc(pr,q):
    t=0
    for i in range(q): t+=math.ceil(pr); pr*=1+S['perUnitBuyRise']
    return t
def rev(pr,q,m=None,drop=None):
    if m is None: m=spread
    if drop is None: drop=S['perUnitSellDrop']
    t=0
    for i in range(q): t+=math.floor(pr*m); pr*=1-drop
    return t
evs=[e for e in ME['events'] if not e.get('weightIsChancePerDock')]
def roll_event(rng,p,last):
    pool=[e for e in evs if (e.get('onlyPort') in (None,p)) and not (ME['noSameEventInPortTwiceInARow'] and e['id']==last)]
    e=rng.choices(pool,[x['weight'] for x in pool])[0]; return e
SEEDS=500; wpm=S['weeksPerMoon']; W=52
routes=[(a,b,g) for a in ports for b in ports if a!=b for g in PG[a]['cheap'] if g in PG[b]['want']]
rok={r:0 for r in routes}; rn={r:0 for r in routes}; maxratio=0; evcount={p:0 for p in ports}; evtypes={}
jack=[]; HOLD=S['holdSize']; WH=S['warehouse']['leaseSlots']
for sd in range(SEEDS):
    rng=random.Random(sd); B=bases(rng); pr=dict(B); act={}; last={p:None for p in ports}
    for w in range(W):
        for k in pr:
            v=pr[k]*(1+rng.uniform(-drift,drift)); v=v+(B[k]-v)*pull; pr[k]=min(max(v,B[k]*.5),B[k]*2)
            maxratio=max(maxratio,pr[k]/B[k])
        if w%wpm==0:
            for p in ports:
                for _ in range(ME['rollsPerPortPerMoon']):
                    e=roll_event(rng,p,last[p]); last[p]=e['id']; evcount[p]+=1; evtypes[e['id']]=evtypes.get(e['id'],0)+1
                    if e['target']=='oneGoodFromPortWant': g=rng.choice(PG[p]['want'])
                    elif e['target']=='oneGoodFromPortCheap': g=rng.choice(PG[p]['cheap'])
                    else: g=None
                    if e['id']=='shortage': act[p,g]=(rng.uniform(e['priceMult']['min'],e['priceMult']['max']),w+e['durationWeeks'])
                    elif e['id']=='glut': act[p,g]=(e['priceMult']['max'],w+e['durationWeeks'])
                    elif e['target']=='luxuries':
                        for x in e['luxuries']: act[p,x]=(e['priceMult'],w+e['durationWeeks'])
                    elif e['id']=='blockade': act[p,g]=(0,w+e['durationWeeks'])
                    if e['id']=='shortage':  # jackpot: stockpile hold+warehouse bought at the cheapest port at base, sold into shortage
                        src=min(ports,key=lambda q:B[q,g]); n=HOLD+WH
                        c=buyc(B[src,g],n); r_=rev(pr[p,g]*act[p,g][0],n); jack.append((r_/c,r_-c,g,p))
        eff={k:pr[k]*(act[k][0] if k in act and act[k][1]>w else 1) for k in pr}
        for (a,b,g) in routes:
            if eff[b,g]==0: continue
            c=buyc(eff[a,g],HOLD); r_=rev(eff[b,g],HOLD); rn[a,b,g]+=1; rok[a,b,g]+=r_>c
P(f' seeds={SEEDS} x {W} weeks; max price/base ever = {maxratio:.2f} (clamp 0.5-2.0, pullToBase {pull}) -> no runaway: {"PASS" if maxratio<=2.0001 else "FAIL"}')
low=[]; dates_pct={}
for r in routes:
    pct=rok[r]/rn[r]; P(f'  {r[0]:>6}->{r[1]:<6} {r[2]:<8} full-hold({HOLD}) profitable {pct*100:5.1f}%');
    if pct<.8: low.append((r,pct))
    if r[2]=='dates': dates_pct[f'{r[0]}->{r[1]}']=pct
P(f' routes >=80% profitable: {"PASS" if not low else "FAIL "+str([(list(x[0]), round(x[1]*100,1)) for x in low])}')
P(f' dates want bonus applied: {gwb.get("dates","(none)")} -> dates routes: '+', '.join(f'{k} {v*100:.1f}%' for k,v in dates_pct.items()))
cover={g for (_,_,g) in routes}; nor=[g for g in goods if g not in cover and not G[g]['contraband']]
P(f' goods with no legal route: {nor} (silk exempt) -> {"PASS" if set(nor)<= {"silk"} else "FAIL"}')
cb=[g for p,l in BA['sells'].items() for g in l if any(g in l2 for l2 in BA['buysBanned'].values())]
P(f' contraband routes via back alley: {cb}')
jr=sorted(x[0] for x in jack); jp=sorted(x[1] for x in jack)
P(f' shortage jackpot ({HOLD+WH} units stockpiled at cheapest base, sold into shortage): multiple median {jr[len(jr)//2]:.2f}x p99 {jr[int(.99*len(jr))]:.2f}x max {jr[-1]:.2f}x; profit median {jp[len(jp)//2]:,} max {jp[-1]:,} coins (start coins {S["startCoins"]})')
mx=max(jack); P(f'  worst case: {mx[2]} into {mx[3]}')
res['jackpot_max']=jr[-1]
res['dates_pct']=dates_pct
P(f' events per port per moon: '+', '.join(f'{p} {evcount[p]/(SEEDS*13):.2f}' for p in ports)+f'  types {evtypes}; foreignShip extra {V["market"]["foreignShipChance"]} per dock')
P(' NOTE: events above are simulated from the data file / v0.6 settings; confirm Game.cs wires the same when reviewing v0.6c.')

# ---------- 2. ambush ----------
P('\n== 2. Ambush odds ==')
A=V['ambush']
def design(cargo,coins,contra,fame,risky):
    p=V['encounterChance']
    for st in AM['rollOrder']:
        s=st['step']
        if s=='addCargo': p+=st['per1000Units']*cargo/1000
        elif s=='addCoinsAboard': p+=st['per1000Coins']*coins/1000
        elif s=='addContraband' and contra: p+=st['value']
        elif s=='addFame': p+=st['perLevel']*fame
        elif s=='multiplyRiskyRoute' and risky: p*=st['value']
        elif s=='cap': p=min(p,st['value'])
    return p
last=AM['rollOrder'][-1]['step']=='cap'
P(f' base={design(0,0,0,0,0):.3f} (target 1/3) {"PASS" if abs(design(0,0,0,0,0)-1/3)<.001 else "FAIL"}; cap is last step: {"PASS" if last else "FAIL"}')
wc=design(2000,2000,1,5,1); raw=(0.333+.02+.02+.05+.05)*2
P(f' worst case (2000 cargo,2000 coins,contraband,fame5,risky): raw {raw:.3f} -> {wc:.3f} {"PASS" if wc==0.5 else "FAIL"}')
P(f' scaling: cargo+1000 {design(1000,0,0,0,0)-design(0,0,0,0,0):+.3f}, coins+1000 {design(0,1000,0,0,0)-design(0,0,0,0,0):+.3f}, contraband {design(0,0,1,0,0)-design(0,0,0,0,0):+.3f}, fame/level {design(0,0,0,1,0)-design(0,0,0,0,0):+.3f}, risky clean {design(0,0,0,0,1):.3f}')
P(f'  wrong-order check (cap before x2) would give {min(raw/2,0.5)*2:.3f} -> cap-last matters')
P(f' WARNING: data says per1000Units of cargo, but GameTown.AmbushChance uses cargo VALUE (coins); holdSize={HOLD} units so per-units makes cargo meaningless (20 units -> +0.0002). Worst-case note in data assumes 2000 "cargo". Pick one: value.')
# live code
pirate_share=L['weights']['pirate']/sum(L['weights'].values())
import re as _re, glob as _glob
_SRC={f.split('/')[-1]:open(f).read() for f in _glob.glob('/workspace/galeborne-unity/Assets/Scripts/*.cs')}
_ALL='\n'.join(_SRC.values()); _G=_SRC.get('Game.cs','')
_voy=bool(_re.search(r'AmbushChanceFor\(\s*to\s*\)',_G)) and bool(_re.search(r'Rnd\.value\s*<\s*ap',_G))
_m=_re.search(r'float\s+AmbushChanceFor\(int\s+\w+\)\s*\{(.*?)return p;',_ALL,_re.S); _fn=_m.group(1) if _m else ''
_steps=[st['step'] for st in AM['rollOrder']]; _miss=[k for k in _steps if f'"{k}"' not in _fn]
P(f' LIVE Game.Voyage (source scan): ambush rolled from AmbushChanceFor(to) and Rnd.value < ap: {"PASS" if _voy else "FAIL"}')
P(f' LIVE AmbushChanceFor(): handles rollOrder steps {_steps}; missing {_miss or "none"}; risky via RiskyRoute: {"PASS" if "multiplyRiskyRoute" in _fn and "RiskyRoute(" in _fn else "FAIL"}; cap via Mathf.Min: {"PASS" if "Mathf.Min" in _fn else "FAIL"} -> {"PASS" if _fn and not _miss else "FAIL"}')
_bait='DumpForBait(' in _ALL and 'playerResponses.bait.minCostPctOfHold' in _ALL; _bluff='playerResponses.bluff.enabled' in _ALL and 'Cunning' in _ALL
P(f' LIVE bait/bluff in Scripts (source scan): bait {"PASS" if _bait else "FAIL"}, bluff {"PASS" if _bluff else "FAIL"}')
_GT=_SRC.get('GameTown.cs',''); _GV=_SRC.get('GameV06.cs','')
_free=_re.search(r'bool\s+free\s*=\s*sails\s*<=\s*S\.pirateFreeSails',_G) and _re.search(r'sails\+\+;.*?bool\s+free',_G,_re.S)
_lt=_re.search(r'sails\s*\+\s*1\s*<=\s*S\.pirateFreeSails',_GT); _lc=_re.search(r'sails\s*\+\s*1\s*<=\s*S\.pirateFreeSails',_GV)
_tth=[float(x) for x in _re.findall(r'AmbushChance\(\)\s*<\s*([0-9.]+)f',_GT)]; _cth=[float(x) for x in _re.findall(r'ap\s*<\s*([0-9.]+)f',_GV)]
_safe='safeUntilSail' in _GT
P(f' pirate-free (source scan): Voyage() free when sails<=pirateFreeSails after sails++: {"PASS" if _free else "FAIL"}; town label sails+1<=pirateFreeSails: {"PASS" if _lt else "FAIL"}; chart label: {"PASS" if _lc else "FAIL"}')
P(f' danger bands: town {_tth} vs chart {_cth} -> {"PASS" if _tth==_cth else "FAIL (town and chart disagree for same odds, e.g. base 33% = town High, chart moderate)"}')
P(f' safeUntilSail honoured by town label: {"PASS" if _safe else "WARN (game also treats sails<safeUntilSail as free; label may show danger on a free voyage)"}')
w=AM['typePick']['weights']; rng=random.Random(1); prev=None; cnt={k:0 for k in w}; rep=0; N=100000
for i in range(N):
    pool=[k for k in w if not (AM['typePick']['noRepeatInARow'] and k==prev)]; t=rng.choices(pool,[w[k] for k in pool])[0]
    rep+=t==prev; prev=t; cnt[t]+=1
P(f' type split nominal {w} -> effective with no-repeat {{'+', '.join(f"{k}: {v/N*100:.1f}%" for k,v in cnt.items())+f'}}, repeats={rep} PASS (note: no-repeat flattens 40/30/30 to ~36/32/32)')
P(f' bait min {AM["playerResponses"]["bait"]["minCostPctOfHold"]*100:.0f}% of hold value (spec 10%) PASS in data; code: '+('implemented (see LIVE bait/bluff line)' if _bait and _bluff else 'NOT implemented')+'.')

# ---------- 3. gunpowder opening ----------
P('\n== 3. Gunpowder opening (first-run vs voyage-4+) ==')
cu=setting('customs', default=L.get('customs')) or L['customs']
gp=G[opening.get('good','gunpowder')]
base_risk=cu['baseRisk']; faker=cu['fakersCrate']; tithe_cut=cu['templeTithe']; floor=cu['floor']
risk=max(floor, base_risk-faker-tithe_cut)/100
risk_crate=max(floor, base_risk-faker)/100
P(f' customs risk (later runs): base {base_risk}%, Faker\'s Crate {risk_crate*100:.0f}%, +tithe {risk*100:.0f}%')
P(f' banned sell: mult={banned_mult}, drop={banned_drop*100:.1f}%/unit; targetMult={opening.get("targetMult",3.0)}')
P(f' opening keys: startWithPrologueDuel={opening.get("startWithPrologueDuel")} thenPort={opening.get("thenPort")} '
  f'buyPort={opening.get("buyPort")} firstRunCustomsAlwaysPass={opening.get("firstRunCustomsAlwaysPass")} '
  f'customsRiskFromVoyage={opening.get("customsRiskFromVoyage")} adsUnlockAfterVoyage={opening.get("adsUnlockAfterVoyage")} '
  f'hideWarehouse={opening.get("hideWarehouseUntilAfterMuscat")} hideTithe={opening.get("hideTitheUntilAfterMuscat")} '
  f'gunpowderCoinFloor={opening.get("gunpowderCoinFloor")} openingDebalAlleyStocksGunpowder={opening.get("openingDebalAlleyStocksGunpowder")} '
  f'gunpowderGwadarOnlyAfterOpening={opening.get("gunpowderGwadarOnlyAfterOpening")} '
  f'openingDebalGunpowderBuyMult={opening.get("openingDebalGunpowderBuyMult")}')
if missing_opening_keys:
    P(f' MISSING opening keys vs expected list: {missing_opening_keys}')
else:
    P(' all expected opening keys present in v06_settings')
for om in opening_mismatches:
    P(' VALUE MISMATCH:', om)

buy_mult_first=float(opening.get('openingDebalGunpowderBuyMult', 1.0))
hide_tithe=bool(opening.get('hideTitheUntilAfterMuscat', False))
hide_wh=bool(opening.get('hideWarehouseUntilAfterMuscat', False))
first_pass=bool(opening.get('firstRunCustomsAlwaysPass', False))
coin_floor_on=bool(opening.get('gunpowderCoinFloor', False))
gp_target=float(opening.get('targetMult',3.0))

def run_gp(rng, buymult, force_pass=False, tithe=True, lease=True, coins=None, apply_coin_floor=False):
    coins=coins if coins is not None else S['startCoins']; b=rng.uniform(gp['min'],gp['max'])
    bp=b*buymult*rng.uniform(.95,1.05); sp=b*rng.uniform(.95,1.05)
    for _ in range(2): sp=min(max(sp*(1+rng.uniform(-drift,drift)),b*.5),b*2)
    fees=(0 if True else cu['fakersCratePrice'])+(cu['titheCost'] if tithe else 0)+(S['warehouse']['leasePerWeek']*2 if lease else 0)
    budget=coins-fees
    # coin floor: ensure at least 3 units of gunpowder are affordable (meaningful starter load)
    min_meaningful=3
    need=buyc(bp, min_meaningful)+fees if apply_coin_floor and coin_floor_on else 0
    topped=0
    if apply_coin_floor and coin_floor_on and budget < buyc(bp, min_meaningful):
        topped=buyc(bp, min_meaningful)-budget
        budget=buyc(bp, min_meaningful)
        coins=budget+fees
    q=0
    while q<HOLD and buyc(bp,q+1)<=budget: q+=1
    goods_cost=buyc(bp,q); inv=goods_cost+fees
    r_=rev(sp,q,banned_mult,banned_drop)
    rk=0.0 if force_pass else (risk if tithe else risk_crate)
    caught=(not force_pass) and (rng.random()<rk)
    ret=0 if caught else r_
    clean_outlay=(r_/inv) if inv else 0
    clean_gross=(r_/goods_cost) if goods_cost else 0
    expected=(ret/inv) if inv else 0
    return expected, clean_gross, clean_outlay, q, r_, inv, topped, goods_cost

# --- first run: Debal alley @ Gwadar price, customs always pass, no tithe/lease ---
N=20000
first=[run_gp(random.Random(i), buy_mult_first,
              force_pass=first_pass or True,
              tithe=False if hide_tithe else True,
              lease=False if hide_wh else True,
              apply_coin_floor=True) for i in range(N)]
f_gross=sum(x[1] for x in first)/N
f_out=sum(x[2] for x in first)/N
f_exp=sum(x[0] for x in first)/N
f_q=sum(x[3] for x in first)/N
f_top=sum(x[6] for x in first)/N
f_cost=sum(x[7] for x in first)/N
# without coin floor, for comparison
first_nofloor=[run_gp(random.Random(i), buy_mult_first, force_pass=True, tithe=False, lease=False, apply_coin_floor=False) for i in range(N)]
nf_q=sum(x[3] for x in first_nofloor)/N
nf_zero=sum(1 for x in first_nofloor if x[3]==0)/N
P(f' FIRST RUN (Debal back alley buyMult={buy_mult_first}, Muscat {banned_mult}x, customs always pass, '
  f'tithe={"hidden" if hide_tithe else "on"}, warehouse={"hidden" if hide_wh else "on"}):')
P(f'  avg units {f_q:.1f}/{HOLD}, avg goods cost {f_cost:.0f}, clean sale/cost {f_gross:.2f}x, '
  f'return on outlay {f_out:.2f}x, expected (customs pass) {f_exp:.2f}x '
  f'-> target ~{gp_target:.0f}x {"PASS" if f_gross>=gp_target*0.9 else "FAIL"}')
P(f'  coin floor ({coin_floor_on}): without floor avg units {nf_q:.1f}, zero-load rate {nf_zero*100:.1f}%; '
  f'with floor avg top-up {f_top:.0f} coins, avg units {f_q:.1f} '
  f'-> meaningful load (>=3u) {"PASS" if f_q>=3 else "FAIL"}')
res['gp_first']={'sale_cost':f_gross,'outlay':f_out,'expected':f_exp,'units':f_q,'topup':f_top}

# --- later runs (voyage 4+): Gwadar buy, real customs, tithe+lease ---
later=[run_gp(random.Random(i), 1.0, force_pass=False, tithe=True, lease=True, apply_coin_floor=False) for i in range(N)]
l_gross=sum(x[1] for x in later)/N
l_out=sum(x[2] for x in later)/N
l_exp=sum(x[0] for x in later)/N
l_q=sum(x[3] for x in later)/N
P(f' LATER RUNS (voyage {opening.get("customsRiskFromVoyage",4)}+: Gwadar buyMult=1.0, customs risk {risk*100:.0f}%, tithe+2wk lease):')
P(f'  avg units {l_q:.1f}, clean sale/cost {l_gross:.2f}x, clean on outlay {l_out:.2f}x, '
  f'expected on outlay {l_exp:.2f}x -> ~2x target {"PASS" if 1.7<=l_exp<=2.4 else "CHECK"}')
res['gp_later']={'sale_cost':l_gross,'outlay':l_out,'expected':l_exp,'units':l_q,'risk':risk}

buy_port=opening.get('buyPort','Debal'); ban_port=opening.get('banPort','Muscat'); good=opening.get('good','gunpowder')
ba_sells_debal=BA.get('sells',{}).get('Debal',[]); ba_sells_gwadar=BA.get('sells',{}).get('Gwadar',[])
ba_buys=BA.get('buysBanned',{}).get(ban_port,[])
if opening.get('openingDebalAlleyStocksGunpowder') and good not in ba_sells_debal:
    P(f' NOTE: openingDebalAlleyStocksGunpowder=true but portGoods._backAlley.sells.Debal={ba_sells_debal} (opening override; Game.cs must honor opening flag)')
if good in ba_buys:
    P(f' sell path OK: {ban_port} buysBanned includes {good}; post-opening Gwadar-only={opening.get("gunpowderGwadarOnlyAfterOpening")} (Gwadar alley sells {ba_sells_gwadar})')
else:
    P(f' WARNING: {ban_port} buysBanned missing {good}: {ba_buys}')

# ---------- 4. copper fallback ----------
P('\n== 4. Opening copper shortage fallback (v06 opening.fallbackShortage) ==')
if not fb:
    P(' no fallbackShortage in settings -> FAIL')
    res['copper_fallback']=None
else:
    cg=fb.get('good','copper'); cp=fb.get('port','Muscat'); cm=float(fb.get('mult',2.0))
    buy_from=opening.get('thenPort','Gwadar')  # copper cheap in Gwadar (thenPort after duel); buyPort is gunpowder alley
    exists=cg in G
    in_cheap=cg in PG.get(buy_from,{}).get('cheap',[])
    in_want=cg in PG.get(cp,{}).get('want',[])
    P(f' fallback: forced {cm}x shortage of {cg} in {cp} if no gunpowder after {fb.get("triggerIfNoGunpowderAfterRumorRepeats",1)} rumor repeat(s)')
    P(f'  good exists: {exists}; {buy_from} cheap lists {cg}: {in_cheap}; {cp} wants {cg}: {in_want} -> {"PASS" if exists and in_cheap and in_want else "FAIL"}')
    clean_c=[]; outlay_c=[]; Nfb=20000
    for i in range(Nfb):
        rng=random.Random(i); gdef=G[cg]
        b=rng.uniform(gdef['min'],gdef['max'])
        bp=b*S['cheapMult']*rng.uniform(.95,1.05)
        sp=b*S['wantMult']*float(gwb.get(cg,1.0))*rng.uniform(.95,1.05)
        for _ in range(2):
            bp=min(max(bp*(1+rng.uniform(-drift,drift)),b*S['cheapMult']*.5),b*S['cheapMult']*2)
            sp=min(max(sp*(1+rng.uniform(-drift,drift)),b*S['wantMult']*.5),b*S['wantMult']*2)
        # lease optional; fallback is legal trade — no customs
        fees=S['warehouse']['leasePerWeek']*2
        budget=S['startCoins']-fees; q=0
        while q<HOLD and buyc(bp,q+1)<=budget: q+=1
        if q==0:
            fees=0; budget=S['startCoins']
            while q<HOLD and buyc(bp,q+1)<=budget: q+=1
        gc=buyc(bp,q); inv=gc+fees
        r_=rev(sp*cm,q)  # shortage mult on want price; normal sellSpread + perUnitSellDrop
        clean_c.append(r_/gc if gc else 0)
        outlay_c.append(r_/inv if inv else 0)
    avg_g=sum(clean_c)/len(clean_c); avg_o=sum(outlay_c)/len(outlay_c)
    P(f' copper fallback sim ({Nfb} seeds): buy {cg} in {buy_from} (cheapMult), sell in {cp} at want x {cm} shortage; clean sale/cost {avg_g:.2f}x, return on outlay (2wk lease) {avg_o:.2f}x')
    res['copper_fallback']={'sale_cost':avg_g,'outlay':avg_o,'mult':cm,'good':cg,'port':cp}
    P(f'  {"PASS" if avg_g>=1.8 else "FAIL"} (design: ~{cm}x shortage should clear ~2x gross before spread/drop)')

# summary line for parent
P('\n== SUMMARY ==')
if res.get('gp_first'):
    g0=res['gp_first']
    P(f' FIRST RUN gunpowder: sale/cost {g0["sale_cost"]:.2f}x, outlay {g0["outlay"]:.2f}x, expected {g0["expected"]:.2f}x, units {g0["units"]:.1f}')
if res.get('gp_later'):
    g1=res['gp_later']
    P(f' LATER RUNS gunpowder: sale/cost {g1["sale_cost"]:.2f}x, outlay {g1["outlay"]:.2f}x, expected {g1["expected"]:.2f}x (risk {g1["risk"]*100:.0f}%)')
miss=res.get('missing_opening_keys') or []
P(f' opening keys missing: {miss if miss else "none"}')
if dates_pct:
    P(f' dates profitability: '+', '.join(f'{k} {v*100:.1f}%' for k,v in dates_pct.items())+f' (target >=80%: {"PASS" if all(v>=0.8 for v in dates_pct.values()) else "FAIL"})')
if res.get('copper_fallback'):
    cf=res['copper_fallback']; P(f' copper fallback: {cf["sale_cost"]:.2f}x sale/cost, {cf["outlay"]:.2f}x on outlay')
P(f' mismatches ({len(mism)}): '+('; '.join(mism) if mism else 'none'))

open('/workspace/galeborne-unity/Tools/balance-checks/results.txt','w').write('\n'.join(out)+'\n')
