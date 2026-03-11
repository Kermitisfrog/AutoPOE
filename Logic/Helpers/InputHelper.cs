using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using System;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Helpers
{
    public static class CursorHelper
    {
        public static void SetCursorPosHuman2(Vector2 vec)
        {
            var windowRect = Core.GameController.Window.GetWindowRectangle();
            var absoluteX = (int)(windowRect.X + vec.X);
            var absoluteY = (int)(windowRect.Y + vec.Y);
            Input.SetCursorPos(new Vector2(absoluteX, absoluteY));
        }

        public static void MouseoverItem(Entity item, Random random)
        {
            var uiLoot = Core.GameController.IngameState.IngameUi.ItemsOnGroundLabels.FirstOrDefault(I => I.IsVisible && I.ItemOnGround.Id == item.Id);
            if (uiLoot != null)
            {
                var clickPos = uiLoot.Label.GetClientRect().Center;
                var windowRect = Core.GameController.Window.GetWindowRectangle();
                Input.SetCursorPos(new Vector2(
                    clickPos.X + random.Next(-15, 15) + (int)windowRect.X,
                    clickPos.Y + random.Next(-10, 10) + (int)windowRect.Y));
                Thread.Sleep(30 + random.Next(Core.Settings.Follower.BotInputFrequency));
            }
        }

        public static void ClickLevelableGem(Element clickableElement, Random random)
        {
            if (!clickableElement.IsVisible)
                return;

            var windowTopLeft = Core.GameController.Window.GetWindowRectangleTimeCache.TopLeft;
            var center = clickableElement.GetClientRectCache.Center;
            Input.SetCursorPos(new Vector2(windowTopLeft.X + center.X, windowTopLeft.Y + center.Y));
            Thread.Sleep(20 + random.Next(20));
            Input.LeftDown();
            Thread.Sleep(15 + random.Next(15));
            Input.LeftUp();
            Core.ActionPerformed();
        }
    }
}
