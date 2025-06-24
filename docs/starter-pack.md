# TABG Starter Pack — Setup & Configuration Guide

## 🚨 **IMPORTANT – READ THIS FIRST** 🚨

1. **Always press Save** after every change; the GUI does **not** auto-save.  
2. **Loadouts restriction:** except for ammo, add **only one** weapon, blessing, or other item per slot—adding more overwrites the existing item. (Just add the blessings 3x1)  
3. To reopen the setup tool later, open your server folder and double-click **`StarterPackSetup.exe`**.  
   After you close the config window, the server GUI will start automatically.

---

## What Is the Starter Pack?

An all-in-one quality-of-life mod for Totally Accurate Battlegrounds servers by **[@ContagiouslyStupid](https://github.com/ContagiouslyStupid)**.  That is auto installed 
Use the GUI (`StarterPackSetup.exe`) or the JSON file it generates to fine-tune every setting below.

---

## Feature Overview

| Category | What You Can Change | Default / Notes |
|----------|--------------------|-----------------|
| **Ring** | • Locations • Sizes • *Speed is still set inside `game_settings.txt` or the in-game server UI* | Vanilla values |
| **Downed State** | Enable/disable bleed-out (disabled ⇒ instant death) | Enabled |
| **Respawn Lockout** | Allow players back in after lockout | Enabled |
| **Truck Elimination** | Kill players ejected from trucks (forces early zoning) | Disabled |
| **Loot Drop on Death** | Prevent corpses from dropping gear to reduce lag | Disabled |
| **On-Kill Rewards** | Auto-grant ammo and/or meds on every kill | Off |
| **Respawn Loadouts** | Balanced kits given on respawn (edit in GUI) | Empty |
| **Lobby Timer** | How long the lobby stays open (⚠ high values can destabilise servers) | 15 min |
| **Lobby Spawn Points** | Toggle vanilla spots or set a custom position | Vanilla |
| **Vote-to-Start** | `/votestart` player count, vote %, countdown length | 4 players / 60 % / 30 s |
| **Match Timer** | Hard match limit (do not set too high) | 15 min |
| **Win Conditions** | *Default* • *Kills To Win* • *Debug* | Default |
| **Heal-on-Kill** | Restore % HP on every kill | Off |
| **Spell/Blessing Airdrops** | Interval & start offset for airdrops | On / 180 s |

---

## Editing Tips

* **Hover tooltips** inside the GUI explain most fields.  
* Blank values revert to TABG's native defaults.  
* For ring editing, use TABG's spectator camera to grab coordinates quickly.  
* If something behaves strangely, re-open the GUI and verify that your last change was saved.

---

## Troubleshooting

| Problem | Likely Cause | Fix |
|---------|--------------|-----|
| Changes don't apply | Forgot to press **Save** | Re-open GUI, adjust, press Save |
| Extra blessings disappear | More than one non-ammo item in same Loadout slot | Only one per slot |
| Ring too slow/fast | Ring speed still set elsewhere | Edit `game_settings.txt` or in-game UI |

--- 