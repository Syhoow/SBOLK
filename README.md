# SBOLK
SBOLK
A top-down 2D arena shooter built in Godot 4.4 with C#. Players fight escalating waves of enemies, collect drops, upgrade through a between-wave shop, and compete on a Firebase-backed global leaderboard.

## Weapon System
- Weapon scripts are organized in `Scripts/Weapons`.
- Supported weapon types: Pistol, Smg, Rifle, Sniper, Shotgun.
- Shared weapon stats and behavior are defined in code for fast balancing.
- Adjust weapon size on the gun root node itself, not on the `Sprite2D` child.

## Weapon Visual Tuning
- Open `Scenes/pistol.tscn`.
- Select the root `gun` node to change overall weapon size and orientation.
- Use the per-weapon child nodes for hold position, barrel placement, and sprite-specific offsets.

## Controls
- Move: WASD or Arrow Keys
- Shoot: Left Mouse Button
- Reload: `R`
- Switch weapons: `1` (Pistol), `2` (Smg), `3` (Rifle), `4` (Sniper), `5` (Shotgun)

**Enemy types (5)**
Basic Direct charge toward player at constant speed.
Dash Windup pause → explosive dash at 6000 px/s → brief post-dash stall.
Teleport Pauses then teleports behind the player every ~4 s.
Gun Maintains preferred distance (200 px), strafes sideways, shoots every 5 s.
Bomb Rushes player at 1.2× speed, explodes on contact (3000 knockback), self-destructs.

**Wave / game loop**
Title screen → Auth / login → Arena wave → Goal reached → Portal spawns → Shop (endrun) → Next wave ×1.5
Each wave has a WaveGoal (kills/drops). When reached, a portal spawns at a random arena position. Entering the portal scales enemy HP by ×1.25 and damage by ×1.5, increments the DifficultyMultiplier by ×1.5, and opens the between-wave shop. 
Healing pots spawn outside the arena on a 15-second timer (max 2 at a time).

**Shop & cosmetics**
In-run shop Between waves the player can spend gold earned from kills to buy upgrades. Items are managed by Endrun.cs and rendered into a grid of ShopItem nodes.

**Cosmetic shop**
12 hat cosmetics (Crown, Cap, Wizard, hat_1–hat_9) at prices 50–150 coins. Offers rotate every hour. Data syncs to Firebase per user. Managed by the CosmeticManager autoload.

**Firebase integration**
Auth + Realtime Database
Uses the godot-firebase addon. Players log in via email or fall back to anonymous auth. Scores are submitted only when the new score beats the player's existing best (checked before writing). Leaderboard fetches the top 10 from leaderboards/global. Cosmetic data (owned counts, equipped hat, coins) is persisted per UID with a local cache fallback.

**Visual effects & shaders**
SBOLKBackground scrolling arena bg
glitch screen glitch effect
screen full-screen post-process
swirl portal/vortex distortion

Impact frames use an impactframe.tres shader material. A monogram pixel font family (regular, extended, extended italic) carries all in-game UI text. HDR 2D and transparent viewport are enabled for compositing flexibility.
