# Code structure

All gameplay code is in `Assets/Scripts` (assembly **Hooker.Runtime**). All editor tools are in `Assets/Editor` (assembly **Hooker.Editor**, editor only, depends on Hooker.Runtime).

## Assets/Scripts

| Folder | What lives there |
|---|---|
| Core | Game flow and global systems: GameManager, GameInput, RunOrder, ScreenFade, PauseEverywhere, GameFeel (hit-stop, screen shake), DevSettings/DevTools |
| Camera | CameraFollow2D, BackdropFollow, ArenaBackdrop |
| Player | Movement, combat, art, fall damage, respawn, player balance |
| Player/Abilities | Boss-earned abilities: PlayerAbility (spikes), PlayerChain + ChainHold, PlayerUpgrades, AbilityArt |
| Combat | Health and everything that deals damage: ContactDamage, DamageZone, KillZone, Projectile, HealthPickup, IChainStunnable |
| Combat/StatusEffects | Burn, Poison, Soaked |
| Enemies | EnemyChase, EnemyShooter, ExplodingBarrel, GooTendril |
| Bosses | BossTurns, BossGroupGate, and one folder per boss (HardyBoss, PoisonBoss, OctopusBoss, FatherBoss) |
| Hazards | ChainTrap; Hazards/Poison: puddles, drippers, drops, pendulums, PoisonArt |
| World | Level pieces: checkpoints, doors, exits, platforms, hook points, breakables, Revisit |
| Sailing | Boat scenes: BoatDrive, BoatExit, BoatBob, SailMenu, SailMonologue |
| Items | Inventory, ItemSettings, ItemPickup, Chest, PlayerItems |
| Companion | Companion (Ariel), CompanionSettings, CompanionTalk, JailCell |
| Dialogue | DialogueManager, triggers, SceneDialogue, DefeatDialogue, TutorialSign |
| UI | Menus, health bars, and **UiKit** (shared Panel/Fill/Label/WhiteSprite for code-built UI) |
| Story | AfterBossScene, EndingGrave |
| Visuals | Ambient effects: ocean layers, water surface/drips, torch flicker, sprite loops |

## Assets/Editor

Folders follow the **Tools** menu: `Builders` (Rebuild Levels, tutorial, menus, ending), `Art` (Tools > Art), `Decorators`, `Balance`, `Tools` (dev tools, items, companion, story, setup), and `Shared` (**LevelKit** and **EditorKit**, helpers used by many tools).

## Partial classes

Big classes are split into `Class.Part.cs` files in the same folder. The main `Class.cs` always holds the Inspector fields, lifecycle methods (Awake/Start/Update), the main loop and reset/death logic; the part files hold groups of related methods (for bosses: `.Attacks` plus helpers).

## Conventions for new code

- Put a new script in the folder of the feature it belongs to; move it together with its `.meta` (or move it inside Unity) so references survive.
- For UI built in code, use `using static UiKit;` instead of copying Panel/Fill/Label again.
- Editor helpers used by more than one tool go in `Editor/Shared` (EditorKit or LevelKit).
- When a class passes roughly 500 lines, split a cohesive group of methods into a partial file. Keep serialized fields in the main file.
