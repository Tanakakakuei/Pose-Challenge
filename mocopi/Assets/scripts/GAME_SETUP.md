# mocopi Alphabet Wall Game Setup

## GameMode UI and round flow (2026-10-08)

The current local flow uses `MocopiLocalGameLoopController` with `GameModePresentation`.
`Tools > mocopi > Apply GameMode UI and Loop Setup` builds the `UIPanels` hierarchy in the
open GameMode scene. It preserves the original prototype panels as inactive objects,
wires all state labels/buttons, and replaces prefab-asset references with real scene
instances for the second camera and wall manager. The setup is undoable.

- Menus use white touch panels, a short hold indicator, smooth visual feedback, and
  mouse clicks. Real mocopi input accepts hand colliders only. A hand already overlapping
  a newly opened menu must leave before another selection is accepted.
- Touch feedback scales the canvas, never the collider. Hiding a panel clears held
  contacts, including colliders that are disabled without an exit callback.
- The menu camera moves closer; countdown and gameplay restore the original view.
  Score, resolved-wall progress, and clear/miss feedback appear in the HUD.
- The scene's debug mode supports playing without mocopi. Confirm the avatar, then
  click Start. With mocopi, hold the overhead ready pose or touch each player's ready button.
- Countdown shows `5 → 4 → 3 → 2 → 1`, one second each in real time, followed by
  `スタート！` for 0.75 seconds before walls begin moving. It resets on retry and is
  cancelled when leaving the countdown. Both the HUD Text and legacy CountDown_UI
  touch-panel prefab are supported, including scenes with no assigned countdown Text.
- Retry clears the previous round and restores time scale. Returning to selection also
  removes walls and detached debris. Both mocopi avatars stay active in connection wait.
- Default pacing after applying the setup: 10 walls, 3 m/s, 4.2 seconds between walls.

When an explicit `Temp/GameModeUIUpgrade.request` is present, the editor applies the
setup once after script refresh, while GameMode is open and not playing. It first saves
the live scene, including unsaved edits, as `Assets/Editor/GameModeBeforeUI-*.unity`, then
saves the upgraded original scene. Completion is recorded in `Logs/game-ui-live-apply.txt`.
Without that request file, imports do not automatically modify scenes.

`GameModeValidation.PlayTest` runs actual Unity Play mode in the validation copy. It
checks touch lifecycle, 10-wall solo and retry rounds, co-op, versus, paused retry,
menu return, and detached debris cleanup. Its wall speeds are accelerated only in memory.
Hardware mocopi gestures still need a hands-on check.

This project contains a mocopi-driven wall avoidance game prototype.

The player uses mocopi to pose like an uppercase alphabet cut-out. A wall made from small cubes moves toward the player. Cubes that touch the player fall, and too many contacts ends the game.

## Main Scripts

- `WallBuilder.cs`: builds a rectangular wall grid from small cube triggers.
- `MaskPatternGenerator.cs`: creates A-Z alphabet cut-out masks.
- `WallMaskData.cs`: stores and applies the mask to a wall instance.
- `WallManager.cs`: spawns walls, moves them, cycles letters, scores clears, and ends the game.
- `MocopiPlayerColliderRig.cs`: adds body colliders to a mocopi-driven humanoid avatar.
- `MocopiGameStateController.cs`: switches the single-scene flow between mocopi connection wait, avatar selection, ready, gameplay, and result.
- `GameManager.cs`: updates UI and handles restart/game-over flow.

## Scene Setup

1. Create a wall prefab.
   - Create an empty GameObject named `AlphabetWall`.
   - Add `WallBuilder`.
   - Set `Rows = 52`, `Columns = 42`, `Cube Size = 0.05`.
   - Click `Build Wall Grid` in the Inspector.
   - Save it as a prefab.
   - The prefab should have `WallMaskData`, `WallCube` children, trigger `BoxCollider`s, and kinematic `Rigidbody`s.

2. Create the wall spawner.
   - Create an empty GameObject named `WallManager`.
   - Add `WallManager`.
   - Assign the `AlphabetWall` prefab to `Wall Prefab`.
   - Set `Spawn Point` to a Transform in front of the player.
   - Adjust `Move Speed`, `Spawn Interval`, and `Destroy Distance`.
   - Disable `Randomize Letters` to play A, B, C...Z in order. Enable it for random letters.
   - To cut the wall from the live mocopi pose instead of a letter, enable `Use Mocopi Pose Mask`.
   - Assign the mocopi avatar `Animator` to `Pose Mask Animator`, or leave it empty and let `WallManager` find the avatar with `MocopiPlayerColliderRig`.
   - Leave `Pose Mask Projection` empty to project the avatar into the spawned wall plane. Assign a separate Transform only if you want a fixed capture plane.
   - To keep a generated cut-out, select the spawned wall or wall prefab's `WallMaskData`, set `Mask Save File Name`, then click `Save Current Mask Data`.
   - Click `Load Saved Mask Data` on a same-sized wall grid to reuse that saved mask. Relative save names are written under `Application.persistentDataPath`.
   - The `WallMaskData` Inspector also lists saved `*.json` masks from the same save folder with a wall preview. Blue cells are solid cubes, black cells are cut-out holes. Use `Load` to apply one to the selected wall, or `Copy Path` to copy its full path.

3. Prepare the mocopi avatar.
   - Use the existing `MocopiSimpleReceiver` / `MocopiAvatar` setup from the receiver sample.
   - Add `MocopiPlayerColliderRig` to the same GameObject as the humanoid `Animator` and `MocopiAvatar`.
   - Create a Unity layer named `Player` if it does not exist. The rig uses it automatically.
   - Press Play and confirm the generated body colliders follow the mocopi avatar.

4. Add UI.
   - Create a Canvas with optional `Text` objects for score, hits, next letter, and game over.
   - Create an empty GameObject named `GameManager`.
   - Add `GameManager`.
   - Assign `WallManager` and the UI Text references.

5. Add the single-scene state flow.
   - Create one UI root object per screen: `MocopiConnectionWaitPanel`, `AvatarSelectionPanel`, `ReadyPanel`, `GameStartPanel`, and `ResultPanel`.
   - Under `MocopiConnectionWaitPanel`, create `MocopiConnectPromptPanel` with text like `Please connect mocopi`.
   - Under `MocopiConnectionWaitPanel`, create `MocopiConnectCompletePanel` with text like `Mocopi connected`.
   - Create an empty GameObject named `MocopiGameStateController`.
   - Add `MocopiGameStateController`.
   - Assign the five screen panels, the two mocopi connection sub-panels, `WallManager`, and any optional title, message, selected avatar, and result `Text` references.
   - On `WallManager`, disable `Play On Start` so walls wait until the `GameStart` state.
   - Wire the mocopi connected button or touchable panel to `ConfirmMocopiConnected`. This shows the complete panel, then advances to `AvatarSelection` after `Connection Complete Delay`.
   - If you want manual advance from the complete panel, disable `Auto Advance After Connection Complete` and wire a button to `ContinueToAvatarSelection`.
   - In `AvatarSelectionPanel`, place three tapestry-style avatar panels side by side.
   - Add `AvatarSelectionTapestryPanel` to each tapestry panel, assign its preview `Image`, avatar sprite, label, normal color, and selected yellow color.
   - Wire each tapestry panel's UI Button or touch callback to its `AvatarSelectionTapestryPanel.Choose`.
   - Assign the three tapestry panel components to `Avatar Selection Panels` on `MocopiGameStateController`.
   - Put selectable avatar prefabs in `Avatar Prefabs` and assign `Avatar Spawn Point` if the avatar should spawn at selection time.
   - Or put existing scene avatar root objects in `Avatar Roots` if the avatars are already in the scene and should be toggled instead of spawned.
   - After a tapestry panel is chosen, it lights yellow and advances to `Ready` after `Avatar Selection Wait Seconds`.
   - In `ReadyPanel`, create `ReadyInstructionPanel`, `ReadyCountdownPanel`, and `ReadyStartPanel`.
   - Use `ReadyInstructionPanel` for the pose prompt: hold both hands touching above the head.
   - Use `ReadyCountdownPanel` for the five-second countdown and assign its countdown `Text` to `Ready Countdown Text`.
   - Use `ReadyStartPanel` for a `Start` message. It appears when the countdown reaches 0, then the state moves to `GameStart`.
   - Assign the three ready panels on `MocopiGameStateController`.
   - Add `ReadyHandOrbVisualizer` to a scene object, then assign it to `Ready Hand Orb Visualizer`.
   - The visualizer draws two hand spheres with `Custom/ReadyHandOrbGeometry` and merges them into one yellow sphere when both hands touch.
   - Tune `Ready Hold Seconds`, `Hand Touch Distance`, `Hands Above Head Height`, and `Max Hands Head Horizontal Distance` if the mocopi pose feels too strict or loose.
   - Wire later UI buttons or touchable panels to `StartGame` and `RestartScene`.

6. Add touchable mocopi UI panels.
   - Create an empty GameObject in front of the player, for example `TouchableUI`.
   - Add `TouchableUIPanelGrid`.
   - Put one panel label per line in `Labels`, then click `Rebuild Touchable Panels`.
   - Adjust panel values under `Panel Style`, including `Panel Opacity`, `Panel Outline Color`, `Panel Outline Brightness`, `Panel Outline Alpha`, and `Panel Outline Thickness Pixels`.
   - Adjust text values under `Font Style`, including `Font`, `Font Color`, `Font Outline Color`, `Font Outline Brightness`, `Font Outline Alpha`, and `Font Outline Thickness Pixels`.
   - Disable `Show Panel Background` when you want only the font and outline to float in space while the trigger rectangle still works.
   - Each generated panel has a trigger `BoxCollider`, a kinematic `Rigidbody`, and a world-space UI label. Add callbacks to each `TouchableUIPanel` event if a panel should start, retry, or change game state.

## GameMode: independent player arenas

- GameMode has separate `Player1Arena` and `Player2Arena` roots, each with its own stage, floor, lights, avatar, camera, and wall manager. Solo mode hides the second environment; both mocopi receivers remain available on the connection screen.
- Layers 8 (`Player1Arena`) and 9 (`Player2Arena`) are reserved for these arenas. Each camera and light uses only its own layer. `PlayerArenaIsolation` disables physics collisions between these layers during play and restores the previous setting on exit.
- Spawned walls, detached cubes, avatar colliders, mouse colliders, and impact effects inherit their owner's arena layer. Touch panels also reject the other player's colliders. Keep wall roots unparented: wall hit accounting uses detached cube hierarchy.
- The HUD is clipped to each camera's viewport and shows that player's score, wall progress, and feedback. The round state and co-op/versus result remain shared.
- To configure a compatible GameMode scene again, stop Play mode and use `Tools > mocopi > Separate Player Arenas`. This duplicates the stage only on the first application and saves the scene and layer names.
- Automated Play-mode validation: `GameModeValidation.PlayTest` checks separate rendering/physics/touch layers, 5-second countdown, ten-wall rounds, retry, co-op, versus, debris cleanup, and the return from 2P to 1P. Run this in a project copy: the batch test exits Unity when finished.

## Tuning Tips

### Music, light trails and 3D audio floor

- GameMode uses `Assets/Audio/Tick Tock Tantrum.mp3` (116.6 seconds). `GameMusicDirector` on the game controller plays one shared 2D soundtrack when the state becomes `Playing`, after the countdown/start cue. Retry restarts from the beginning. Very long rounds loop the track.
- Leaving play (result or menu return) fades the track to silence over **3 real seconds**, then stops it. The fade also runs at `Time.timeScale = 0`; a new round cancels any previous fade. Default music volume is **0.65**, adjustable on `GameMusicDirector`.
- Each `Player1Arena/Environment/Music Visuals` and `Player2Arena/Environment/Music Visuals` has its own `ArenaMusicVisuals`. `Play Speed` controls the approaching light trails (default 28 m/s); `Idle Speed` is the slower menu motion (5 m/s). Lights use sparse 0.8 m lanes with slow/cruising/overtaking speed groups, staggered entries and occasional doublets. They follow the side walls, sloping shoulders and ceiling of the tunnel toward the player; no moving light plane is created on the floor.
- The track's actual spectrum drives 16 frequency bands. A short history travels through 64 wide translucent tiles in two concentric rows around each player. The circle diameter is 115% of the actual wall prefab width. A soft pastel multicolor halo and thin moving electric traces outline the ring. `Tile Opacity` defaults to 0.24 (lower is more transparent), and `Maximum Bar Height` defaults to 0.22 m; `Spectrum Gain` on the music director adjusts sensitivity. The central player lane remains clear, and the decorative meshes have no colliders. Both player views share the same audio analysis while keeping their render layers separate.
- `Tools > mocopi > Apply Music and Audio Floor` configures the current GameMode scene and saves it. `ArenaMusicValidation.Run` in a project copy checks real audio output/spectrum, silent countdown, synchronized start, three-second fade, retry, player isolation, transparent tile rendering and open-bottom tunnel geometry, then exits Unity. It writes previews under `Logs/GameModePreview/music-floor-*.png`.

## Tuning Tips

- `MocopiPlayerColliderRig` radii are world-space metres: `0.05` means a 5 cm radius, independent of the avatar/parent scale. Capsule length follows the current distance between its bones without applying scale twice. Offsets are also metres; their axes follow the corresponding bone rotation.
- Tune the rig on the scene avatar (or its prefab), not the generated `*_PlayerCollider` children. Existing children update in the editor and during play. Changes made only during Play mode remain temporary, as with other Unity Inspector changes.
- Gizmo previews use the same world dimensions and offsets as the generated colliders. The collider hierarchy still belongs to the original bones, preserving hand-only touch filtering and per-player layers.
- Mouse testing has a separate radius: `MocopiLocalGameLoopController > Debug Mouse Collider Radius`. It is also in metres. Disable `Debug Use Mouse Collision` when testing only the avatar's body contact.
- `PlayerColliderValidation.Run` validates actual physics surfaces and inside/outside contact at different scales in a project copy. Existing authored radius/offset values are retained; after this fix their world size may be smaller than the previously scale-multiplied result.

- If hits feel too strict, raise `Hole Expansion Cells` on `WallMaskData` or reduce collider radii in `MocopiPlayerColliderRig`.
- If the mocopi pose hole feels too narrow or wide, tune `Pose Body Radius`, `Pose Hand Foot Radius`, and `Pose Head Radius` on `WallMaskData`.
- Saved mask JSON uses one row per mask row: `1` means solid cube, `0` means cut-out hole.
- If the letter hole is too small, increase `Rows` and `Columns` before rebuilding the wall prefab.
- If walls arrive too fast for mocopi latency, lower `Move Speed` or increase `Spawn Interval`.
- `Safe Hit Threshold` in `WallManager` controls how many cube hits are allowed before a wall subtracts from the score.

## Recommended First Test

1. Start with `Move Speed = 2`, `Spawn Interval = 5`, `Safe Hit Threshold = 3`.
2. Confirm A-shaped holes appear and move toward the player.
3. Confirm contact cubes fall and the score subtracts only when a wall exceeds the safe hit threshold.
4. After the flow works, raise speed and tune the safe hit threshold.

## Detached wall cubes and UI feedback (2026-10-20)

- A contacted cell keeps its arena render layer, detaches, enables gravity and free rotation, and inherits the wall velocity with a small outward nudge. A solid child collider replaces its original trigger. Moderate friction, low restitution, damping and continuous collision detection produce a short tumble that settles on the floor.
- Each WallCube has its own Life Time (7 seconds) and Life Time Variation (1.5 seconds). Expiry uses real time, including a paused result screen. Ordinary wall removal does not remove debris; retry/menu cleanup does.
- Player1Debris / Player2Debris occupy physics layers 10 / 11. Debris colliders exclude all contacts at override priority 10. Only the owning arena's floor and player colliders explicitly allow them at priority 20. Cube-to-cube, wall, UI and cross-arena contacts are rejected. The existing Plane floors are discovered at round start; a custom stage can supply WallManager's Debris Floors explicitly.
- WallHitTracker records first contacts independently of object lifetime, so an expired cube still counts in the wall result.
- TouchableUIPanel plays once after its hold/click is accepted. Automatic feedback resolves existing persistent actions; return/cancel actions use Cancel. Runtime-only bindings can set Click Sound explicitly. UIActionAudio uses a shared 2D source at volume 0.65, outside menu hierarchies, with a 60 ms overlap guard. Sound provenance and normalization values are in Assets/Resources/Audio/UI/SOURCES.md.
- Japanese operator guides: output/pdf/GameMode_Guide_JA.pdf and output/pdf/CaptureMode_Guide_JA.pdf. Regenerate with Docs/build_manuals.py (ReportLab; Windows BIZ UD Gothic fonts).
- Run DebrisAndUIValidation.Run with Unity's -executeMethod against a disposable project copy to check floor/player physics, forbidden contacts, independent expiry, scoring, cleanup and menu sound bindings. GameModeValidation.PlayTest also covers the complete solo/co-op/versus flow.
