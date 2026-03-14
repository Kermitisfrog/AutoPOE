using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class DirectFollowAction
    {
        private DateTime _nextDirectFollowDashAt = DateTime.MinValue;
        private bool _isEnabled;
        private bool _wasShiftDownLastTick;

        public bool IsEnabled => _isEnabled;

        public void Reset()
        {
            try
            {
                Input.KeyUp(Core.Settings.Follower.MovementKey);
            }
            catch
            {
                // Ignore unload/load timing issues where settings/input are not yet available.
            }

            _nextDirectFollowDashAt = DateTime.MinValue;
            _isEnabled = false;
            _wasShiftDownLastTick = false;
        }

        public void UpdateToggleState(bool isShiftDown, List<TaskNode> tasks)
        {
            if (isShiftDown && !_wasShiftDownLastTick)
                Toggle(tasks);

            _wasShiftDownLastTick = isShiftDown;
        }

        public void HandleTick(Entity? followTarget, List<TaskNode> tasks, Random random, Action<Vector2> setCursorPosHuman2, Func<bool> tryMaintainBuffTargets, Action<Vector2> setLastTargetPosition)
        {
            if (!_isEnabled)
                return;

            tasks.Clear();
            if (tryMaintainBuffTargets())
                return;

            if (followTarget == null)
                return;

            var targetPos = followTarget.GridPosNum;
            setCursorPosHuman2(Controls.GetScreenClampedGridPos(targetPos));
            setLastTargetPosition(targetPos);

            var followerPos = Core.GameController.Player.GridPosNum;
            var leaderDistance = Vector2.Distance(followerPos, targetPos);
            if (leaderDistance > Core.Settings.Follower.DashLeaderDistance.Value)
                TryDirectFollowDash(random);
        }

        private void Toggle(List<TaskNode> tasks)
        {
            _isEnabled = !_isEnabled;

            if (_isEnabled)
            {
                tasks.Clear();
                Input.KeyDown(Core.Settings.Follower.MovementKey);
            }
            else
            {
                Input.KeyUp(Core.Settings.Follower.MovementKey);
            }
        }

        private void TryDirectFollowDash(Random random)
        {
            if (DateTime.Now < _nextDirectFollowDashAt)
                return;

            Input.KeyDown(Core.Settings.Follower.DashKey);
            Thread.Sleep(15 + random.Next(15));
            Input.KeyUp(Core.Settings.Follower.DashKey);
            Core.ActionPerformed();

            _nextDirectFollowDashAt = DateTime.Now.AddMilliseconds(Math.Max(25, Core.Settings.Follower.BotInputFrequency.Value));
        }
    }
}