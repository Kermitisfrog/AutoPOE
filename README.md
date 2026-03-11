# AutoPOE

AutoPOE is an ExileCore plugin that automates a follower character in Path of Exile. The current implementation is centered on a single follower sequence that tracks a configured leader, stays near them, reacts to area transitions, handles a small set of contextual tasks, and drives the game client through simulated keyboard and mouse input.

The plugin is built for Windows and depends on ExileCore and the game memory structures exposed by the DLLs in the `Resources` folder.

## What It Does

At runtime, the plugin can:

- Follow a named party leader.
- Move directly toward the leader when far away.
- Detect likely same-instance transitions when the leader suddenly disappears or moves a long distance.
- Reacquire the leader after area changes.
- Click nearby quest items.
- Claim a nearby waypoint once per area.
- Attack nearby hostile enemies while staying close to the leader.
- Cast a configured buff skill on the leader and an optional second target.
- Auto-click the gem level-up button when gems are ready.
- Mirror the leader's Ultimatum choice when the Ultimatum panel is open.
- Optionally weapon swap around `smite_buff` state.
- Recover from death by clicking resurrect at checkpoint.
- Show an in-game debug overlay for follower state.
- Write caught exceptions to `Errors.txt` in the plugin runtime directory.

## Runtime Model

`Main` is the ExileCore plugin entry point. It initializes shared state in `Core`, then runs `FollowerSequence` during `Tick()` whenever all of the following are true:

- The plugin is enabled.
- The game client is in-game.
- The area is not loading.
- The bot is toggled on.
- The global action throttle allows another action.

`Render()` handles the start/stop hotkey and draws the follower debug overlay. `AreaChange()` reinitializes terrain/pathing state for the follower sequence and attempts to bring the game window back to the foreground.

## Controls

### Main control

- Plugin enable toggle: ExileCore settings checkbox for `Enable`.
- Start or stop bot: `Insert` by default.

### Runtime overrides

- Hold `CapsLock`: disables follower logic for that tick, clears queued tasks, and allows manual control without interference.
- Press `Shift`: toggles direct follow mode on or off.

### Direct follow mode

When direct follow mode is enabled:

- Normal task logic is skipped.
- The follower continuously aims at the leader.
- The movement key is held down until direct follow is toggled off.
- If the leader gets farther than `Dash Leader Distance`, the configured dash key is tapped.

## Feature Breakdown

### 1. Leader following

The plugin resolves the follow target by player name or render name using `Follower.LeaderName`.

Behavior:

- If the leader is farther away than `Close Path Distance`, a movement task is queued or updated toward the leader.
- If the leader appears to have moved too far in a single update, the plugin searches known transitions near the leader's last position and tries to use one.
- If the leader is temporarily missing after an area change, the plugin waits for `Leader Reacquire Delay` before resuming stale tasks.
- If the leader cannot be found later in the area, the plugin can still attempt a transition near the last known leader position.

### 2. Terrain-aware dash support

If `Allow Dash` is enabled and the character is not in town, movement tasks can attempt a dash.

Dash logic uses two checks:

- Leader-distance dash: if the leader is farther than `Dash Leader Distance`, dash toward the leader.
- Terrain dash: inspect terrain tiles between follower and target and dash if the route suggests a traversable wall/obstacle shortcut.

After a dash, the plugin immediately follows with a movement input to keep closing distance.

### 3. Transition handling

The follower sequence caches transitions from the current area, including:

- Standard area transitions
- Portals
- Town portals
- Specific special portal metadata used by the current code

Transition tasks:

- Move toward the transition if far away.
- Click the transition when within follow range.
- Give up after several attempts if the transition does not resolve.

### 4. Quest loot pickup

When the follower is already near the leader, the plugin looks for nearby quest loot:

- Only targetable world items are considered.
- Items are treated as quest items if their translated class name is `QuestItem` or their icon matches the large green pentagon loot marker used in the current detection code.
- The plugin hovers the ground label first, then clicks once targeted.

This is intentionally narrow. It is not a general loot bot.

### 5. Waypoint claiming

If the follower is near a waypoint and has not used one yet in the current area, a waypoint task is added.

Current behavior:

- The task clicks the waypoint.
- The sequence only attempts this once per area instance.

### 6. Combat support

Combat is optional and only runs while the follower is close to the leader.

Enemy selection:

- Hostile, alive, targetable monsters within `Close Path Distance`.

Combat task behavior:

- Combat only queues if `Enable Combat Tasks` is on.
- Combat only queues when the player does not currently have `smite_buff`.
- Enemies must also be within `Combat Leash Distance` of the leader.
- When active, combat uses the configured `Combat Key` on the enemy's screen position.
- When combat ends or fails, the plugin waits `Combat Reengage Delay` before requeueing combat.

### 7. Weapon swap automation

If `Enable Weapon Swap` is enabled, the follower sequence checks the player's weapon type together with `smite_buff`:

- If `smite_buff` is active and the main-hand weapon type is not `4`, weapon swap is pressed.
- If `smite_buff` is not active and the main-hand weapon type is `4`, weapon swap is pressed.

This logic is specialized for the author's setup. If your build does not rely on this pattern, leave it disabled.

### 8. Buff support

Buff tasks are intended for link-style support behavior.

Behavior:

- The plugin checks whether the leader has the configured buff name.
- If not, it queues a buff task aimed at the leader.
- If `Extra Buff Target Name` is set, the plugin also tries to keep that second player buffed.
- The plugin tracks the target entity ID when possible so the buff task can reacquire the right player.
- Buff tasks expire after several attempts or if the target moves out of close range.

By default the buff to check is `critical_link_target`.

### 9. Gem leveling

If `Enable Gem Leveling` is enabled, the plugin inspects the gem level-up panel and looks for a `LevelUpAllGemsButton` element through reflection.

Current behavior:

- Only the global level-up-all button is clicked.
- The task is only queued when the element is visible.
- The task removes itself after one click or a few failed checks.

### 10. Ultimatum mirroring

If the Ultimatum panel is open:

- An Ultimatum task is forced to the front of the queue.
- The plugin waits until one of the choice elements has at least one locked vote.
- It clicks that same choice.
- It then clicks the confirm button if present.

This is effectively a vote-follow behavior for party play.

### 11. Death handling

If the player is dead and the resurrect panel is visible, the plugin attempts to click `Resurrect At Checkpoint` and then resumes normal logic afterward.

## Configuration

All settings are exposed through ExileCore's settings UI.

### Top-level settings

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `Enable` | Toggle | `false` | Enables the plugin logic and overlay. |
| `StartBot` | Hotkey | `Insert` | Toggles `Core.IsBotRunning`. |
| `Action Frequency` | Range `25-500` | `100` | Global minimum time between actions in milliseconds. |
| `Clamp Size` | Range `100-1000` | `400` | Clamp radius used when target positions fall outside the visible game window. |

### Follower settings

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `Leader Name` | Text | empty | Character name or render name of the player to follow. |
| `Movement Key` | Hotkey | `T` | Main movement skill or movement input used while following. |
| `Movement Frequency` | Range `10-250` | `50` | Delay between follower task inputs in milliseconds. |
| `Close Path Distance` | Range `10-500` | `100` | Distance threshold that separates close follow from far/pathfinding behavior. Uses grid coordinates. |
| `Leader Reacquire Delay` | Range `0-10000` | `4000` | Delay after area load while waiting to find the leader again before resuming other behavior. |
| `Allow Dash` | Toggle | `true` | Enables automated dash attempts during movement. |
| `Dash Key` | Hotkey | `W` | Skill key used for dash or travel skill. |
| `Dash Leader Distance` | Range `5-200` | `40` | Minimum leader distance before direct dash toward leader is attempted. |
| `Enable Combat Tasks` | Toggle | `true` | Allows the plugin to queue combat actions. |
| `Combat Reengage Delay` | Range `0-5000` | `1500` | Cooldown before combat can be queued again after a combat task ends. |
| `Combat Leash Distance` | Range `20-200` | `60` | Maximum enemy distance from the leader for combat to be allowed. |
| `Combat Key` | Hotkey | `LButton` | Attack key used during combat tasks. |
| `Enable Weapon Swap` | Toggle | `true` | Enables the specialized `smite_buff`/weapon type swap behavior. |
| `Weapon Swap Key` | Hotkey | `X` | Key used for weapon swap. |
| `Enable Buff Tasks` | Toggle | `true` | Allows buff tasks to be generated. |
| `Buff Key` | Hotkey | `Z` | Skill key used to cast the buff. |
| `Buff Target Name` | Text | `critical_link_target` | Buff name checked on the target before a buff task is queued. |
| `Extra Buff Target Name` | Text | empty | Optional second player name to keep buffed in addition to the leader. |
| `Enable Gem Leveling` | Toggle | `true` | Enables automatic gem level-up clicks. |

## Typical Setup

1. Copy or place the plugin in your ExileCore plugins source folder.
2. Run ExileAPI
3. Enable `Auto POE` in the plugin list.
4. Configure `Leader Name` to exactly match the leader's player name or render name.
5. Bind `Movement Key`, `Dash Key`, `Combat Key`, `Buff Key`, and `Weapon Swap Key` to match your in-game hotbar.
6. Press `Insert` to start or stop the bot.

## Recommended Configuration Notes

- Use a movement key that is safe to spam and can path naturally toward the leader. Cannot be left click.
- Keep `Close Path Distance` conservative. Too low causes excessive repositioning; too high reduces transition detection quality.
- If the character uses no travel skill, disable `Allow Dash`.
- If the build is not using Smite or similar behavior, disable `Enable Combat Tasks` and `Enable Weapon Swap`.
- If you are using a link/support skill, confirm the target buff name shown by your build and set `Buff Target Name` accordingly.
- If gem leveling is unreliable after a game update, the reflected UI member name may have changed.

## Debug Overlay

The plugin draws multiple debug lines on screen, including:

- Whether the leader is found.
- Which branch of follow logic is active.
- Task count and current task.
- Distance to leader and next task target.
- Whether direct follow mode is active.
- How many levelable gems were detected.
- The reflected type name of the gem level-up button.

This overlay is useful when tuning settings or checking why a task was or was not queued.

## Logging and Error Handling

Most top-level exceptions are caught and written to `Errors.txt` in the plugin base directory.

Current protected paths include:

- `Main.Tick`
- `Main.Render`
- `Main.AreaChange`
- `FollowerSequence.Initialize`
- `EntityHelper.GetPlayerEntityByName`

If behavior stops unexpectedly, check `Errors.txt` first.

## Source Map

- `Main.cs`: ExileCore plugin lifecycle.
- `Core.cs`: shared state, action throttling, and error logging.
- `Settings.cs`: all configuration nodes shown in ExileCore.
- `Controls.cs`: window-aware input helpers and general UI interaction.
- `Logic/Sequences/FollowerSequence.cs`: primary behavior engine and task queue.
- `Logic/Actions/*`: task implementations for movement, transitions, loot, combat, buffs, gem leveling, waypoint claiming, and ultimatum handling.
- `Logic/Helpers/EntityHelper.cs`: leader, enemy, buff, and quest loot detection.
- `Logic/Helpers/GemHelper.cs`: gem level-up UI detection.
- `Logic/Helpers/InputHelper.cs`: cursor placement and UI click helpers.