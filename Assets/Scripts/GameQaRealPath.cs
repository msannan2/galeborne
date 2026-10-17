using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

/// gamedev_encounter_fix: qa harness that sails full voyages through the player's own Voyage() coroutine
/// (roll at departure -> decision cards -> 45% encounter -> arrival -> dock), tapping each card's real buttons.
public partial class Game {
  IEnumerator QaRealPath(System.Action<bool, string> Chk, System.Action<string> L) {
    { tab = -1; dialog = null; Refresh(); yield return null; Save(); string rp_bakA = SavePath + ".qa_rp", rp_bakB = SavePathB + ".qa_rp"; if (File.Exists(SavePath)) File.Copy(SavePath, rp_bakA, true); if (File.Exists(SavePathB)) File.Copy(SavePathB, rp_bakB, true);
      float rp_sMin = S.sailSecondsMin, rp_sMax = S.sailSecondsMax, rp_ts0 = Time.timeScale; int rp_fr0 = Application.targetFrameRate; bool rp_cam0 = cam && cam.enabled, rp_firstRun0 = firstRun; int rp_wk0 = week;
      int rp_rolled = 0, rp_picked = 0, rp_shown = 0, rp_Weights = 0, rp_Rep = 0, rp_Wx = 0, rp_stuck = 0, rp_Pir = 0; string rp_prevShown = ""; int rp_lost = 0, rp_late = 0, rp_over90 = 0, rp_held = 0, rp_wxVoy = 0, rp_wxBad = 0, rp_wxEnc = 0; bool rp_wxNow = false, rp_owed = false; float rp_maxAt = 0; var rp_rolls = new System.Collections.Generic.List<float>();
      void rp_RpLog(string rp_c, string rp_st, LogType rp_t) { if (rp_c.StartsWith("GB VEVENTS weights") && !rp_c.Contains("qa synthetic")) { if (rp_owed) rp_lost++; rp_Weights++; rp_wxNow = false; rp_owed = rp_c.Contains("encountered True"); if (rp_owed) rp_rolled++; }
        else if (rp_c.StartsWith("GB VEVENTS weather card this voyage")) { if (!rp_wxNow) rp_wxVoy++; rp_wxNow = true; }
        else if (rp_c.StartsWith("GB ENCOUNTER at ")) { float rp_at = float.Parse(rp_c.Substring(16, 4), System.Globalization.CultureInfo.InvariantCulture); rp_maxAt = Mathf.Max(rp_maxAt, rp_at); if (rp_at > 0.905f) rp_over90++; if (rp_c.Contains("(late")) rp_held++; }
        else if (rp_c.StartsWith("GB ENCOUNTER LOST")) { rp_lost++; rp_owed = false; }
        else if (rp_c.StartsWith("GB ARRIVAL landmark") && rp_c.Contains("(late")) rp_late++; else if (rp_c.StartsWith("GB VEVENTS pick")) rp_picked++; else if (rp_c.StartsWith("GB ENCOUNTER show ")) { rp_shown++; string rp_id = rp_c.Substring(18).Trim(); rp_owed = false; if (rp_wxNow) { rp_wxEnc++; if (VeFamily(rp_id) == "weather") rp_wxBad++; } if (rp_id == "pirate") rp_Pir++; if (rp_id == rp_prevShown) rp_Rep++; if (rp_prevShown != "" && VeFamily(rp_prevShown) == "weather" && VeFamily(rp_id) == "weather") rp_Wx++; rp_prevShown = rp_id; } }
      Application.logMessageReceived += rp_RpLog; lastFamily = ""; lastEncFamily = ""; lastEncId = ""; ambushHalveNext = false;
      S.sailSecondsMin = S.sailSecondsMax = 1.2f; Time.timeScale = 6f; Application.targetFrameRate = -1; if (cam) cam.enabled = false; firstRun = false; storyAmbushDone = true; veForce = ""; veForceNext = ""; veForceHail = "";
      string[] rp_pref = { "Pay toll", "Pay nothing", "Bait", "Decline", "Hold your course", "Sail on", "Continue", "Noted", "Reef sails", "Leave it", "OK", "Close", "Collect", "Take" };
      const int rp_RN = 320; int rp_db = PortIdx("Debal"), rp_mu = PortIdx("Muscat"); port = rp_db;
      for (int rp_v = 0; rp_v < rp_RN; rp_v++) { week = rp_wk0; coins = 5000; System.Array.Clear(hold, 0, hold.Length); hold[G("copper")] = 6; hullNow = -1; sails = S.pirateFreeSails + 10; safeUntilSail = 0; harbourDebt = 0; tab = -1; dialog = null;
        int rp_to = port == rp_db ? rp_mu : rp_db; StartCoroutine(Voyage(rp_to)); yield return null; float rp_t0 = Time.realtimeSinceStartup;
        while (sailing && Time.realtimeSinceStartup - rp_t0 < 30f) { if (dialog != null) { var rp_bs = dialog.GetComponentsInChildren<UnityEngine.UI.Button>().Where(rp_b => rp_b.interactable).ToList(); string rp_Lb(UnityEngine.UI.Button rp_b) { var rp_tx = rp_b.GetComponentInChildren<UnityEngine.UI.Text>(); return rp_tx ? rp_tx.text : ""; }
            var rp_pick = rp_pref.Select(rp_pf => rp_bs.FirstOrDefault(rp_b => rp_Lb(rp_b).StartsWith(rp_pf))).FirstOrDefault(rp_b => rp_b != null) ?? rp_bs.LastOrDefault(); if (rp_pick != null) rp_pick.onClick.Invoke(); }
          yield return null; }
        if (sailing) { rp_stuck++; L($"real-path voyage {rp_v} stuck (dialog {(dialog ? dialog.name : "none")})"); break; } if (rp_rolls.Count < rp_Weights) rp_rolls.Add(veLastRoll); }
      if (rp_owed) rp_lost++; Application.logMessageReceived -= rp_RpLog;
      { /* gamedev_encounter_fix2: the 90% hold, through the real Voyage loop: a forced encounter with the skip-ad flag set from the first frame (the ship would jump straight to arrival) must still show, at 0.90 */
        string ef2_at = ""; bool ef2_lostL = false; int ef2_show = 0; void ef2_Log(string ef2_c, string ef2_st, LogType ef2_t) { if (ef2_c.StartsWith("GB ENCOUNTER at ")) ef2_at = ef2_c; else if (ef2_c.StartsWith("GB ENCOUNTER LOST")) ef2_lostL = true; else if (ef2_c.StartsWith("GB ENCOUNTER show ")) ef2_show++; }
        Application.logMessageReceived += ef2_Log; week = rp_wk0; coins = 5000; hullNow = -1; tab = -1; dialog = null; lastEncId = ""; lastFamily = ""; veForceNext = "rumor"; int ef2_to = port == rp_db ? rp_mu : rp_db;
        StartCoroutine(Voyage(ef2_to)); skipVoyage = true; float ef2_t0 = Time.realtimeSinceStartup;
        while (sailing && Time.realtimeSinceStartup - ef2_t0 < 30f) { if (dialog != null) { var ef2_bs = dialog.GetComponentsInChildren<UnityEngine.UI.Button>().Where(ef2_b => ef2_b.interactable).ToList(); var ef2_p = ef2_bs.FirstOrDefault(ef2_b => { var ef2_x = ef2_b.GetComponentInChildren<UnityEngine.UI.Text>(); return ef2_x && rp_pref.Any(ef2_f => ef2_x.text.StartsWith(ef2_f)); }) ?? ef2_bs.LastOrDefault(); if (ef2_p != null) ef2_p.onClick.Invoke(); } yield return null; }
        Application.logMessageReceived -= ef2_Log; bool ef2_docked = !sailing; skipVoyage = false;
        float ef2_a = VeAdvance(0.3f, 2f, 1.2f, true), ef2_b2 = VeAdvance(0.3f, 2f, 1.2f, false), ef2_c2 = VeAdvance(0.2f, 0.1f, 1.2f, true);
        Chk(ef2_at.StartsWith("GB ENCOUNTER at 0.90") && ef2_show == 1 && !ef2_lostL && ef2_docked && Mathf.Abs(ef2_a - 1.08f) < 1e-4f && Mathf.Abs(ef2_b2 - 2.3f) < 1e-4f && Mathf.Abs(ef2_c2 - 0.3f) < 1e-4f,
          $"gamedev_encounter_fix2: skip-ad from the first frame with an encounter owed: '{ef2_at}', shown {ef2_show}, lost {ef2_lostL}, docked {ef2_docked}; clock step 0.3+2.0 of 1.2 s holds at {ef2_a:0.00} when owed ({ef2_b2:0.00} when not), small steps unchanged ({ef2_c2:0.00})"); } S.sailSecondsMin = rp_sMin; S.sailSecondsMax = rp_sMax; Time.timeScale = rp_ts0; Application.targetFrameRate = rp_fr0; if (cam) cam.enabled = rp_cam0; firstRun = rp_firstRun0;
      if (File.Exists(rp_bakA)) File.Copy(rp_bakA, SavePath, true); if (File.Exists(rp_bakB)) File.Copy(rp_bakB, SavePathB, true); Load(); tab = -1; dialog = null; Refresh(); yield return null;
      float rp_rate = rp_shown / (float)rp_RN, rp_sd = Mathf.Sqrt(0.25f / rp_RN), rp_mean = rp_rolls.Count > 0 ? rp_rolls.Average() : 0, rp_lowFrac = rp_rolls.Count > 0 ? rp_rolls.Count(rp_x => rp_x < 0.5f) / (float)rp_rolls.Count : 0;
      Chk(rp_stuck == 0 && rp_Weights == rp_RN && Mathf.Abs(rp_rate - 0.5f) <= 4 * rp_sd && rp_picked == rp_rolled && rp_shown == rp_rolled && rp_lost == 0 && rp_over90 == 0, $"gamedev_encounter_fix: REAL voyage path, {rp_RN} full voyages: {rp_shown} encounters shown ({rp_rate:P1}, want 50% +/- {4 * rp_sd:P1}); rolled True {rp_rolled}, picked {rp_picked}, shown {rp_shown} ({rp_lost} lost after the roll; latest shown at {rp_maxAt:0.00} of the voyage, {rp_over90} after 90%; {rp_held} held past the 45% window by a card or long frame, arrivals marked late {rp_late}); roll mean {rp_mean:0.000}, {rp_lowFrac:P1} of rolls < 0.5; {rp_Pir} pirates; stuck {rp_stuck}");
      Chk(rp_wxBad == 0 && rp_wxEnc > 0, $"gamedev_encounter_fix2: REAL voyage path: {rp_wxVoy} voyages showed a weather decision card, {rp_wxEnc} of them then had an encounter, {rp_wxBad} of those were squall/squallLine (want 0)");
      Chk(rp_Rep == 0 && rp_Wx == 0, $"gamedev_encounter_fix: REAL voyage path: {rp_Rep} immediate repeats of an encounter id, {rp_Wx} squall/squallLine back to back over {rp_shown} encounters"); }
  }
}
