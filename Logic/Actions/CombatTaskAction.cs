using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.Shared.Helpers;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class CombatTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.Movement.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.Movement.BotInputFrequency));
            task.AttemptCount++;

            if (Core.GameController.Player.Buffs.Any(buff => buff.Name == "smite_buff"))
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            if (!Core.Settings.Follower.Combat.IsCombatEnabled.Value)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            var hostileEnemy = context.GetNearbyHostileEnemy();
            if (hostileEnemy == null || task.AttemptCount > 5)
            {
                Core.Graphics.DrawText($"[DEBUG] Combat task ended: Enemy found={hostileEnemy != null}, Attempts={task.AttemptCount}", new Vector2(100, 260), SharpDX.Color.Red);
                var cooldownMs = Core.Settings.Follower.Combat.CombatReengageDelay.Value;
                if (cooldownMs > 0)
                    context.SetCombatCooldown(DateTime.Now.AddMilliseconds(cooldownMs));
                context.Tasks.RemoveAt(0);
                return;
            }

            var enemyDistance = Vector2.Distance(Core.GameController.Player.GridPosNum, hostileEnemy.GridPosNum);
            if (enemyDistance >= Core.Settings.Follower.Movement.ClearPathDistance.Value)
            {
                Core.Graphics.DrawText($"[DEBUG] Enemy out of range: {enemyDistance:F0}", new Vector2(100, 260), SharpDX.Color.Red);
                var cooldownMs = Core.Settings.Follower.Combat.CombatReengageDelay.Value;
                if (cooldownMs > 0)
                    context.SetCombatCooldown(DateTime.Now.AddMilliseconds(cooldownMs));
                context.Tasks.RemoveAt(0);
                return;
            }

            var enemyScreenPos = Controls.GetScreenClampedGridPos(hostileEnemy.GridPosNum);
            context.SetCursorPosHuman2(enemyScreenPos);
            Thread.Sleep(25);

            Input.KeyDown(Core.Settings.Follower.Combat.CombatKey);
            Thread.Sleep(context.Random.Next(15) + 10);
            Input.KeyUp(Core.Settings.Follower.Combat.CombatKey);
            Thread.Sleep(context.Random.Next(15) + 10);

            Core.ActionPerformed();

            Core.Graphics.DrawText($"[DEBUG] Combat attack attempt {task.AttemptCount}", new Vector2(100, 260), SharpDX.Color.Red);
        }
    }
}
