import json,sys
p='/workspace/galeborne-unity/Assets/Resources/galeborne_settings.json';j=json.load(open(p));c=j['combat']
for k in ['pirateAccBonus','pirateDamageMult','pirateCombat']: c.pop(k,None)
e,v=[dict(x.split('=') for x in a.split(',')) for a in sys.argv[1:3]]
def T(name,d,flags): return {"name":name,"reactionSeconds":float(d['r']),"headingNoiseDeg":float(d['n']),"accBonus":float(d['a']),"damageMult":float(d['d']),"captainCombat":float(d['c']),**flags}
c['pirateTiers']=[T("early",e,dict(seeksUpwind=False,usesFire=False,usesChain=True,usesGrape=False)),T("veteran",v,dict(seeksUpwind=True,usesFire=True,usesChain=True,usesGrape=True))]
c['earlyPirateUntilSail']=6
json.dump(j,open(p,'w'),indent=1)
