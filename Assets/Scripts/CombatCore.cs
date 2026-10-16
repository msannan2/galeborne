// Pure C# combat model (no UnityEngine) so the same rules run in the game and in the headless simulation.
using System;
using System.Collections.Generic;

[Serializable] public class ShipStats { public float hull, sails, gunsPerSide, crew, speed, reload; }
[Serializable] public class AmmoDef { public string name; public float hull, sails, guns, crew, range; }
[Serializable] public class WindCfg { public float running, broadReach, beam, closeHauled, intoWind; }
[Serializable] public class CombatCfg {
  public ShipStats hasan, pirate; public AmmoDef[] ammo; public WindCfg wind;
  public float speedScale, turnRate, halfSailMult, accel, startRangeMin, startRangeMax, escapeRange;
  public float baseAcc, upwindAccBonus, matchAccBonus, accPenaltyPer10mPastHalf, minAcc, arcDeg, aimedPartMult, damageScale;
  public float gunHpPerGun, sailDmgPerSpeedStep, speedLossPerStep, disableSails, crewReloadPenalty, injuredReloadMult;
  public float fireBurnPerSec, fireBurnSecs, fireDouseFromTick, fireDouseChance; public int firePerBattle;
  public float boardRange, duelZoneBase, duelZonePerCombat, bladeBonus, crewAdvantageBonus, hasanCombat; public bool hasanHasBlade;
  public int duelRounds; public float ringSeconds, avgPlayerHitPerZone, pirateDuelHit;
  public float fleeQuietSeconds, surrenderCrewFrac, noviceDuelBonus, strongboxMin, strongboxMax, surrenderCrewJoinChance; public int noviceDuels; public float assistFloor = 0.25f, assistFadeFrom = 0.35f, assistFadeSpan = 0.3f; public float aimAssistAcc = 0.25f, aimAssistDamageMult = 1.5f; public int aimAssistFirstFights = 5; public float encounterFleeBase = 0.35f, breachBelowHull = 0.5f, breachOpenSeconds = 6, breachEverySeconds = 15, breachDamageMult = 2; public float fleeSpeedMult = 1, tollSeconds = 9999, chainStopChance = 0, tollLootFrac = 0.5f; public float enemyFleeHull, fleeBase, fleeWindWeight, tollCargoPct, maxFightSeconds;
  public float sinkCargoPct, sinkCoinsMin, sinkCoinsMax, disableRareChance, ransom, bounty, prizeShipCoins, loseCargoPct, loseCrewInjuredPct; public int postLossSafeSails;
  public string[] charms; public BotProfile[] bots; public PirateTactics[] pirateTiers; public Difficulty[] difficulties; public int lateFame = 5; public int earlyPirateUntilSail;
}

public enum Part { Hull, Sails, Guns, Crew }
public enum Outcome { None, Sunk, Surrender, HasanStruck, Disabled, BoardReady, Escaped, HasanSunk, HasanBoarded, Timeout, TollPaid }

public class CShip {
  public float side = 90; public float x, z, heading, spd, rudder; public bool fullSail = true; public int ammo; public Part aim = Part.Hull;
  public float hull, sails, gunHp, crew, reloadT, burnT, burnTick; public int fireLeft; public ShipStats st; public bool injured;
  public float hull0, sails0, gun0, crew0;
  public CShip(ShipStats s, CombatCfg c) { st = s; hull = hull0 = s.hull; sails = sails0 = s.sails; gunHp = gun0 = s.gunsPerSide * c.gunHpPerGun; crew = crew0 = s.crew; fireLeft = c.firePerBattle; }
  public int Guns(CombatCfg c) => (int)Math.Ceiling(gunHp / c.gunHpPerGun);
  public bool Disabled(CombatCfg c) => hull > 0 && (sails <= c.disableSails || gunHp <= 0);
}

public class CombatCore {
  public CombatCfg C; public CShip H, P; public float windTo, t; public Random R; public Outcome outcome; public bool pirateFleeing, disabledOffered, fleeSpent, sankInBreach; public float breachT0 = -1; public float noviceZoneBonus; public bool aimAssist; /* breach aim assist (novice difficulty or first fights) */ public float lastShotT; public int hShots, hHits;
  /* adaptive pull: full strength for a captain who keeps missing, fading to assistFloor for one who already hits well */
  public float AssistScale() { if (hShots < 4) return 1f; float hr = hHits / (float)hShots; return Math.Max(C.assistFloor, Math.Min(1f, 1f - (hr - C.assistFadeFrom) / C.assistFadeSpan)); }
  public List<string> log = new List<string>();
  public event Action<CShip, CShip, int, int> OnBroadside; // shooter, target, shots, hits
  public static float D2R = (float)(Math.PI / 180);
  public PirateTactics PT;
  public Difficulty DF;
  public CombatCore(CombatCfg c, int seed, bool injured = false, int tier = 1, int diff = 1) {
    C = c; tier = Math.Min(tier, c.pirateTiers.Length - 1); DF = c.difficulties != null && c.difficulties.Length > 0 ? c.difficulties[Math.Min(diff, c.difficulties.Length - 1)] : new Difficulty();
    PT = c.pirateTiers[tier > 0 && DF.lateUsesEarlyTactics ? 0 : tier].Clone(); PT.damageMult *= DF.pirateDamageMult * (tier > 0 ? DF.lateDamageMult : 1f); aimAssist = DF.aimAssistAcc > 0; R = new Random(seed); H = new CShip(c.hasan, c); P = new CShip(c.pirate, c); H.injured = injured;
    windTo = (float)(R.NextDouble() * 360); float a = (float)(R.NextDouble() * 360), d = Lerp(c.startRangeMin, c.startRangeMax, (float)R.NextDouble());
    P.x = (float)Math.Sin(a * D2R) * d; P.z = (float)Math.Cos(a * D2R) * d; H.heading = (float)(R.NextDouble() * 360); P.heading = Norm(a + 180 + (float)(R.NextDouble() * 60 - 30));
    H.spd = SpeedOf(H) * 0.6f; P.spd = SpeedOf(P) * 0.6f;
  }
  public static float Lerp(float a, float b, float t) => a + (b - a) * t;
  public static float Norm(float a) { a %= 360; return a < 0 ? a + 360 : a; }
  public static float Diff(float a, float b) { float d = Norm(b - a); return d > 180 ? d - 360 : d; }
  public static float Bearing(float dx, float dz) => Norm((float)(Math.Atan2(dx, dz) / D2R));
  public float Range => (float)Math.Sqrt((P.x - H.x) * (P.x - H.x) + (P.z - H.z) * (P.z - H.z));
  public float WindMult(float heading) {
    float off = Math.Abs(Diff(heading, windTo)); var w = C.wind;
    return off < 30 ? w.running : off < 75 ? w.broadReach : off < 105 ? w.beam : off < 150 ? w.closeHauled : w.intoWind; }
  public float SpeedOf(CShip s) {
    float sailLoss = Math.Max(0, (float)Math.Floor((s.sails0 - s.sails) / C.sailDmgPerSpeedStep) * C.speedLossPerStep);
    return s.st.speed * C.speedScale * WindMult(s.heading) * (s.fullSail ? 1 : C.halfSailMult) * Math.Max(0.05f, 1 - sailLoss); }
  public float Upwindness(CShip s) => -(s.x * (float)Math.Sin(windTo * D2R) + s.z * (float)Math.Cos(windTo * D2R));
  public bool InArc(CShip a, CShip b) { float rel = Math.Abs(Diff(a.heading, Bearing(b.x - a.x, b.z - a.z))); return Math.Abs(rel - 90) <= C.arcDeg; }
  public bool CanFire(CShip a, CShip b) => a.reloadT <= 0 && a.gunHp > 0 && a.hull > 0 && InArc(a, b) && Dist(a, b) <= C.ammo[a.ammo].range && !(a.ammo == 3 && a.fireLeft <= 0);
  static float Dist(CShip a, CShip b) => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
  public static Part Match(int ammo) => ammo == 1 ? Part.Sails : ammo == 2 ? Part.Crew : Part.Hull;
  public float Accuracy(CShip a, CShip b) {
    var am = C.ammo[a.ammo]; float acc = C.baseAcc + (Upwindness(a) > Upwindness(b) ? C.upwindAccBonus : 0) + (a.aim == Match(a.ammo) || (a.ammo == 0 && a.aim == Part.Guns) ? C.matchAccBonus : 0);
    if (a == P) acc += PT.accBonus; if (a == H && b == P && aimAssist && a.ammo == 0 && a.aim == Part.Hull && BreachOpen) acc += DF.aimAssistAcc * AssistScale(); /* shot drifts toward the glowing breach */ float past = Dist(a, b) - am.range / 2; if (past > 0) acc -= past / 10 * C.accPenaltyPer10mPastHalf; return Math.Max(C.minAcc, Math.Min(0.98f, acc)); }
  public bool BoardAllowed(CShip a, CShip b) => Dist(a, b) <= C.boardRange && (SpeedOf(b) < SpeedOf(a) || b.Disabled(C)) && a.hull > 0;
  public float DuelZone() {
    float z = noviceZoneBonus + C.duelZoneBase + (C.hasanCombat - PT.captainCombat) * C.duelZonePerCombat + (C.hasanHasBlade ? C.bladeBonus : 0) + (P.crew < H.crew ? C.crewAdvantageBonus : 0); return z; }
  float ReloadTime(CShip s) { float r = s.st.reload * (1 + (1 - s.crew / s.crew0) * C.crewReloadPenalty); if (s.injured) r *= C.injuredReloadMult; return r; }

  public void Fire(CShip a, CShip b) {
    var am = C.ammo[a.ammo]; int shots = a.Guns(C); int hits = 0; float acc = Accuracy(a, b);
    for (int i = 0; i < shots; i++) if (R.NextDouble() < acc) { hits++;
      float k = C.damageScale * (a == P ? PT.damageMult : 1f) * (b == P && a.ammo == 0 && a.aim == Part.Hull && BreachOpen ? C.breachDamageMult * (aimAssist && a == H ? 1f + (DF.aimAssistDamageMult - 1f) * AssistScale() : 1f) : 1f); Func<Part, float> m = p => p == a.aim ? C.aimedPartMult : 1f;
      b.hull -= am.hull * k * m(Part.Hull); b.sails -= am.sails * k * m(Part.Sails); b.gunHp -= am.guns * k * m(Part.Guns); b.crew -= am.crew * k * m(Part.Crew); }
    if (a.ammo == 3) { a.fireLeft--; if (hits > 0) { b.burnT = C.fireBurnSecs; b.burnTick = 0; } a.ammo = 0; }
    b.sails = Math.Max(0, b.sails); b.gunHp = Math.Max(0, b.gunHp); b.crew = Math.Max(1, b.crew);
    if (b == P && pirateFleeing && a.ammo == 1 && hits > 0 && R.NextDouble() < C.chainStopChance) { pirateFleeing = false; fleeSpent = true; P.sails = Math.Min(P.sails, C.disableSails); } // chain to the sails stops a runner
    if (a == H && a.ammo == 0 && a.aim == Part.Hull) { hShots += shots; hHits += hits; } a.reloadT = ReloadTime(a); lastShotT = t; OnBroadside?.Invoke(a, b, shots, hits);
  }
  void Burn(CShip s, float dt) {
    if (s.burnT <= 0) return; s.burnT -= dt; s.hull -= C.fireBurnPerSec * dt; s.burnTick += dt;
    if (s.burnTick >= 1) { s.burnTick -= 1; int tick = (int)Math.Round(C.fireBurnSecs - s.burnT); if (tick >= C.fireDouseFromTick && R.NextDouble() < C.fireDouseChance) s.burnT = 0; } }

  void Move(CShip s, float dt) {
    float target = SpeedOf(s); if (s == P && pirateFleeing) target *= C.fleeSpeedMult; s.spd += (target - s.spd) * Math.Min(1, C.accel * dt);
    s.heading = Norm(s.heading + s.rudder * C.turnRate * dt * (0.4f + 0.6f * Math.Min(1, s.spd / (s.st.speed * C.speedScale))));
    s.x += (float)Math.Sin(s.heading * D2R) * s.spd * dt; s.z += (float)Math.Cos(s.heading * D2R) * s.spd * dt;
    if (s.reloadT > 0) s.reloadT -= dt; }

  public static float AngLerp(float a, float b, float t) => Norm(a + Diff(a, b) * t);
  public static float Side(CShip s, float br) { float d = Diff(s.heading, br); if (Math.Abs(d) < 165 && Math.Abs(d) > 15) s.side = d > 0 ? 90 : -90; return s.side; }
  public static float Steer(CShip s, float desired) => Math.Max(-1, Math.Min(1, Diff(s.heading, desired) / 30f));
  // choose a heading close to `want` that isn't pointed into the wind
  public float AvoidWind(float want) { float off = Diff(windTo + 180, want); if (Math.Abs(off) < 40) want = Norm(windTo + 180 + (off >= 0 ? 40 : -40)); return want; }

  /// waterline breach: under breachBelowHull the side facing Hasan glows for breachOpenSeconds every breachEverySeconds
  public bool BreachOpen => breachT0 >= 0 && (t - breachT0) % C.breachEverySeconds < C.breachOpenSeconds;
  static float Lost(CShip s) => (1 - Math.Max(0, s.hull) / s.hull0) + (1 - s.sails / s.sails0) + (1 - s.crew / s.crew0);
  /// toll only if Hasan has dealt more damage than he has taken
  public bool PirateLosing() => Lost(P) > Lost(H);
  float pThinkT;
  public void PirateAI() {
    var s = P; if (t < pThinkT) { if (CanFire(P, H)) Fire(P, H); return; } pThinkT = t + PT.reactionSeconds; float br = Bearing(H.x - P.x, H.z - P.z), rng = Range;
    if (!pirateFleeing && !fleeSpent && s.hull < C.pirate.hull * C.enemyFleeHull && SpeedOf(P) >= SpeedOf(H)) pirateFleeing = true;
    float want;
    if (pirateFleeing) want = AvoidWind(Norm(br + 180));
    else {
      bool upwind = Upwindness(P) > Upwindness(H) + 10;
      float side = Side(s, br); // keep a broadside to Hasan
      br = Norm(br + (float)((R.NextDouble() * 2 - 1) * PT.headingNoiseDeg));
      want = Norm(br - side);
      if (rng > 90) want = Norm(br - side * 0.4f); else if (rng < 40) want = Norm(br - side * 1.4f);
      if (PT.seeksUpwind && !upwind && rng > 60) want = AngLerp(want, Norm(windTo + 180), 0.3f); // try to work upwind
      if (P.crew < P.crew0 * PT.standoffBelowCrew && rng < C.ammo[2].range * 1.2f) want = Norm(br + 180 - side * 0.5f); // hurt crew: stand off out of grape range
      want = AvoidWind(want);
    }
    if (!pirateFleeing && PT.boardsWhenStronger && P.crew > H.crew * PT.boardCrewRatio && SpeedOf(P) > SpeedOf(H)) want = br;
    s.rudder = Steer(s, want); s.fullSail = true;
    s.ammo = PT.usesFire && s.fireLeft > 0 && rng <= C.ammo[3].range ? 3 : PT.usesChain && SpeedOf(H) > SpeedOf(P) && rng <= C.ammo[1].range ? 1 : PT.usesGrape && rng <= C.ammo[2].range * 0.8f ? 2 : 0; s.aim = s.ammo == 0 ? Part.Hull : Match(s.ammo);
    if (CanFire(P, H)) Fire(P, H);
  }

  /// One fixed step. Player (or bot) has already set H.rudder/fullSail/ammo/aim and may call Fire(H,P).
  public Outcome Step(float dt) {
    if (outcome != Outcome.None) return outcome; t += dt;
    PirateAI(); Move(H, dt); Move(P, dt); Burn(H, dt); Burn(P, dt);
    if (breachT0 < 0 && P.hull < P.hull0 * C.breachBelowHull) breachT0 = t;
    if (P.hull <= 0) { sankInBreach = BreachOpen; return outcome = Outcome.Sunk; }
    if (H.hull <= 0) return outcome = Outcome.HasanSunk;
    if (P.crew < P.crew0 * C.surrenderCrewFrac) return outcome = Outcome.Surrender;
    if (H.crew < H.crew0 * C.surrenderCrewFrac || (H.Disabled(C) && P.crew > H.crew)) return outcome = Outcome.HasanStruck; // same rule applies to Hasan's crew
    if (P.Disabled(C) && H.crew > P.crew && !disabledOffered) { disabledOffered = true; return outcome = Outcome.Disabled; }
    if (t > C.tollSeconds) return outcome = PirateLosing() ? Outcome.TollPaid : Outcome.HasanBoarded; /* behind on damage: last-ditch boarding, settled by a duel */ // losing pirate pays a toll and strikes
    if ((H.Disabled(C) || (PT.boardsWhenStronger && P.crew > H.crew * PT.boardCrewRatio)) && BoardAllowed(P, H)) return outcome = Outcome.HasanBoarded;
    if (pirateFleeing && (Range > C.escapeRange || t - lastShotT > C.fleeQuietSeconds)) { if (R.NextDouble() < C.fleeBase) return outcome = Outcome.Escaped; pirateFleeing = false; fleeSpent = true; } // escape fails: wind drops, she turns to fight
    if (t > C.maxFightSeconds) return outcome = Outcome.Timeout;
    return Outcome.None;
  }
  // average player duel model: per-round hit chance proportional to gold zone
  public bool SimDuel(bool hasanAttacking, float hitMult = 1f) {
    float p = Math.Min(0.95f, DuelZone() * C.avgPlayerHitPerZone * hitMult); if (!hasanAttacking) p *= 1 - C.pirateDuelHit * 0.5f;
    int w = 0, l = 0, need = C.duelRounds / 2 + 1; while (w < need && l < need) { if (R.NextDouble() < p) w++; else l++; } return w >= need; }

  // Sim bots for Hasan. Profiles live in settings ("bots"): good play and sloppy beginner.
  public bool wantBoard; float thinkT, canFireSince = -1; public int plan = -1; // 0 sink, 1 dismast (chain), 2 break crew (grape), 3 board
  public static readonly string[] PlanNames = { "sink", "dismast", "break crew", "board" };
  public void HasanBot(BotProfile b) {
    var s = H; float br = Bearing(P.x - H.x, P.z - H.z), rng = Range;
    if (plan < 0) { double r = R.NextDouble() * (b.planSink + b.planDismast + b.planCrew + b.planBoard); plan = (r -= b.planSink) < 0 ? 0 : (r -= b.planDismast) < 0 ? 1 : (r -= b.planCrew) < 0 ? 2 : 3; }
    if (t > b.planGiveUpSeconds && plan != 0) plan = 0; // fall back to sinking
    if (t >= thinkT) { thinkT = t + b.reactionSeconds;
      bool slower = SpeedOf(P) < SpeedOf(H) || P.Disabled(C);
      wantBoard = plan == 3 ? slower : (P.Disabled(C) && P.crew >= H.crew); // board crippled ships that won't strike
      float side = Side(s, br); float want = Norm(br - side); float close = plan == 2 ? 30 : 45;
      if (pirateFleeing && s.reloadT <= 0.5f && rng <= C.ammo[0].range) { } else if ((wantBoard && rng < 70) || (pirateFleeing && rng > 70)) want = br; else if (rng > 70) want = Norm(br - side * 0.45f); else if (rng < close * 0.8f && !wantBoard) want = Norm(br - side * 1.35f);
      if (plan == 2 && rng > 45 && !wantBoard) want = Norm(br - side * 0.6f);
      want = Norm(want + (float)((R.NextDouble() * 2 - 1) * b.headingNoiseDeg));
      s.rudder = Steer(s, b.avoidsWind ? AvoidWind(want) : want); s.fullSail = true;
      if (b.usesFire && plan == 0 && s.fireLeft > 0 && rng <= C.ammo[3].range && P.burnT <= 0) s.ammo = 3;
      else if ((plan == 1 || (plan == 3 && !slower) || pirateFleeing) && P.sails > C.disableSails && rng <= C.ammo[1].range) s.ammo = 1;
      else if ((plan == 2 || (plan == 3 && slower)) && rng <= C.ammo[2].range) s.ammo = 2; else s.ammo = 0;
      if (R.NextDouble() < b.wrongAmmoChance) s.ammo = R.Next(0, 3);
      s.aim = s.ammo == 0 ? Part.Hull : Match(s.ammo); if (R.NextDouble() < b.wrongAimChance) s.aim = (Part)R.Next(0, 4);
      if (!b.novice && BreachOpen && rng <= C.ammo[0].range && P.crew >= P.crew0 * C.surrenderCrewFrac + 1) { s.ammo = 0; s.aim = Part.Hull; } } // a good captain punishes the glowing breach
    if (CanFire(H, P)) { if (canFireSince < 0) canFireSince = t; if (t - canFireSince >= b.fireReactionSeconds) { Fire(H, P); canFireSince = -1; } } else canFireSince = -1;
  }
}
[Serializable] public class BotProfile { public string name; public float reactionSeconds, headingNoiseDeg, wrongAmmoChance, wrongAimChance, duelHitMult, fireReactionSeconds, boardDisabledChance, planSink, planDismast, planCrew, planBoard, planGiveUpSeconds; public bool usesFire, avoidsWind, novice; }

[Serializable] public class PirateTactics { public string name; public float reactionSeconds, headingNoiseDeg, accBonus, damageMult, captainCombat; public bool seeksUpwind, usesFire, usesChain, usesGrape, boardsWhenStronger; public float boardCrewRatio, standoffBelowCrew; public PirateTactics Clone() => (PirateTactics)MemberwiseClone(); }
[Serializable] public class Difficulty { public string name = "Captain"; public float aimAssistAcc, aimAssistDamageMult = 1, pirateDamageMult = 1, lateDamageMult = 1; public bool lateUsesEarlyTactics; }
