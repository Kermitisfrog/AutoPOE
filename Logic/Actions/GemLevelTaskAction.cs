using AutoPOE.Logic.Sequences;
using ExileCore;
using System;
using System.Linq;

namespace AutoPOE.Logic.Actions
{
    public sealed class GemLevelTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            if (!Core.Settings.Follower.IsGemLevelingEnabled.Value)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.BotInputFrequency));
            task.AttemptCount++;

            var clickableGems = context.GetLevelableGems();
            if (clickableGems.Count == 0 || task.AttemptCount > 5)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            var elementToClick = clickableGems.FirstOrDefault();
            if (elementToClick == null)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            context.ClickLevelableGem(elementToClick);

            context.Tasks.RemoveAt(0);
        }
    }
}
