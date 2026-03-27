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

            if (task.AttemptCount > 3)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            var inviteEntry = CursorHelper.GetPendingTradeInviteEntry(leaderAccountName);
            if (inviteEntry == null)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            var acceptedInvite = CursorHelper.ClickTradeInviteAccept(inviteEntry, context.Random);
            if (!acceptedInvite)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            Thread.Sleep(180 + context.Random.Next(140));
            _ = CursorHelper.CtrlClickAllInventoryItemsForTrade(context.Random);
            Thread.Sleep(120 + context.Random.Next(100));
            _ = CursorHelper.ClickTradeWindowAccept(context.Random);
            context.NextBotAction = DateTime.Now.AddMilliseconds(600 + context.Random.Next(300));

            context.Tasks.RemoveAt(0);
        }
    }
}
