using System;
using UnityEngine;

[Serializable] public class GoodDef { public string id, name; public float min, max; public bool contraband; }
[Serializable] public class PortDef { public string name, art; public string[] cheap, want; }
[Serializable] public class Weights { public float pirate, wreck, merchant, squall, rumor; }
[Serializable] public class ContrabandCfg { public int bannedPortsMin, bannedPortsMax, banMoonsMin, banMoonsMax, announceWeeksAhead; public float bannedSellMult, bannedPerUnitSellDrop = 0.01f; }
[Serializable] public class CustomsCfg { public float baseRisk, fakersCrate, templeTithe, floor, finePct; public int repLoss, fakersCratePrice, titheCost; }
[Serializable] public class WarehouseCfg { public int leasePerWeek, leaseSlots, buyPrice, buySlots, decayAfterMoons; public float decayPerMoon; }

[Serializable] public class AmbushCfg { public float perThousandCargo, perThousandCoinsAboard, contrabandBonus, fameBonusPerLevel, riskyRouteMult, cap, baitMinHoldValuePct, intimidateFleeAtRatio, intimidateScaleFromRatio; public string[] types; public bool noRepeatInARow, namedAndDesperateNeverFlee, fledPiratesDropLoot, bluffUsesCunning; }
[Serializable] public class MarketCfg { public int goodsPerPortMin, goodsPerPortMax, eventsPerPortPerMoon, stockRefillWeeks; public float shortageMultMin, shortageMultMax, glutMultMax, festivalLuxuryMult, foreignShipChance; public bool alwaysListCarriedGoods, contrabandOnlyInBackAlley, pricesVisibleOnMap, rumorsAreVague; }
[Serializable] public class BankCfg { public float interestPerMoon, otherPortWithdrawFee; public bool safeFromSinkAndRobbery; }
[Serializable] public class LenderCfg { public int firstLoanCap, maxLoanCap, termMoons; public float startRatePerMoon; public bool onTimeRaisesCap, onTimeLowersRate, lateSeizesWarehouseFirst, lateSendsDebtCollector; }
[Serializable] public class PortSpecialty { public string port, kind; }
[Serializable] public class ShipwrightCfg { public float repairPerHullPoint, adRepairDiscount, resalePct; public int adRepairPerPortVisit, upgradeTiers, tier3DockWeeksMin, tier3DockWeeksMax; public string[] upgradeKinds; public PortSpecialty[] specialtyList;
  public string specialty(string port) { if (specialtyList == null) return null; foreach (var s in specialtyList) if (s.port == port) return s.kind; return null; } }
[Serializable] public class AdsCfg { public int maxPerPortVisit, maxPerSession, maxPerDay, minSecondsApart; public bool interstitials, removeAdsGrantsRewards, capsApplyToRemoveAdsBuyers; }

/// <summary>Every tunable number lives in Resources/galeborne_settings.json.</summary>
[Serializable] public class GameSettings {
  public int startCoins, holdSize; public string captain;
  public float perUnitSellDrop, perUnitBuyRise, weeklyDriftMax, pullToBase, cheapMult, wantMult;
  public int weeksPerMoon, moonsPerYear; public float sailSecondsMin, sailSecondsMax, encounterChance; public bool debugFast; public float debugFastMultiplier; public int pirateFreeSails;
  public float pirateTollPct, pirateWinChance, rumorSpike, sellSpread; public CombatCfg combat;
  public Weights weights; public ContrabandCfg contraband; public CustomsCfg customs; public WarehouseCfg warehouse;
  public GoodDef[] goods; public PortDef[] ports;
  public AmbushCfg ambush; public MarketCfg market; public BankCfg bank; public LenderCfg moneyLender; public ShipwrightCfg shipwright; public AdsCfg ads; public string[] adPlacements; public float freeFastForwardMult; public bool prologueDuelUnlosable; public int forgivingDuelsFirstN;
  public static GameSettings Load() => JsonUtility.FromJson<GameSettings>(Resources.Load<TextAsset>("galeborne_settings").text);
}

/// <summary>Every UI color, font and panel sprite lives in Resources/galeborne_theme.json. Reskin here.</summary>
[Serializable] public class UITheme {
  public string fontRegular, fontBold, panelSprite; public int fontSize, fontSizeSmall, fontSizeTitle, borderPx;
  public string panel, panelAlt, rowShade, parchment, ink, text, textMuted, brass, gold, down, up, button, buttonText, disabled;
  public string seaShallow, seaDeep, sun, skyHorizon, skyZenith;
  public string ledgerBg, ledgerRowAlt, ledgerInk, ledgerMuted, ledgerRule, ledgerButton, ledgerButtonText, ledgerUp, ledgerDown, stamp, frame;
  public string dhowTeak, dhowIndigo, dhowTurquoise, dhowCream, dhowBrass, pirateHull, pirateBand, pirateSail, pirateStripe, pirateBrass, marker;
  [NonSerialized] public Font regular, bold;
  public static UITheme Load() {
    var t = JsonUtility.FromJson<UITheme>(Resources.Load<TextAsset>("galeborne_theme").text);
    t.regular = Resources.Load<Font>(t.fontRegular); t.bold = Resources.Load<Font>(t.fontBold);
    if (t.regular == null) t.regular = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    if (t.bold == null) t.bold = t.regular; return t; }
  public static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
}
