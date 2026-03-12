using ExileCore;
using System;
using System.Numerics;

namespace AutoPOE
{
    public static class Controls
    {
        private static Random random = new Random();
        // Brings the game window to the foreground using Win32 API
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Brings the game window to the foreground (regain focus)
        /// </summary>
        public static void BringGameWindowToFront()
        {
            var windowHandle = Core.GameController.Window.Process.MainWindowHandle;
            if (windowHandle != IntPtr.Zero)
            {
                SetForegroundWindow(windowHandle);
            }
        }
        public static Vector2 GetScreenByWorldPos(Vector3 worldPos)
        {
            return Core.GameController.IngameState.Camera.WorldToScreen(worldPos);
        }

        public static Vector2 GetScreenByGridPos(Vector2 gridPosNum)
        {
            return Controls.GetScreenByWorldPos(Core.GameController.Game.IngameState.Data.ToWorldWithTerrainHeight(gridPosNum));
        }
        public static Vector2 GetScreenClampedGridPos(Vector2 gridPosNum)
        {
            var screenByGridPos = GetScreenByGridPos(gridPosNum);
            var windowRectangle = Core.GameController.Window.GetWindowRectangle();

            // Clamp in window-relative space so callers can always use SetCursorPosHuman2 safely.
            const float leftMargin = 10f;
            const float rightMargin = 10f;
            const float topMargin = 10f;
            const float bottomMargin = 130f;

            var minX = leftMargin;
            var maxX = Math.Max(minX + 1f, windowRectangle.Width - rightMargin);
            var minY = topMargin;
            var maxY = Math.Max(minY + 1f, windowRectangle.Height - bottomMargin);

            var inSafeBounds = screenByGridPos.X >= minX && screenByGridPos.X <= maxX &&
                               screenByGridPos.Y >= minY && screenByGridPos.Y <= maxY;
            if (inSafeBounds)
                return screenByGridPos;

            var safeCenter = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            var delta = screenByGridPos - safeCenter;
            if (delta.LengthSquared() < float.Epsilon)
                return safeCenter;

            var direction = Vector2.Normalize(delta);
            var projected = safeCenter + direction * (float)(int)Core.Settings.ClampSize;

            return new Vector2(
                Math.Clamp(projected.X, minX, maxX),
                Math.Clamp(projected.Y, minY, maxY));
        }

        public static bool ReleaseAllModifierKeys()
        {
            var isKeyDown = Input.IsKeyDown(Keys.ControlKey) || Input.IsKeyDown(Keys.ShiftKey) || Input.IsKeyDown(Keys.Menu);

            Input.KeyUp(Keys.ControlKey);
            Input.KeyUp(Keys.ShiftKey);
            Input.KeyUp(Keys.Menu);

            return isKeyDown;
        }
        public static async Task ClosePanels()
        {
            if (ReleaseAllModifierKeys())
                await Task.Delay(250);

            if (Core.GameController.IngameState.IngameUi.InventoryPanel.IsVisible ||
                Core.GameController.IngameState.IngameUi.Cursor.Action == ExileCore.Shared.Enums.MouseActionType.UseItem)
                await UseKey(Keys.Escape);
        }

        /// <summary>
        /// Sets cursor position relative to the game window, making it resolution and window position independent
        /// </summary>
        private static void SetCursorPosWindowAware(Vector2 position)
        {
            var windowRect = Core.GameController.Window.GetWindowRectangle();
            var absoluteX = (int)(windowRect.X + position.X);
            var absoluteY = (int)(windowRect.Y + position.Y);

            Input.SetCursorPos(new Vector2(absoluteX, absoluteY));
        }


        public static async Task ClickScreenPos(Vector2 position, bool isLeft = true, bool exactPosition = false, bool holdCtrl = false)
        {
            if (!exactPosition)
                position += new Vector2((float)random.Next(-15, 15), (float)random.Next(-15, 15));

            SetCursorPosWindowAware(position);
            await Task.Delay(random.Next(20, 50));

            if (holdCtrl)
            {
                Input.KeyDown(Keys.LControlKey);
                await Task.Delay(random.Next(20, 50));
            }

            if (isLeft)
                await LeftClick();
            else
                await RightClick();

            if (Input.IsKeyDown(Keys.LControlKey))
                Input.KeyUp(Keys.LControlKey);

            await Task.Delay(random.Next(30, 75));
            Core.ActionPerformed();
        }

        public static async Task UseKeyAtGridPos(Vector2 pos, Keys key, bool exactPosition = false)
        {
            var screenClampedGridPos = GetScreenClampedGridPos(pos);
            if (!exactPosition)
                screenClampedGridPos += new Vector2((float)random.Next(-5, 5), (float)random.Next(-5, 5));

            SetCursorPosWindowAware(screenClampedGridPos);
            await Task.Delay(random.Next(15, 30));
            await UseKey(key);
            Core.ActionPerformed();
        }

        public static async Task UseKey(Keys key, int minDelay = 0)
        {
            Input.KeyDown(key);
            await Task.Delay(minDelay + random.Next(15, 30));
            Input.KeyUp(key);
            Core.ActionPerformed();
        }

        public static async Task RightClick()
        {
            Input.RightDown();
            await Task.Delay(random.Next(10, 50));
            Input.RightUp();
            Core.ActionPerformed();
        }
        public static async Task LeftClick()
        {
            Input.LeftDown();
            await Task.Delay(random.Next(10, 50));
            Input.LeftUp();
            Core.ActionPerformed();
        }
        public static async Task SendChatMessage(string message)
        {
            await Controls.UseKey(Keys.Enter);
            await Task.Delay(150);
            string sanitizedMessage = message.Replace("+", "{+}")
                                             .Replace("^", "{^}")
                                             .Replace("%", "{%}")
                                             .Replace("~", "{~}")
                                             .Replace("(", "{(}")
                                             .Replace(")", "{)}")
                                             .Replace("{", "{{}")
                                             .Replace("}", "{}}");
            SendKeys.SendWait(sanitizedMessage);
            await Task.Delay(150);

            await Controls.UseKey(Keys.Enter);
            await Task.Delay(100);
            Core.ActionPerformed();
        }

    }
}
