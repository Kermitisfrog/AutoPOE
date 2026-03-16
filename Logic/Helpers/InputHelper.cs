using ExileCore;
using ExileCore.PoEMemory.Components;
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

        public static bool ClickClosestVisibleWorldItemLabel(Random random, Vector2? leaderPos = null)
        {
            var visibleLabels = Core.GameController.IngameState.IngameUi.ItemsOnGroundLabelsVisible;
            if (visibleLabels == null)
                return false;

            var followerPos = Core.GameController.Player.GridPosNum;
            var maxDist = Core.Settings.Follower.Movement.ClearPathDistance.Value;

            var closestLabel = visibleLabels
                .Where(label => label?.ItemOnGround != null && label.Label != null)
                .Where(label => !(label.Label.Text ?? "").EndsWith("gold", StringComparison.OrdinalIgnoreCase))
                .Where(label => !leaderPos.HasValue || Vector2.Distance(leaderPos.Value, label.ItemOnGround.GridPosNum) <= maxDist)
                .Select(label => new { Label = label, GroundItem = label.ItemOnGround })
                .Where(x =>
                {
                    if (x.GroundItem.Type == ExileCore.Shared.Enums.EntityType.WorldItem)
                        return true;

                    try
                    {
                        return x.GroundItem.GetComponent<WorldItem>() != null;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .OrderBy(x => Vector2.Distance(followerPos, x.GroundItem.GridPosNum))
                .FirstOrDefault();

            if (closestLabel == null)
                return false;

            var clickPos = closestLabel.Label.Label.GetClientRect().Center;
            var windowRect = Core.GameController.Window.GetWindowRectangle();
            Input.SetCursorPos(new Vector2(
                clickPos.X + random.Next(-2, 3) + (int)windowRect.X,
                clickPos.Y + random.Next(-1, 2) + (int)windowRect.Y));
            Thread.Sleep(20 + random.Next(20));

            Input.LeftDown();
            Thread.Sleep(15 + random.Next(20));
            Input.LeftUp();
            Core.ActionPerformed();
            return true;
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
