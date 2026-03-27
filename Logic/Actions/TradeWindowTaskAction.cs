using AutoPOE.Logic.Helpers;
using AutoPOE.Logic.Sequences;
using ExileCore;
using System;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class TradeWindowTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            var tradeWindow = Core.GameController.IngameState.IngameUi.TradeWindow;
            if (!tradeWindow.IsVisible)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.Movement.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.Movement.BotInputFrequency));

            Thread.Sleep(180 + context.Random.Next(140));
            _ = CursorHelper.CtrlClickAllInventoryItemsForTrade(context.Random);
            Thread.Sleep(120 + context.Random.Next(100));
            _ = CursorHelper.ClickTradeWindowAccept(context.Random);
            context.NextBotAction = DateTime.Now.AddMilliseconds(600 + context.Random.Next(300));

            context.Tasks.RemoveAt(0);
        }
    }
}
