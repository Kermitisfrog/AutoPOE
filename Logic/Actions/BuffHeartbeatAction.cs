using AutoPOE.Logic.Helpers;
using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class BuffHeartbeatAction
    {
        private readonly Dictionary<uint, DateTime> _nextBuffAttemptByTargetEntityId = new Dictionary<uint, DateTime>();

        public void Reset()
        {
            _nextBuffAttemptByTargetEntityId.Clear();
        }

        public bool TryMaintainTargets(Entity? followTarget, List<TaskNode> tasks, Random random, Action<Vector2> setCursorPosHuman2, Action<DateTime> setNextBotAction)
        {
            if (!Core.Settings.Follower.IsBuffEnabled.Value)
            {
                _nextBuffAttemptByTargetEntityId.Clear();
                return false;
            }

            var configuredBuffName = Core.Settings.Follower.BuffTargetBuffName.Value?.Trim();
            if (string.IsNullOrWhiteSpace(configuredBuffName))
                configuredBuffName = "critical_link_target";

            var extraBuffTargetName = Core.Settings.Follower.ExtraBuffTargetName.Value?.Trim();
            var extraBuffTarget = EntityHelper.GetPlayerEntityByName(extraBuffTargetName);
            if (followTarget != null && extraBuffTarget != null && extraBuffTarget.Id == followTarget.Id)
                extraBuffTarget = null;

            var activeTargetIds = new HashSet<uint>();
            if (followTarget != null)
                activeTargetIds.Add(followTarget.Id);
            if (extraBuffTarget != null)
                activeTargetIds.Add(extraBuffTarget.Id);

            CleanupTrackedBuffTargets(activeTargetIds);

            if (TryMaintainTarget(followTarget, GetBuffTargetLabel(followTarget, "leader"), configuredBuffName, random, setCursorPosHuman2, setNextBotAction))
                return true;

            if (TryMaintainTarget(extraBuffTarget, GetBuffTargetLabel(extraBuffTarget, extraBuffTargetName ?? "extra-target"), configuredBuffName, random, setCursorPosHuman2, setNextBotAction))
                return true;

            return false;
        }

        private bool TryMaintainTarget(Entity? target, string targetLabel, string buffName, Random random, Action<Vector2> setCursorPosHuman2, Action<DateTime> setNextBotAction)
        {
            if (target == null)
                return false;

            if (!_nextBuffAttemptByTargetEntityId.ContainsKey(target.Id))
                _nextBuffAttemptByTargetEntityId[target.Id] = DateTime.Now;

            var now = DateTime.Now;
            if (now < _nextBuffAttemptByTargetEntityId[target.Id])
                return false;

            var followerPosition = Core.GameController.Player.GridPosNum;
            var targetDistance = Vector2.Distance(followerPosition, target.GridPosNum);
            if (targetDistance >= Core.Settings.Follower.ClearPathDistance.Value)
                return false;

            var hasBuff = EntityHelper.HasBuff(target, buffName);
            var refreshDue = hasBuff;
            var sourceBuffName = GetSourceBuffName(buffName);

            setCursorPosHuman2(Controls.GetScreenClampedGridPos(target.GridPosNum));
            Thread.Sleep(25);

            Input.KeyDown(Core.Settings.Follower.BuffKey);
            Thread.Sleep(15 + random.Next(10));
            Input.KeyUp(Core.Settings.Follower.BuffKey);
            Core.ActionPerformed();

            Thread.Sleep(35 + random.Next(20));

            var targetBuffApplied = EntityHelper.HasBuff(target, buffName);
            var sourceBuffRefreshed = IsSourceBuffTimerHealthy(sourceBuffName);
            var buffApplied = targetBuffApplied && sourceBuffRefreshed;

            _nextBuffAttemptByTargetEntityId[target.Id] = buffApplied
                ? DateTime.Now.Add(GetBuffRefreshInterval())
                : DateTime.Now.AddMilliseconds(Math.Max(100, Core.Settings.Follower.BotInputFrequency.Value));

            setNextBotAction(DateTime.Now.AddMilliseconds(Math.Max(25, Core.Settings.Follower.BotInputFrequency.Value)));

            var castReason = refreshDue ? "refresh" : "missing";
            var castResult = buffApplied ? "confirmed" : "retry";
            Core.Graphics.DrawText($"[DEBUG] Buff heartbeat cast -> {targetLabel} reason={castReason} result={castResult} target={targetBuffApplied} sourceTimerOK={sourceBuffRefreshed}", new Vector2(100, 280), SharpDX.Color.Green);
            return true;
        }

        private static string GetSourceBuffName(string targetBuffName)
        {
            if (string.IsNullOrWhiteSpace(targetBuffName))
                return string.Empty;

            return targetBuffName.Replace("target", "source", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSourceBuffTimerHealthy(string sourceBuffName)
        {
            if (string.IsNullOrWhiteSpace(sourceBuffName))
                return false;

            try
            {
                var player = Core.GameController.Player;
                var sourceBuff = player.Buffs.FirstOrDefault(buff => string.Equals(buff.Name, sourceBuffName, StringComparison.OrdinalIgnoreCase));
                if (sourceBuff == null)
                    return false;

                return sourceBuff.Timer > 3f;
            }
            catch
            {
                return false;
            }
        }

        private static string GetBuffTargetLabel(Entity? target, string fallbackLabel)
        {
            if (target == null)
                return fallbackLabel;

            return !string.IsNullOrWhiteSpace(target.RenderName) ? target.RenderName : fallbackLabel;
        }

        private void CleanupTrackedBuffTargets(HashSet<uint> activeTargetIds)
        {
            var staleTargetIds = _nextBuffAttemptByTargetEntityId.Keys.Where(id => !activeTargetIds.Contains(id)).ToList();
            foreach (var staleTargetId in staleTargetIds)
                _nextBuffAttemptByTargetEntityId.Remove(staleTargetId);
        }

        private static TimeSpan GetBuffRefreshInterval()
        {
            return TimeSpan.FromSeconds(Math.Max(1, Core.Settings.Follower.BuffRefreshIntervalSeconds.Value));
        }
    }
}