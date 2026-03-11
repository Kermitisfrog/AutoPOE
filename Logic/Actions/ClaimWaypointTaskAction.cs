using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.Shared.Helpers;
using System;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class ClaimWaypointTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            if (Vector2.Distance(Core.GameController.Player.GridPosNum, task.WorldPosition) > 35)
            {
                var screenPos = Controls.GetScreenClampedGridPos(task.WorldPosition);
                Input.KeyUp(Core.Settings.Follower.MovementKey);
                Thread.Sleep(Core.Settings.Follower.BotInputFrequency);
                context.SetCursorPosHuman2(screenPos);
                Thread.Sleep(100);
                Input.LeftDown();
                Thread.Sleep(context.Random.Next(25) + 30);
                Input.LeftUp();
                Core.ActionPerformed();
                context.NextBotAction = DateTime.Now.AddSeconds(1);
            }

            task.AttemptCount++;
            if (task.AttemptCount > 3)
                context.Tasks.RemoveAt(0);
        }
    }
}
