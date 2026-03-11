using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class BuffLeadTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.BotInputFrequency));
            task.AttemptCount++;

            if (!Core.Settings.Follower.IsBuffEnabled.Value)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            var buffTarget = ResolveBuffTarget(context, task);
            var buffTargetPosition = buffTarget?.GridPosNum ?? task.WorldPosition;

            if (buffTarget == null && buffTargetPosition == Vector2.Zero)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            var leaderDistance = Vector2.Distance(Core.GameController.Player.GridPosNum, buffTargetPosition);

            if (leaderDistance >= Core.Settings.Follower.ClearPathDistance.Value || task.AttemptCount > 3)
            {
                Core.Graphics.DrawText($"[DEBUG] Buff task ended: Leader distance={leaderDistance:F0}, Attempts={task.AttemptCount}", new Vector2(100, 280), SharpDX.Color.Green);
                context.Tasks.RemoveAt(0);
                return;
            }

            var leaderScreenPos = Controls.GetScreenClampedGridPos(buffTargetPosition);
            context.SetCursorPosHuman2(leaderScreenPos);
            Thread.Sleep(25);

            Input.KeyDown(Core.Settings.Follower.BuffKey);
            Thread.Sleep(context.Random.Next(5));
            Input.KeyUp(Core.Settings.Follower.BuffKey);
            Core.ActionPerformed();

            var debugTarget = !string.IsNullOrEmpty(task.TargetLabel) ? task.TargetLabel : "leader";
            Core.Graphics.DrawText($"[DEBUG] Buff cast attempt {task.AttemptCount} -> {debugTarget}", new Vector2(100, 280), SharpDX.Color.Green);
        }

        private static Entity? ResolveBuffTarget(FollowerActionContext context, TaskNode task)
        {
            try
            {
                if (task.TargetEntityId.HasValue)
                {
                    var byId = Core.GameController.EntityListWrapper.Entities.FirstOrDefault(entity => entity.Id == task.TargetEntityId.Value);
                    if (byId != null)
                        return byId;
                }
            }
            catch
            {
                // Fall back to follow target below.
            }

            return context.FollowTarget;
        }
    }
}
