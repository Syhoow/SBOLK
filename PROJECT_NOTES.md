# SBOLK Project Notes

## Current Focus
- Weapon system and gun visuals
- HUD for weapon and ammo
- Weapon-specific node setup in gun scene

## Controls
- Move: WASD or Arrow Keys
- Shoot: Left Mouse Button
- Reload: R
- Switch weapon: 1 (Pistol), 2 (Smg), 3 (Rifle), 4 (Sniper), 5 (Shotgun)

## Weapon Script Architecture
- Main weapon logic: Scripts/Weapons/Gun.cs
- Bullet logic: Scripts/Weapons/Bullet.cs
- Weapon types enum: Scripts/Weapons/WeaponType.cs
- Weapon stat model: Scripts/Weapons/WeaponStats.cs
- HUD logic: Scripts/UI/WeaponHud.cs

## Scene Wiring
- Main scene: Scenes/main.tscn
  - Contains Player, Enemy, and HUD
- Gun scene: Scenes/pistol.tscn
  - Root node has gun script
  - Contains per-weapon child nodes for independent orientation tuning
- Bullet scene: Scenes/bullet.tscn

## Texture Mapping
- Pistol: Assets/pistol.png
- Rifle: Assets/rifle.png
- Sniper: Assets/sniper.png
- Shotgun: Assets/shotgun.png
- Smg: temporary fallback to rifle texture until smg texture is added

## Tuning Workflow
1. Open Scenes/pistol.tscn
2. Select a weapon node (PistolNode, SmgNode, RifleNode, SniperNode, ShotgunNode)
3. Adjust Sprite2D position/scale/rotation for hold look
4. Adjust Marker2D position to the barrel tip
5. Run game and test keys 1 to 5

## Git Safety Workflow
1. Check current state
   - git status
2. Create your feature branch
   - git checkout -b feature/gun-visual-pass
3. Commit often
   - git add .
   - git commit -m "Update gun visuals and tuning"
4. Push branch
   - git push -u origin feature/gun-visual-pass

## Pre-PR Checklist
- Game runs without errors
- All weapon keys switch correctly
- Muzzle position is correct per weapon
- HUD updates weapon and ammo correctly
- No unintended file changes in git status

## Next Suggested Tasks
- Add dedicated smg texture when available
- Add per-weapon recoil and muzzle flash
- Add per-weapon damage and enemy health integration
