# k4 town layouts — hook into GameV06k.cs (Game Designer)

New file (already in project): `Assets/Scripts/TownLayoutsK4.cs` — `public static class TownLayoutsK4`,
`GameObject Build(string town, Transform parent, Material mat, Func<int,Rect> tu)`. Primitive MeshB on the town atlas (townMat), no new assets/licences.

## The only edit needed in GameV06k.cs (inside `BuildLandmarksV06k()`, currently lines 35–37)
Replace:
```csharp
    townLayouts["Gwadar"] = BuildLayoutGwadar(townLayoutsRoot);
    townLayouts["Debal"]  = BuildLayoutDebal(townLayoutsRoot);
    townLayouts["Muscat"] = BuildLayoutMuscat(townLayoutsRoot);
```
with:
```csharp
    foreach (var tn in new[] { "Gwadar", "Debal", "Muscat" }) townLayouts[tn] = TownLayoutsK4.Build(tn, townLayoutsRoot, townMat, TU); // k4 layouts (Game Designer)
```
Nothing else changes: `ShowTownLandmark()` already toggles `townLayouts[here]`, layouts stay on `townMat` (palette tint applies, landmarks keep `lmMat`).
The old `BuildLayoutGwadar/Debal/Muscat` become unused (leave them or delete them; deleting is optional).

## Clearances vs art/landmarks_k4/GUIDE.md
- Landmarks at z 54–56, y ≈16: layouts stay at z ≤ 34 (Muscat back crag) / ≤ 25 elsewhere, so no overlap.
- Centre stair |x| < 2.2 kept clear in Debal/Muscat.
- Debal top terrace leaves x 0..12 open under the spire/pennant; awnings are saffron/teal only (red is the pennant's).
- Gwadar nets are grey rope, not red. Muscat fort on the RIGHT crag (x +19).
- Note for CAD: GUIDE puts the Muscat town landmark at x −4 (left of centre) while the spec says right crag; the layout's own fort is on the right, so consider moving LM_Muscat_Town to x ≈ +6.
