using AutoPOE.Logic.Actions;
using AutoPOE.Logic.Helpers;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;

namespace AutoPOE.Logic.Sequences
{
    /// <summary>
    /// Follower sequence allows a character to automatically follow a leader player in the same game instance.
    /// Handles terrain parsing, task queuing, and movement with support for transitions and quest loot.
    /// </summary>
    public class FollowerSequence : ISequence
    {
        private readonly Dictionary<TaskNode.TaskNodeType, IFollowerTaskAction> _taskActions;
        private Random _random = new Random();
        private Dictionary<uint, Entity> _areaTransitions = new Dictionary<uint, Entity>();
        private List<TaskNode> _tasks = new List<TaskNode>();

        private Vector2 _lastTargetPosition = Vector2.Zero;
        private Vector2 _lastPlayerPosition = Vector2.Zero;
        private Entity? _followTarget = null;
        private string _debugLeaderBranch = string.Empty;
        private string _debugFarDetails = string.Empty;

        private DateTime _combatCooldownUntil = DateTime.MinValue;
        private DateTime _reacquireLeaderUntil = DateTime.MinValue;

        private bool _hasUsedWP = false;

        private int _numRows, _numCols;
        private byte[,]? _tiles;

        private DateTime _nextBotAction = DateTime.Now;
        private DateTime _nextDirectFollowDashAt = DateTime.MinValue;
        private bool _isDirectFollowModeEnabled = false;
        private bool _wasShiftDownLastTick = false;

        public FollowerSequence()
        {
            _taskActions = new Dictionary<TaskNode.TaskNodeType, IFollowerTaskAction>
            {
                { TaskNode.TaskNodeType.Movement, new MovementTaskAction() },
                { TaskNode.TaskNodeType.Loot, new LootTaskAction() },
                { TaskNode.TaskNodeType.Transition, new TransitionTaskAction() },
                { TaskNode.TaskNodeType.ClaimWaypoint, new ClaimWaypointTaskAction() },
                { TaskNode.TaskNodeType.Combat, new CombatTaskAction() },
                { TaskNode.TaskNodeType.BuffLead, new BuffLeadTaskAction() },
                { TaskNode.TaskNodeType.GemLevel, new GemLevelTaskAction() },
                { TaskNode.TaskNodeType.Ultimatum, new UltimatumTaskAction() },
            };
            ResetPathing();
        }

        private void ResetPathing()
        {
            ReleaseMovementKeySafely();
            _isDirectFollowModeEnabled = false;
            _wasShiftDownLastTick = false;
            _tasks = new List<TaskNode>();
            _followTarget = null;
            _lastTargetPosition = Vector2.Zero;
            _lastPlayerPosition = Vector2.Zero;
            _areaTransitions = new Dictionary<uint, Entity>();
            _hasUsedWP = false;
        }

        public void Initialize()
        {
            try
            {
                // Failsafe: ensure we are not leaving movement key held after plugin reloads.
                ReleaseMovementKeySafely();
                ResetPathing();
                _reacquireLeaderUntil = DateTime.Now.AddMilliseconds(Core.Settings.Follower.LeaderReacquireDelayMs.Value);

                // Load initial transitions
                var initByType = Core.GameController.EntityListWrapper.ValidEntitiesByType;
                foreach (var transition in initByType[EntityType.AreaTransition]
                    .Concat(initByType[EntityType.Portal])
                    .Concat(initByType[EntityType.TownPortal]))
                {
                    if (!_areaTransitions.ContainsKey(transition.Id))
                        _areaTransitions.Add(transition.Id, transition);
                }

                // Parse terrain data for pathfinding
                var terrain = Core.GameController.IngameState.Data.Terrain;
                var terrainBytes = Core.GameController.Memory.ReadBytes(terrain.LayerMelee.First, terrain.LayerMelee.Size);
                _numCols = (int)(terrain.NumCols - 1) * 23;
                _numRows = (int)(terrain.NumRows - 1) * 23;
                if ((_numCols & 1) > 0)
                    _numCols++;

                _tiles = new byte[_numCols, _numRows];
                int dataIndex = 0;
                for (int y = 0; y < _numRows; y++)
                {
                    for (int x = 0; x < _numCols; x += 2)
                    {
                        var b = terrainBytes[dataIndex + (x >> 1)];
                        _tiles[x, y] = (byte)((b & 0xf) > 0 ? 1 : 255);
                        if (x + 1 < _numCols)
                            _tiles[x + 1, y] = (byte)((b >> 4) > 0 ? 1 : 255);
                    }
                    dataIndex += terrain.BytesPerRow;
                }

                // Read ranged layer
                terrainBytes = Core.GameController.Memory.ReadBytes(terrain.LayerRanged.First, terrain.LayerRanged.Size);
                dataIndex = 0;
                for (int y = 0; y < _numRows; y++)
                {
                    for (int x = 0; x < _numCols; x += 2)
                    {
                        var b = terrainBytes[dataIndex + (x >> 1)];

                        if (_tiles[x, y] == 255)
                            _tiles[x, y] = (byte)((b & 0xf) > 3 ? 2 : 255);
                        if (x + 1 < _numCols && _tiles[x + 1, y] == 255)
                            _tiles[x + 1, y] = (byte)((b >> 4) > 3 ? 2 : 255);
                    }
                    dataIndex += terrain.BytesPerRow;
                }
            }
            catch (Exception ex)
            {
                _tiles = null;
                _numCols = 0;
                _numRows = 0;
                Core.LogError("FollowerSequence.Initialize", ex);
            }
        }

        public void Tick()
        {
            // Ultimatum panel: if visible, ensure an Ultimatum task is at the front of the queue and execute it.
            var ultimatumPanel = Core.GameController.IngameState.IngameUi.UltimatumPanel;
            if (ultimatumPanel != null && ultimatumPanel.IsVisible)
            {
                if (!_tasks.Any(t => t.Type == TaskNode.TaskNodeType.Ultimatum))
                    _tasks.Insert(0, new TaskNode(Vector2.Zero, 0, TaskNode.TaskNodeType.Ultimatum));

                if (DateTime.Now > _nextBotAction && _tasks.Count > 0)
                    ExecuteTask();
                return;
            }

            if (Input.IsKeyDown(Keys.CapsLock))
            {
                // If control is held, disable follower logic and clear tasks to give player full manual control without interference.
                if (_tasks.Count > 0)
                    _tasks.Clear();
                _followTarget = null;
                _lastTargetPosition = Vector2.Zero;
                _debugLeaderBranch = "manual-control";
                return;
            }
            var isShiftDown = Input.IsKeyDown(Keys.ShiftKey);
            if (isShiftDown && !_wasShiftDownLastTick)
            {
                ToggleDirectFollowMode();
            }
            _wasShiftDownLastTick = isShiftDown;

            if (_isDirectFollowModeEnabled)
            {
                // Direct follow mode ignores all standard follower task logic.
                _tasks.Clear();
                _followTarget = EntityHelper.GetFollowingTarget();

                if (_followTarget != null)
                {
                    var targetPos = _followTarget.GridPosNum;
                    CursorHelper.SetCursorPosHuman2(Controls.GetScreenClampedGridPos(targetPos));
                    _lastTargetPosition = targetPos;

                    var followerPos = Core.GameController.Player.GridPosNum;
                    var leaderDistance = Vector2.Distance(followerPos, targetPos);
                    if (leaderDistance > Core.Settings.Follower.DashLeaderDistance.Value)
                        TryDirectFollowDash();
                }

                _lastPlayerPosition = Core.GameController.Player.GridPosNum;
                return;
            }

            // Weapon swap check: if enabled, check if we have the smite buff but no longer have a nearby enemy. If so, swap weapons to reset smite cooldown
            if (Core.Settings.Follower.IsWeaponSwapEnabled.Value)
            {
                // Get the value of the MainHandWeaponType stat from the Player's Stats dictionary and check if it's 4. If so it's trypanaon and we should swap
                if (Core.GameController.Player.Buffs.Any(buff => buff.Name == "smite_buff") && Core.GameController.Player.Stats.TryGetValue(GameStat.MainHandWeaponType, out int weaponType) && weaponType != 4)
                {
                    Input.KeyDown(Core.Settings.Follower.WeaponSwapKey);
                    Thread.Sleep(_random.Next(15) + 10);
                    Input.KeyUp(Core.Settings.Follower.WeaponSwapKey);
                    Thread.Sleep(_random.Next(15) + 10);
                }
                else if (!Core.GameController.Player.Buffs.Any(buff => buff.Name == "smite_buff") && Core.GameController.Player.Stats.TryGetValue(GameStat.MainHandWeaponType, out int weaponType2) && weaponType2 == 4)
                {
                    Input.KeyDown(Core.Settings.Follower.WeaponSwapKey);
                    Thread.Sleep(_random.Next(15) + 10);
                    Input.KeyUp(Core.Settings.Follower.WeaponSwapKey);
                    Thread.Sleep(_random.Next(15) + 10);
                }
            }
            if (!Core.GameController.Player.IsAlive)
            {
                // Attempt to revive the player
                try
                {
                    var revivePanel = Core.GameController.IngameState.IngameUi.ResurrectPanel;
                    if (revivePanel != null && revivePanel.IsVisibleLocal)
                    {
                        var center = revivePanel.ResurrectAtCheckpoint.GetClientRect().Center;
                        Thread.Sleep(_random.Next(500, 1500));
                        Input.LeftDown();
                        Thread.Sleep(_random.Next(50, 150));
                        Input.LeftUp();
                        _ = Controls.ClickScreenPos(new System.Numerics.Vector2(center.X, center.Y));
                        Thread.Sleep(_random.Next(500, 1500));
                    }
                }
                catch
                {
                    // Silently fail if revive panel is not available
                }
                return;
            }

            // Dynamically update area transitions (portals/passages appear during gameplay)
            var validByType = Core.GameController.EntityListWrapper.ValidEntitiesByType;
            var currentTransitionIds = new HashSet<uint>(
                validByType[EntityType.AreaTransition]
                    .Concat(validByType[EntityType.Portal])
                    .Concat(validByType[EntityType.TownPortal])
                    .Concat(validByType[EntityType.MiscellaneousObjects].Where(I => I.Metadata == "Metadata/MiscellaneousObjects/Faridun/DjinnPortal"))
                    .Concat(validByType[EntityType.Effect].Where(I => I.Metadata == "Metadata/Effects/Microtransactions/Town_Portals/SekhemaPortal/SekhemaPortal"))
                    .Select(I => I.Id));

            // Remove transitions that no longer exist
            var removedIds = _areaTransitions.Keys.Where(id => !currentTransitionIds.Contains(id)).ToList();
            foreach (var id in removedIds)
                _areaTransitions.Remove(id);

            // Add new transitions
            foreach (var transition in validByType[EntityType.AreaTransition]
                .Concat(validByType[EntityType.Portal])
                .Concat(validByType[EntityType.TownPortal])
                    .Concat(validByType[EntityType.MiscellaneousObjects].Where(I => I.Metadata == "Metadata/MiscellaneousObjects/Faridun/DjinnPortal"))
                    .Concat(validByType[EntityType.Effect].Where(I => I.Metadata == "Metadata/Effects/Microtransactions/Town_Portals/SekhemaPortal/SekhemaPortal")))
            {
                if (!_areaTransitions.ContainsKey(transition.Id))
                    _areaTransitions.Add(transition.Id, transition);
            }

            // Cache the current follow target
            _followTarget = EntityHelper.GetFollowingTarget();
            if (_followTarget == null && DateTime.Now < _reacquireLeaderUntil)
            {
                _tasks.Clear();
                _nextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.BotInputFrequency.Value);
                _lastPlayerPosition = Core.GameController.Player.GridPosNum;
                return;
            }

            if (_followTarget != null)
            {
                var followerPos = Core.GameController.Player.GridPosNum;
                var targetPos = _followTarget.GridPosNum;
                var distanceFromFollower = Vector2.Distance(followerPos, targetPos);
                // Core.Graphics.DrawText($"[DEBUG] Known transitions: {string.Join(", ", _areaTransitions.Values.Select(t => t.RenderName))}", new Vector2(100, 140), SharpDX.Color.Magenta);
                // Core.Graphics.DrawText($"[DEBUG] Leader found at distance {leaderDist:F0}, ClearPath={Core.Settings.Follower.ClearPathDistance.Value}", new Vector2(100, 140), SharpDX.Color.Cyan);


                // We are NOT within clear path distance range of leader. Logic can continue
                if (distanceFromFollower >= Core.Settings.Follower.ClearPathDistance.Value)
                {
                    _debugLeaderBranch = "far";
                    // Leader moved VERY far in one frame. Check for transition to use to follow them.
                    var distanceMoved = Vector2.Distance(_lastTargetPosition, targetPos);
                    _debugFarDetails = $"lastTargetZero={(_lastTargetPosition == Vector2.Zero)} distMoved={distanceMoved:F0} clearPath={Core.Settings.Follower.ClearPathDistance.Value}";
                    if (_lastTargetPosition != Vector2.Zero && distanceMoved > Core.Settings.Follower.ClearPathDistance.Value)
                    {
                        _debugLeaderBranch = "far/transition";
                        var transition = _areaTransitions.Values.OrderBy(I => Vector2.Distance(_lastTargetPosition, I.GridPosNum)).FirstOrDefault();
                        var transitionDist = transition != null ? Vector2.Distance(_lastTargetPosition, transition.GridPosNum) : -1;
                        _debugFarDetails += $" transitionFound={(transition != null)} transitionDist={transitionDist:F0}";
                        if (transition != null && transitionDist < Core.Settings.Follower.ClearPathDistance.Value)
                        {
                            _tasks.Add(new TaskNode(transition.GridPosNum, 200, TaskNode.TaskNodeType.Transition));
                            _debugFarDetails += " taskAdded=true";
                        }
                        else
                        {
                            _debugFarDetails += " taskAdded=false";
                        }
                    }
                    else if (!_tasks.Any(t => t.Type == TaskNode.TaskNodeType.Transition))
                    {
                        // Upsert a single Movement task aimed at the leader's current position.
                        // MovementTaskAction handles the cursor aim, key press, and dash check.
                        _debugLeaderBranch = "far/direct-move";
                        var existingMoveTask = _tasks.FirstOrDefault(t => t.Type == TaskNode.TaskNodeType.Movement);
                        if (existingMoveTask != null)
                            existingMoveTask.WorldPosition = targetPos;
                        else
                            _tasks.Insert(0, new TaskNode(targetPos, Core.Settings.Follower.ClearPathDistance.Value));
                    }
                }
                else
                {
                    _debugLeaderBranch = "close";
                    // Clear all movement and transition tasks — we are within close range of the leader.
                    for (var i = _tasks.Count - 1; i >= 0; i--)
                        if (_tasks[i].Type == TaskNode.TaskNodeType.Movement || _tasks[i].Type == TaskNode.TaskNodeType.Transition)
                            _tasks.RemoveAt(i);

                    // Check if we should add quest loot logic. We're close to leader already
                    var questLoot = EntityHelper.GetLootableQuestItem();
                    var questLootInRange = questLoot != null &&
                        Vector2.Distance(followerPos, questLoot.GridPosNum) < Core.Settings.Follower.ClearPathDistance.Value;
                    var hasLootTask = _tasks.FirstOrDefault(I => I.Type == TaskNode.TaskNodeType.Loot) != null;
                    Core.Graphics.DrawText($"[DEBUG] QuestLoot: Found={(questLoot != null)} InRange={questLootInRange} HasTask={hasLootTask}", new Vector2(100, 210), SharpDX.Color.Yellow);
                    if (questLoot != null &&
                        questLootInRange &&
                        !hasLootTask)
                    {
                        Core.Graphics.DrawText($"[DEBUG] Found quest loot item at {questLoot.GridPosNum}", new Vector2(100, 220), SharpDX.Color.Cyan);
                        _tasks.Add(new TaskNode(questLoot.GridPosNum, Core.Settings.Follower.ClearPathDistance.Value, TaskNode.TaskNodeType.Loot));
                    }

                    // Check if there's a waypoint nearby (only if not used yet)
                    if (!_hasUsedWP)
                    {
                        var waypoint = Core.GameController.EntityListWrapper.Entities.FirstOrDefault(I =>
                            I.Type == EntityType.Waypoint &&
                            Vector2.Distance(followerPos, I.GridPosNum) < Core.Settings.Follower.ClearPathDistance.Value);

                        if (waypoint != null)
                        {
                            _hasUsedWP = true;
                            _tasks.Add(new TaskNode(waypoint.GridPosNum, Core.Settings.Follower.ClearPathDistance.Value, TaskNode.TaskNodeType.ClaimWaypoint));
                        }
                    }

                    // Buff check: queue buff tasks for leader and optional extra named player if they are missing link target buff.
                    if (Core.Settings.Follower.IsBuffEnabled.Value)
                    {
                        var configuredBuffName = Core.Settings.Follower.BuffTargetBuffName.Value?.Trim();
                        if (string.IsNullOrWhiteSpace(configuredBuffName))
                            configuredBuffName = "critical_link_target";

                        var followTargetHasConfiguredBuff = EntityHelper.HasBuff(_followTarget, configuredBuffName);
                        if (_followTarget != null && !followTargetHasConfiguredBuff && !HasBuffTaskForTarget(_followTarget.Id))
                        {
                            var leaderLabel = !string.IsNullOrWhiteSpace(_followTarget.RenderName) ? _followTarget.RenderName : "leader";
                            Core.Graphics.DrawText($"[DEBUG] Creating buff task on leader at {targetPos}", new Vector2(100, 260), SharpDX.Color.Green);
                            _tasks.Add(new TaskNode(targetPos, Core.Settings.Follower.ClearPathDistance.Value, TaskNode.TaskNodeType.BuffLead, _followTarget.Id, leaderLabel));
                        }

                        var extraBuffTargetName = Core.Settings.Follower.ExtraBuffTargetName.Value?.Trim();
                        var extraBuffTarget = EntityHelper.GetPlayerEntityByName(extraBuffTargetName);
                        var isDifferentFromLeader = _followTarget == null || extraBuffTarget == null || extraBuffTarget.Id != _followTarget.Id;

                        if (extraBuffTarget != null && isDifferentFromLeader && !EntityHelper.HasBuff(extraBuffTarget, configuredBuffName) && !HasBuffTaskForTarget(extraBuffTarget.Id))
                        {
                            var extraLabel = !string.IsNullOrWhiteSpace(extraBuffTarget.RenderName) ? extraBuffTarget.RenderName : extraBuffTargetName ?? "extra-target";
                            Core.Graphics.DrawText($"[DEBUG] Creating buff task on extra target at {extraBuffTarget.GridPosNum}", new Vector2(100, 280), SharpDX.Color.Green);
                            _tasks.Add(new TaskNode(extraBuffTarget.GridPosNum, Core.Settings.Follower.ClearPathDistance.Value, TaskNode.TaskNodeType.BuffLead, extraBuffTarget.Id, extraLabel));
                        }
                    }

                    // Combat check: if player does NOT have the smite buff, look for nearby hostile enemies
                    // Combat check: only if combat tasks enabled and player does NOT have the mine buff
                    var shouldHoldCloseFollowMovement = false;
                    if (Core.Settings.Follower.IsCombatEnabled.Value && !Core.GameController.Player.Buffs.Any(buff => buff.Name == "smite_buff"))
                    {
                        var hostileEnemy = EntityHelper.GetNearbyHostileEnemy();
                        var combatLeash = Core.Settings.Follower.CombatLeashDistance.Value;
                        var enemyDistToLeader = hostileEnemy != null ? Vector2.Distance(hostileEnemy.GridPosNum, targetPos) : float.MaxValue;
                        var combatCooldownActive = DateTime.Now < _combatCooldownUntil;

                        if (hostileEnemy != null &&
                            !combatCooldownActive &&
                            enemyDistToLeader <= combatLeash)
                        {
                            shouldHoldCloseFollowMovement = true;
                            var existingCombatTaskIndex = _tasks.FindIndex(I => I.Type == TaskNode.TaskNodeType.Combat);
                            if (existingCombatTaskIndex < 0)
                            {
                                Core.Graphics.DrawText($"[DEBUG] Found hostile enemy for combat at {hostileEnemy.GridPosNum}", new Vector2(100, 240), SharpDX.Color.Red);
                                // Insert combat at the front so close-follow movement does not starve combat execution.
                                _tasks.Insert(0, new TaskNode(hostileEnemy.GridPosNum, Core.Settings.Follower.ClearPathDistance.Value, TaskNode.TaskNodeType.Combat));
                            }
                            else if (existingCombatTaskIndex > 0)
                            {
                                var combatTask = _tasks[existingCombatTaskIndex];
                                _tasks.RemoveAt(existingCombatTaskIndex);
                                _tasks.Insert(0, combatTask);
                                combatTask.WorldPosition = hostileEnemy.GridPosNum;
                            }
                            else
                            {
                                _tasks[0].WorldPosition = hostileEnemy.GridPosNum;
                            }
                        }
                        else if (hostileEnemy != null && (combatCooldownActive || enemyDistToLeader > combatLeash))
                        {
                            Core.Graphics.DrawText($"[DEBUG] Combat skipped: cooldown={combatCooldownActive} enemyDistToLeader={enemyDistToLeader:F0} leash={combatLeash}", new Vector2(100, 240), SharpDX.Color.Red);
                        }
                    }

                    if (shouldHoldCloseFollowMovement)
                    {
                        for (var i = _tasks.Count - 1; i >= 1; i--)
                        {
                            if (_tasks[i].Type == TaskNode.TaskNodeType.Movement)
                                _tasks.RemoveAt(i);
                        }
                    }

                    // Gem level check: if enabled, create a gem level task on the leader (if we have levelable gems and no existing gem level task)
                    var hasGemLevelTask = _tasks.FirstOrDefault(I => I.Type == TaskNode.TaskNodeType.GemLevel) != null;
                    if (Core.Settings.Follower.IsGemLevelingEnabled.Value && !hasGemLevelTask && GemHelper.GetLevelableGems().Count > 0)
                    {
                        Core.Graphics.DrawText("[DEBUG] Creating gem level task", new Vector2(100, 300), SharpDX.Color.LightSkyBlue);
                        _tasks.Add(new TaskNode(followerPos, Core.Settings.Follower.ClearPathDistance.Value, TaskNode.TaskNodeType.GemLevel));
                    }
                }
                _lastTargetPosition = targetPos;
            }
            // Leader is null but we have tracked them this map.
            // Try using transition to follow them to their map
            else if (_tasks.Count == 0 &&_lastTargetPosition != Vector2.Zero)
            {
                Core.Graphics.DrawText($"[DEBUG] Leader lost, searching for transitions. Known transitions: {_areaTransitions.Count}", new Vector2(100, 140), SharpDX.Color.Magenta);
                
                var transOptions = _areaTransitions.Values
                    .Where(I => Vector2.Distance(_lastTargetPosition, I.GridPosNum) < Core.Settings.Follower.ClearPathDistance.Value)
                    .OrderBy(I => Vector2.Distance(_lastTargetPosition, I.GridPosNum))
                    .ToArray();
                
                Core.Graphics.DrawText($"[DEBUG] Transitions in range: {transOptions.Length}", new Vector2(100, 160), SharpDX.Color.Magenta);
                
                if (transOptions.Length > 0)
                    _tasks.Add(new TaskNode(transOptions[_random.Next(transOptions.Length)].GridPosNum, Core.Settings.Follower.ClearPathDistance.Value, TaskNode.TaskNodeType.Transition));
            }

            // Execute tasks
            if (DateTime.Now > _nextBotAction && _tasks.Count > 0)
            {
                ExecuteTask();
            }

            _lastPlayerPosition = Core.GameController.Player.GridPosNum;
        }

        private void ExecuteTask()
        {
            var currentTask = _tasks.First();
            var playerDistanceMoved = Vector2.Distance(Core.GameController.Player.GridPosNum, _lastPlayerPosition);

            // We are using a same map transition and have moved significantly since last tick. Mark the transition task as done.
            if (currentTask.Type == TaskNode.TaskNodeType.Transition &&
                playerDistanceMoved >= Core.Settings.Follower.ClearPathDistance.Value)
            {
                _tasks.RemoveAt(0);
                if (_tasks.Count > 0)
                    currentTask = _tasks.First();
                else
                    return;
            }

            var context = new FollowerActionContext(
                _random,
                _tasks,
                _followTarget,
                () => _nextBotAction,
                value => _nextBotAction = value,
                CheckDashTerrain,
                EntityHelper.GetLootableQuestItem,
                EntityHelper.GetNearbyHostileEnemy,
                GemHelper.GetLevelableGems,
                item => CursorHelper.MouseoverItem(item, _random),
                element => CursorHelper.ClickLevelableGem(element, _random),
                CursorHelper.SetCursorPosHuman2,
                value => _combatCooldownUntil = value);

            _taskActions[currentTask.Type].Execute(context, currentTask);
        }

        private void ToggleDirectFollowMode()
        {
            _isDirectFollowModeEnabled = !_isDirectFollowModeEnabled;

            if (_isDirectFollowModeEnabled)
            {
                _tasks.Clear();
                Input.KeyDown(Core.Settings.Follower.MovementKey);
            }
            else
            {
                Input.KeyUp(Core.Settings.Follower.MovementKey);
            }
        }

        private void TryDirectFollowDash()
        {
            if (DateTime.Now < _nextDirectFollowDashAt)
                return;

            Input.KeyDown(Core.Settings.Follower.DashKey);
            Thread.Sleep(15 + _random.Next(15));
            Input.KeyUp(Core.Settings.Follower.DashKey);
            Core.ActionPerformed();

            _nextDirectFollowDashAt = DateTime.Now.AddMilliseconds(Math.Max(25, Core.Settings.Follower.BotInputFrequency.Value));
        }

        private void ReleaseMovementKeySafely()
        {
            try
            {
                Input.KeyUp(Core.Settings.Follower.MovementKey);
            }
            catch
            {
                // Ignore unload/load timing issues where settings/input are not yet available.
            }
        }

        private bool CheckDashTerrain(Vector2 targetPosition)
        {
            var playerGridPos = Core.GameController.Player.GridPosNum;
            bool PerformDash(Vector2 dashTarget, string reason)
            {
                Core.Graphics.DrawText($"[DEBUG] CheckDashTerrain: dashReason={reason}", new Vector2(10, 340), SharpDX.Color.Lime);
                _nextBotAction = DateTime.Now.AddMilliseconds(500 + _random.Next(Core.Settings.Follower.BotInputFrequency));
                CursorHelper.SetCursorPosHuman2(Controls.GetScreenClampedGridPos(dashTarget));
                Thread.Sleep(50 + _random.Next(Core.Settings.Follower.BotInputFrequency));
                Input.KeyDown(Core.Settings.Follower.DashKey);
                Thread.Sleep(15 + _random.Next(Core.Settings.Follower.BotInputFrequency));
                Input.KeyUp(Core.Settings.Follower.DashKey);

                // After dash, attempt one move input toward the follow target to keep closing distance.
                var moveTarget = _followTarget?.GridPosNum ?? dashTarget;
                CursorHelper.SetCursorPosHuman2(Controls.GetScreenClampedGridPos(moveTarget));
                Thread.Sleep(30 + _random.Next(Core.Settings.Follower.BotInputFrequency));
                Input.KeyDown(Core.Settings.Follower.MovementKey);
                Thread.Sleep(20 + _random.Next(Core.Settings.Follower.BotInputFrequency));
                Input.KeyUp(Core.Settings.Follower.MovementKey);

                Core.ActionPerformed();
                return true;
            }

            if (_followTarget != null)
            {
                var leaderDistance = Vector2.Distance(playerGridPos, _followTarget.GridPosNum);
                if (leaderDistance > Core.Settings.Follower.DashLeaderDistance.Value)
                {
                    return PerformDash(_followTarget.GridPosNum, "leader-distance");
                }
            }

            // Debug: show parameters for dash evaluation
            Core.Graphics.DrawText($"[DEBUG] CheckDashTerrain: player=({playerGridPos.X:F0},{playerGridPos.Y:F0}) target=({targetPosition.X:F0},{targetPosition.Y:F0})", new Vector2(10, 320), SharpDX.Color.Orange);
            var distance = Vector2.Distance(playerGridPos, targetPosition);
            var dir = targetPosition - playerGridPos;
            dir = System.Numerics.Vector2.Normalize(dir);

            var distanceBeforeWall = 0;
            var distanceInWall = 0;
            var shouldDash = false;
            var points = new List<System.Drawing.Point>();

            const int clearThreshold = 30;
            const int minWallDistance = 3;

            for (var i = 0; i < 300; i++)
            {
                var v2Point = playerGridPos + i * dir;
                var point = new System.Drawing.Point((int)(playerGridPos.X + i * dir.X),
                    (int)(playerGridPos.Y + i * dir.Y));

                if (points.Contains(point))
                    continue;
                if (Vector2.Distance(v2Point, targetPosition) < 2)
                    break;

                points.Add(point);

                if (point.X < 0 || point.X >= _numCols || point.Y < 0 || point.Y >= _numRows)
                    break;

                if (_tiles == null)
                    break;
                    
                var tile = _tiles[point.X, point.Y];

                if (tile == 255)
                {
                    shouldDash = false;
                    break;
                }
                else if (tile == 2)
                {
                    if (shouldDash)
                        distanceInWall++;
                    shouldDash = true;
                }
                else if (!shouldDash)
                {
                    distanceBeforeWall++;
                    if (distanceBeforeWall > clearThreshold)
                        break;
                }
            }

            if (distanceBeforeWall > clearThreshold || distanceInWall < minWallDistance)
                shouldDash = false;

            if (shouldDash)
            {
                return PerformDash(targetPosition, "terrain");
            }
            Core.Graphics.DrawText("[DEBUG] CheckDashTerrain: shouldDash=FALSE", new Vector2(10, 360), SharpDX.Color.Red);
            return false;
        }

        private bool HasBuffTaskForTarget(uint targetEntityId)
        {
            return _tasks.Any(task => task.Type == TaskNode.TaskNodeType.BuffLead && task.TargetEntityId == targetEntityId);
        }




        public void Render()
        {
            Core.Graphics.DrawText($"[DEBUG] Leader check: followTarget={(_followTarget != null ? "Found" : "Null")} lastTarget={_lastTargetPosition}", new Vector2(100, 80), SharpDX.Color.Magenta);
            Core.Graphics.DrawText($"[DEBUG] Leader branch: {_debugLeaderBranch}", new Vector2(100, 60), SharpDX.Color.Magenta);
            Core.Graphics.DrawText($"[DEBUG] Far details: {_debugFarDetails}", new Vector2(100, 40), SharpDX.Color.Magenta);
            var followerPos = Core.GameController.Player.GridPosNum;
            var leaderDist = _followTarget != null ? Vector2.Distance(followerPos, _followTarget.GridPosNum) : 0;
            // Core.Graphics.DrawText($"[DEBUG] Leader found at distance {leaderDist:F0}, ClearPath={Core.Settings.Follower.ClearPathDistance.Value}", new Vector2(100, 140), SharpDX.Color.Cyan);
            if (_tasks != null && _tasks.Count > 1)
                for (var i = 1; i < _tasks.Count; i++)
                {
                    var start = Controls.GetScreenClampedGridPos(_tasks[i - 1].WorldPosition);
                    var end = Controls.GetScreenClampedGridPos(_tasks[i].WorldPosition);
                    Core.Graphics.DrawLine(start, end, 2, SharpDX.Color.Pink);
                }

            var dist = _tasks != null && _tasks.Count > 0 ? Vector2.Distance(Core.GameController.Player.GridPosNum, _tasks.First().WorldPosition) : 0;
            var targetDist = _lastTargetPosition == Vector2.Zero ? "NA" : Vector2.Distance(Core.GameController.Player.GridPosNum, _lastTargetPosition).ToString();
            followerPos = Core.GameController.Player.GridPosNum;
            leaderDist = _followTarget != null ? Vector2.Distance(followerPos, _followTarget.GridPosNum) : 0;
            var canExecute = DateTime.Now > _nextBotAction ? "Ready" : "Waiting";
            var taskInfo = _tasks?.Count > 0 ? _tasks[0].Type.ToString() : "None";
            var taskCount = _tasks?.Count ?? 0;
            var directFollow = _isDirectFollowModeEnabled ? "ON" : "OFF";
            var levelableGemCount = GemHelper.GetLevelableGems().Count;
            var levelUpButtonType = GemHelper.GetLevelUpAllButtonTypeName();
            
            Core.Graphics.DrawText($"Follower: Leader='{Core.Settings.Follower.LeaderName}' Tasks={_tasks?.Count ?? 0} NextDist={dist:F0} TargetDist={targetDist} taskCount={taskCount}", new Vector2(100, 100), SharpDX.Color.White);
            Core.Graphics.DrawText($"LeaderDist={leaderDist:F0} FollowTarget={(_followTarget != null ? "Found" : "Lost")} CurrentTask={taskInfo} CanExecute={canExecute}", new Vector2(100, 120), SharpDX.Color.Yellow);
            Core.Graphics.DrawText($"DirectFollowMode={directFollow} (toggle: Shift)", new Vector2(100, 140), SharpDX.Color.LawnGreen);
            Core.Graphics.DrawText($"LevelableGems={levelableGemCount}", new Vector2(100, 160), SharpDX.Color.LightSkyBlue);
            Core.Graphics.DrawText($"LevelUpAllType={levelUpButtonType}", new Vector2(100, 180), SharpDX.Color.LightSkyBlue);
        }
    }

    public class TaskNode
    {
        public enum TaskNodeType
        {
            Movement,
            Transition,
            Loot,
            ClaimWaypoint,
            Combat,
            BuffLead,
            GemLevel,
            Ultimatum,
        }

        public Vector2 WorldPosition { get; set; }
        public TaskNodeType Type { get; set; }
        public int Bounds { get; set; }
        public int AttemptCount { get; set; }
        public uint? TargetEntityId { get; set; }
        public string? TargetLabel { get; set; }

        public TaskNode(Vector2 position, int bounds, TaskNodeType type = TaskNodeType.Movement, uint? targetEntityId = null, string? targetLabel = null)
        {
            WorldPosition = position;
            Type = type;
            Bounds = bounds;
            TargetEntityId = targetEntityId;
            TargetLabel = targetLabel;
        }
    }
}
