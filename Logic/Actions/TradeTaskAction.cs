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
            if (!acceptedInvite)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            // Poll until the trade window appears; the UI needs time to open after accepting the invite.
            var tradeWindow = Core.GameController.IngameState.IngameUi?.TradeWindow;
            const int pollIntervalMs = 120;
            const int tradeWindowTimeoutMs = 1500;
            var elapsed = 0;
            while ((tradeWindow == null || !tradeWindow.IsVisible) && elapsed < tradeWindowTimeoutMs)
            {
                Thread.Sleep(pollIntervalMs + context.Random.Next(60));
                elapsed += pollIntervalMs;
                tradeWindow = Core.GameController.IngameState.IngameUi?.TradeWindow;
            }

            if (tradeWindow == null || !tradeWindow.IsVisible)
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
