# Galeborne – Monetization Pricing v1 (researched 2026-10-14)

Prices in USD (US storefront) as found via web search on 2026-10-14. Store prices change; re-check before launch.

## 1. Benchmarks found
| Game / source | Model | Price(s) found | Source |
|---|---|---|---|
| Pirates Outlaws (pirate roguelike) | Paid + IAP | $0.99 on Play; iOS IAPs $1.99–$8.99 (Hero $1.99/$4.99, Booster $2.99, Packages $1.99, gold $1.99–$8.99) | https://play.google.com/store/apps/details?id=com.piratesoutlaws.fabledgame&hl=en · https://apps.apple.com/us/app/pirates-outlaws/id1442776789 |
| Stardew Valley (premium ref) | Premium, no IAP | $4.99 | https://play.google.com/store/apps/details?id=com.chucklefish.stardewvalley&hl=en_US |
| Kingdom Two Crowns | Premium | $6.99 (Play, per search snippet / App Pricing Lab) | https://play.google.com/store/apps/details?hl=en&id=com.rawfury.kingdom2crowns · https://apppricinglab.com/compare/apple/1406710800/1477991646 |
| Slay the Spire (mobile) | Premium | $9.99 (on sale $6.99) | https://play.google.com/store/apps/details/Slay_the_Spire?id=com.humble.SlayTheSpire&hl=en_NZ |
| Uncharted Waters Origin | F2P + IAP | Search snippet shows $4.99 item; a user review says packages start ~$40 (**unverified / anecdotal**) | https://play.google.com/store/apps/details?id=com.linegames.uwogl&hl=en |
| Port Royale 4 | PC only (no Android) | $39.99 / Extended $44.99 – not a mobile comp | https://store.steampowered.com/app/1024650 |
| Brain Test series (UNICO) | F2P + ads | Remove Ads $4.99 (x3 titles) | https://apppricinglab.com/app/apple/1486214495 |
| AppFollow review analysis | – | Players accept $0.99–$4.99 for remove-ads; >$5 draws complaints; bundle with a bonus | https://appfollow.io/blog/what-mobile-game-players-want-monetization-insights-from-app-store-reviews |
| MonetizationGuy | – | Argues remove-ads $4.99–$7.99 to exceed ad LTV (casual, interstitial-heavy) | https://monetizationguy.com/articles/hybrid-monetization-ads-iap-subscription-decision |
| Battle/season pass benchmarks | – | Most passes $5–$10/season, $9.99 typical, $4.99 viable entry; PUBG Mobile base ~$4.99 | https://www.guardingpearsoftware.com/blog/game-monetization-benchmarks-2026-skin-battle-pass-and-45384 · https://mwm.ai/glossary/battle-pass |
| Rewarded-ad caps | – | Suggested start: 3/session, 8/day, 60–90 s cooldown; mid/hardcore 3–5/session | https://audiencelab.ai/blog/rewarded-video-ads-optimization |
| Unity LevelPlay case (Hempire) | – | Raising one placement cap 10→20/day lifted ARPDAU 37% (placement-specific) | https://unity.com/resources/lbc-studios |

Not found / unverified: Seafarer-type trading games' IAP lists; Slay the Spire has no mobile DLC (it is premium with no IAP), so no mobile DLC price point found; a Pirates Outlaws Play IAP breakdown (only iOS shown).

## 2. Google Play points
- Pricing templates retired (since 27 Oct 2025); set price per product or via the API. Play converts a base USD price per region, with regional min/max limits and sub-dollar pricing in many markets → set USD base and review localized prices for PK/IN/BR/ID etc. https://support.google.com/googleplay/android-developer/answer/6334373
- Better Ads Experiences: no unexpected full-screen interstitials (e.g. at game start / before splash); rewarded ads the user opts into are exempt. https://support.google.com/googleplay/android-developer/answer/12271244 · https://support.google.com/googleplay/android-developer/answer/9857753
- AdMob rewarded policy: state the reward before the ad, user can dismiss, always grant the reward on completion. https://support.google.com/admob/answer/7313578
- Families policy applies only if kids are a target audience; keep target age 13+ to avoid it.

## 3. Recommended Galeborne price points
| Item | Price (USD base) | Reasoning |
|---|---|---|
| Remove Ads | **$4.99**, bundled with an exclusive "Sailmaker's" flag/sail cosmetic | Common benchmark; ≤$5 avoids complaints; bonus makes it feel like support. Since our ads are opt-in only, Remove Ads = auto-grant time-saver rewards (or skip-ad), not just hiding banners. |
| Cosmetic – small (flag, single sail) | **$0.99–$1.99** | Matches the low end of Pirates Outlaws' IAPs; impulse tier. |
| Cosmetic – ship skin | **$2.99** | Mid tier, matches common $2.99 SKUs. |
| Cosmetic – full set (hull + sails + flag) | **$4.99** | Bundle discount vs. buying the pieces. |
| Content pack (new region + story chapter) | **$4.99** (launch-bundle of all packs ~$9.99) | Equal to Stardew's whole-game price; a meaningful region at $4.99 feels fair (AppFollow); premium-feel comps sit at $4.99–$9.99. |
| Captain's Charter (cosmetic-only, ~8-week season) | **$4.99** (optional $9.99 deluxe with tier skips) | Lower end of $5–$10 pass range suits a single-player, non-competitive game. |
| Rewarded-ad caps | **Time-saver 1× per port visit; max 3 offers/session, 8/day, 90 s cooldown** | Audiencelab starting point; port-visit cap is already in our design; A/B test before raising. |

Next step: confirm localized prices in Play Console for key emerging markets (Play auto-converts; sanity-check that $4.99 doesn't round awkwardly).
