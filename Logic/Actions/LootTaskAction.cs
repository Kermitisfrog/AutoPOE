using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.PoEMemory.Components;
using System;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class LootTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.BotInputFrequency));
            task.AttemptCount++;
            var questLoot = context.GetLootableQuestItem();

            if (questLoot == null || task.AttemptCount > 5)
            {
                Core.Graphics.DrawText($"[DEBUG] Quest loot removed: Found={questLoot != null}, Attempts={task.AttemptCount}", new Vector2(100, 220), SharpDX.Color.Yellow);
                context.Tasks.RemoveAt(0);
                return;
            }

            var lootDistance = Vector2.Distance(Core.GameController.Player.GridPosNum, questLoot.GridPosNum);
            if (lootDistance >= Core.Settings.Follower.ClearPathDistance.Value)
            {
                Core.Graphics.DrawText($"[DEBUG] Quest loot out of range: {lootDistance:F0}", new Vector2(100, 220), SharpDX.Color.Yellow);
                context.Tasks.RemoveAt(0);
                return;
            }

            Input.KeyUp(Core.Settings.Follower.MovementKey);
            Thread.Sleep(Core.Settings.Follower.BotInputFrequency);

            var targetInfo = questLoot.GetComponent<Targetable>();
            Core.Graphics.DrawText($"[DEBUG] Loot attempt {task.AttemptCount}: Targeted={targetInfo?.isTargeted ?? false}, Distance={lootDistance:F0}", new Vector2(100, 220), SharpDX.Color.Yellow);

            if (targetInfo != null)
            {
                if (!targetInfo.isTargeted)
                {
                    context.MouseoverItem(questLoot);
                }
                else
                {
                    Thread.Sleep(25);
                    Input.LeftDown();
                    Thread.Sleep(25 + context.Random.Next(Core.Settings.Follower.BotInputFrequency));
                    Input.LeftUp();
                    Core.ActionPerformed();
                    context.NextBotAction = DateTime.Now.AddSeconds(1);
                    context.Tasks.RemoveAt(0);
                }
            }
        }
    }
}
