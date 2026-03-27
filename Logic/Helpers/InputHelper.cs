using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using System;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;

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
                .Select(label =>
                {
                    WorldItem? worldItemComponent;
                    try
                    {
                        worldItemComponent = label.ItemOnGround.GetComponent<WorldItem>();
                    }
                    catch
                    {
                        worldItemComponent = null;
                    }

                    return new { Label = label, GroundItem = label.ItemOnGround, WorldItem = worldItemComponent };
                })
                .Where(x =>
                {
                    return x.WorldItem != null && !x.WorldItem.AllocatedToSomeoneElse;
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

        public static object? GetPendingTradeInviteEntry(string leaderAccountName)
        {
            if (string.IsNullOrWhiteSpace(leaderAccountName))
                return null;

            try
            {
                var ingameUi = Core.GameController.IngameState.IngameUi;
                var invitesPanel = GetPropertyValue<object>(ingameUi, "InvitesPanel");
                if (invitesPanel == null)
                    return null;

                var invites = GetPropertyValue<System.Collections.IEnumerable>(invitesPanel, "Invites");
                if (invites == null)
                    return null;

                foreach (var inviteEntry in invites)
                {
                    if (inviteEntry == null)
                        continue;

                    var actionText = GetPropertyValue<string>(inviteEntry, "ActionText");
                    if (!string.Equals(actionText, "sent you a trade request", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var accountName = GetPropertyValue<string>(inviteEntry, "AccountName");
                    if (!string.Equals(accountName, leaderAccountName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return inviteEntry;
                }
            }
            catch (Exception ex)
            {
                Core.LogError("CursorHelper.GetPendingTradeInviteEntry", ex);
            }

            return null;
        }

        public static bool ClickTradeInviteAccept(object inviteEntry, Random random)
        {
            try
            {
                if (inviteEntry == null)
                    return false;

                // Retry briefly because the invite window can appear/interpolate over multiple ticks.
                const int maxAttempts = 8;
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    Thread.Sleep(attempt == 0 ? 120 + random.Next(80) : 55 + random.Next(55));

                    var acceptButton = GetPropertyValue<Element>(inviteEntry, "AcceptButton");
                    if (acceptButton == null || !acceptButton.IsVisible)
                        continue;

                    var center = acceptButton.Center;
                    if (center.X <= 0 || center.Y <= 0)
                        continue;

                    Input.SetCursorPos(new Vector2(
                        center.X + random.Next(-2, 3),
                        center.Y + random.Next(-2, 3)));

                    Thread.Sleep(25 + random.Next(20));
                    Input.LeftDown();
                    Thread.Sleep(15 + random.Next(20));
                    Input.LeftUp();
                    Core.ActionPerformed();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Core.LogError("CursorHelper.ClickTradeInviteAccept", ex);
                return false;
            }
        }

        public static bool CtrlClickAllInventoryItemsForTrade(Random random)
        {
            try
            {
                var playerInventories = Core.GameController.IngameState.Data.ServerData.PlayerInventories;
                if (playerInventories == null || playerInventories.Count == 0)
                    return false;

                var inventory = playerInventories[0]?.Inventory;
                if (inventory == null)
                    return false;

                var items = inventory.InventorySlotItems;
                if (items == null || items.Count == 0)
                    return false;

                Input.KeyDown(Keys.LControlKey);
                Thread.Sleep(20 + random.Next(20));
                try
                {
                    foreach (var item in items)
                    {
                        if (item == null)
                            continue;

                        var center = item.GetClientRect().Center;

                        var windowRect = Core.GameController.Window.GetWindowRectangle();
                        Input.SetCursorPos(new Vector2(
                            center.X + random.Next(-2, 3) + (int)windowRect.X,
                            center.Y + random.Next(-2, 3) + (int)windowRect.Y));

                        Thread.Sleep(80 + random.Next(81));
                        Input.LeftDown();
                        Thread.Sleep(15 + random.Next(20));
                        Input.LeftUp();
                        Thread.Sleep(20 + random.Next(20));
                        Core.ActionPerformed();
                    }
                }
                finally
                {
                    Input.KeyUp(Keys.LControlKey);
                }

                return true;
            }
            catch (Exception ex)
            {
                Core.LogError("CursorHelper.CtrlClickAllInventoryItemsForTrade", ex);
                Input.KeyUp(Keys.LControlKey);
                return false;
            }
        }

        public static bool ClickTradeWindowAccept(Random random)
        {
            try
            {
                var ingameUi = Core.GameController.IngameState.IngameUi;
                var tradeWindow = GetPropertyValue<object>(ingameUi, "TradeWindow");
                if (tradeWindow == null)
                    return false;

                var acceptButton = GetPropertyValue<Element>(tradeWindow, "AcceptButton");
                if (acceptButton == null || !acceptButton.IsVisible)
                    return false;

                var center = acceptButton.GetClientRect().Center;
                var windowRect = Core.GameController.Window.GetWindowRectangle();
                Input.SetCursorPos(new Vector2(
                    center.X + random.Next(-2, 3) + (int)windowRect.X,
                    center.Y + random.Next(-2, 3) + (int)windowRect.Y));

                Thread.Sleep(20 + random.Next(20));
                Input.LeftDown();
                Thread.Sleep(15 + random.Next(20));
                Input.LeftUp();
                Core.ActionPerformed();
                return true;
            }
            catch (Exception ex)
            {
                Core.LogError("CursorHelper.ClickTradeWindowAccept", ex);
                return false;
            }
        }

        private static T? GetPropertyValue<T>(object source, string propertyName) where T : class
        {
            var property = source.GetType().GetProperty(propertyName);
            if (property == null)
                return null;

            return property.GetValue(source) as T;
        }
    }
}
