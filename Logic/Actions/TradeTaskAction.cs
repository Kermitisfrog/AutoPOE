using AutoPOE.Logic.Helpers;
using AutoPOE.Logic.Sequences;
using ExileCore;
using System;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class TradeTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            if (!Core.Settings.Follower.Items.IsTradeEnabled.Value)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            var leaderAccountName = Core.Settings.Follower.Items.TradeLeaderAccountName.Value?.Trim();
            if (string.IsNullOrWhiteSpace(leaderAccountName))
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.Movement.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.Movement.BotInputFrequency));
            task.AttemptCount++;

            if (task.AttemptCount > 4)
            {
                context.Tasks.RemoveAt(0);
                return;
            }


            var acceptedInvite = CursorHelper.ClickTradeInviteAccept(leaderAccountName, context.Random);
            // Whether the invite click succeeded or not, remove this task — the TradeWindow
            // Tick check will create a TradeWindow task once the window actually opens.
            context.Tasks.RemoveAt(0);
        }
    }
}
