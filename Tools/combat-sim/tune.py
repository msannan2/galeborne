import json,sys,subprocess
p='/workspace/galeborne-unity/Assets/Resources/galeborne_settings.json';s=json.load(open(p))
for kv in sys.argv[1:]:
  k,v=kv.split('=');s['combat'][k]=type(s['combat'][k])(float(v)) if not isinstance(s['combat'][k],bool) else v=='1'
json.dump(s,open(p,'w'),indent=1)
