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
        private static string GetLootLabel(TaskNode task)
        {
            return task.Type == TaskNode.TaskNodeType.RegularItemLooting ? "regular loot" : "quest loot";
        }

        private static bool TryYieldToBuffs(FollowerActionContext context, string lootLabel)
        {
            var buffHandled = context.TryMaintainBuffs();
            if (buffHandled)
                Core.Graphics.DrawText($"[DEBUG] {lootLabel} interrupted for buff refresh", new Vector2(100, 220), SharpDX.Color.GreenYellow);

            return buffHandled;
        }

        public void Execute(FollowerActionContext context, TaskNode task)
        {
            var lootLabel = GetLootLabel(task);

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

            if (TryYieldToBuffs(context, lootLabel))
                return;

            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.Movement.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.Movement.BotInputFrequency));
            task.AttemptCount++;

            if (task.AttemptCount > 5)
            {
                Core.Graphics.DrawText($"[DEBUG] {lootLabel} removed: Attempts={task.AttemptCount}", new Vector2(100, 220), SharpDX.Color.Yellow);
                context.Tasks.RemoveAt(0);
                return;
            }

            Input.KeyUp(Core.Settings.Follower.Movement.MovementKey);
            Thread.Sleep(Core.Settings.Follower.Movement.BotInputFrequency);

            if (TryYieldToBuffs(context, lootLabel))
                return;

            var clickedLabel = context.ClickClosestVisibleWorldItemLabel();
            Core.Graphics.DrawText($"[DEBUG] {lootLabel} attempt {task.AttemptCount}: ClickedClosestVisibleWorldLabel={clickedLabel}", new Vector2(100, 220), SharpDX.Color.Yellow);

            if (clickedLabel)
            {
                context.NextBotAction = DateTime.Now.AddSeconds(1);
                context.Tasks.RemoveAt(0);
            }
        }
    }
}
