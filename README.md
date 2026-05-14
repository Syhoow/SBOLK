# SBOLK
A top down shooter game

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
