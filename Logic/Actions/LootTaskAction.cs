using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using System;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class LootTaskAction : IFollowerTaskAction
    {
        private static bool TryYieldToBuffs(FollowerActionContext context)
        {
            var buffHandled = context.TryMaintainBuffs();
            if (buffHandled)
                Core.Graphics.DrawText("[DEBUG] loot interrupted for buff refresh", new Vector2(100, 220), SharpDX.Color.GreenYellow);

            return buffHandled;
        }

        public void Execute(FollowerActionContext context, TaskNode task)
        {
            if (!Core.Settings.Follower.Items.IsLootEnabled.Value)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            if (task.Type == TaskNode.TaskNodeType.RegularItemLooting && context.FollowTarget != null)
            {
                var leaderDistance = Vector2.Distance(Core.GameController.Player.GridPosNum, context.FollowTarget.GridPosNum);
                if (leaderDistance >= Core.Settings.Follower.Movement.ClearPathDistance.Value)
                {
                    Core.Graphics.DrawText($"[DEBUG] Regular loot canceled: leader distance {leaderDistance:F0}", new Vector2(100, 220), SharpDX.Color.Yellow);
                    context.Tasks.RemoveAt(0);
                    return;
                }
            }

            if (TryYieldToBuffs(context))
                return;

            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.Movement.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.Movement.BotInputFrequency));
            task.AttemptCount++;

            // Locking onto one item can take several seconds of walking before it's in pickup range;
            // bound by elapsed time rather than attempt count so travel isn't mistaken for a stuck task.
            if (DateTime.Now - task.CreatedAt > TimeSpan.FromSeconds(15))
            {
                Core.Graphics.DrawText($"[DEBUG] loot removed: timed out after {task.AttemptCount} attempts", new Vector2(100, 220), SharpDX.Color.Yellow);
                context.Tasks.RemoveAt(0);
                return;
            }

            Input.KeyUp(Core.Settings.Follower.Movement.MovementKey);
            Thread.Sleep(Core.Settings.Follower.Movement.BotInputFrequency);

            if (TryYieldToBuffs(context))
                return;

            // Lock onto whichever item we click first, and keep clicking that same one until its label
            // disappears (picked up) instead of re-picking "closest" every attempt, which drifts as we walk.
            var (clicked, clickedEntityId) = context.ClickWorldItemLabel(task.TargetEntityId);

            if (!clicked)
            {
                var reason = task.TargetEntityId.HasValue ? "locked target picked up/gone" : "no visible loot";
                Core.Graphics.DrawText($"[DEBUG] loot task done: {reason}", new Vector2(100, 220), SharpDX.Color.Yellow);
                context.Tasks.RemoveAt(0);
                return;
            }

            task.TargetEntityId ??= clickedEntityId;
            Core.Graphics.DrawText($"[DEBUG] loot attempt {task.AttemptCount}: locked target={task.TargetEntityId}", new Vector2(100, 220), SharpDX.Color.Yellow);
        }
    }
}
