using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.Shared.Helpers;
using System;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class TransitionTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            var taskDistance = Vector2.Distance(Core.GameController.Player.GridPosNum, task.WorldPosition);
            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.BotInputFrequency.Value * 2 + context.Random.Next(Core.Settings.Follower.BotInputFrequency));
            var screenPos = Controls.GetScreenClampedGridPos(task.WorldPosition);
            if (taskDistance <= Core.Settings.Follower.ClearPathDistance.Value)
            {
                Input.KeyUp(Core.Settings.Follower.MovementKey);
                context.SetCursorPosHuman2(screenPos);
                Thread.Sleep(100);
                Input.LeftDown();
                Thread.Sleep(context.Random.Next(25) + 30);
                Input.LeftUp();
                Core.ActionPerformed();
                context.NextBotAction = DateTime.Now.AddSeconds(1);
            }
            else
            {
                context.SetCursorPosHuman2(screenPos);
                Thread.Sleep(context.Random.Next(25) + 30);
                Input.KeyDown(Core.Settings.Follower.MovementKey);
                Thread.Sleep(context.Random.Next(25) + 30);
                Input.KeyUp(Core.Settings.Follower.MovementKey);
                Core.ActionPerformed();
            }

            task.AttemptCount++;
            if (task.AttemptCount > 3)
                context.Tasks.RemoveAt(0);
        }
    }
}
