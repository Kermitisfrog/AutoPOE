using AutoPOE.Logic.Helpers;
using AutoPOE.Logic.Sequences;
using ExileCore;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class UltimatumTaskAction : IFollowerTaskAction
    {
        public void Execute(FollowerActionContext context, TaskNode task)
        {
            var ultimatumPanel = Core.GameController.IngameState.IngameUi.UltimatumPanel;
            if (ultimatumPanel == null || !ultimatumPanel.IsVisible)
            {
                context.Tasks.RemoveAt(0);
                return;
            }

            var leaderChoice = ultimatumPanel.ChoicesElements.FirstOrDefault(c => c.LockedVotes >= 1);
            if (leaderChoice == null)
                return; // Panel is up but leader hasn't voted yet — stay queued and wait.

            context.NextBotAction = DateTime.Now.AddMilliseconds(Core.Settings.Follower.Movement.BotInputFrequency.Value + context.Random.Next(Core.Settings.Follower.Movement.BotInputFrequency));

            var choiceCenter = leaderChoice.GetClientRect().Center;
            CursorHelper.SetCursorPosHuman2(new Vector2(choiceCenter.X, choiceCenter.Y));
            Thread.Sleep(context.Random.Next(50) + 75);
            Input.LeftDown();
            Thread.Sleep(context.Random.Next(30) + 20);
            Input.LeftUp();
            Thread.Sleep(context.Random.Next(100) + 150);

            var confirmButton = ultimatumPanel.ConfirmButton;
            if (confirmButton != null)
            {
                var confirmCenter = confirmButton.GetClientRect().Center;
                CursorHelper.SetCursorPosHuman2(new Vector2(confirmCenter.X, confirmCenter.Y));
                Thread.Sleep(context.Random.Next(50) + 75);
                Input.LeftDown();
                Thread.Sleep(context.Random.Next(30) + 20);
                Input.LeftUp();
            }

            context.Tasks.RemoveAt(0);
        }
    }
}
