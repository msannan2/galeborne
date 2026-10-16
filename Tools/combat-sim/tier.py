import json,sys
p='/workspace/galeborne-unity/Assets/Resources/galeborne_settings.json';j=json.load(open(p))
for kv in sys.argv[2:]:
  k,v=kv.split('='); t=[x for x in j['combat']['pirateTiers'] if x['name']==sys.argv[1]][0]; t[k]=(type(t[k])(float(v)) if not isinstance(t[k],bool) else v=='1') if k in t else float(v)
json.dump(j,open(p,'w'),indent=1)
