using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.Shared.Helpers;
using System;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class MovementTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            var taskDistance = Vector2.Distance(Core.GameController.Player.GridPosNum, task.WorldPosition);
            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.Movement.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.Movement.BotInputFrequency));

            Core.Graphics.DrawText($"[DEBUG] Dash check: IsDashEnabled={Core.Settings.Follower.Movement.IsDashEnabled.Value}", new Vector2(10, 300), SharpDX.Color.Orange);
            if (!Core.GameController.Area.CurrentArea.IsTown)
                if (Core.Settings.Follower.Movement.IsDashEnabled.Value && context.CheckDashTerrain(task.WorldPosition))
                    return;

            context.SetCursorPosHuman2(Controls.GetScreenClampedGridPos(task.WorldPosition));
            Thread.Sleep(context.Random.Next(25) + 30);
            Input.KeyDown(Core.Settings.Follower.Movement.MovementKey);
            Thread.Sleep(context.Random.Next(25) + 30);
            Input.KeyUp(Core.Settings.Follower.Movement.MovementKey);
            Core.ActionPerformed();

            if (taskDistance <= Core.Settings.Follower.Movement.ClearPathDistance.Value)
                context.Tasks.RemoveAt(0);
        }
    }
}
