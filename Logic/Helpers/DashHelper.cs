using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Threading;

namespace AutoPOE.Logic.Helpers
{
    public static class DashHelper
    {
        public static bool TryDashTerrain(Vector2 targetPosition, Entity? followTarget, byte[,]? tiles, int numCols, int numRows, Random random, Action<DateTime> setNextBotAction, Action<Vector2> setCursorPosHuman2)
        {
            var playerGridPos = Core.GameController.Player.GridPosNum;

            bool PerformDash(Vector2 dashTarget, string reason)
            {
                Core.Graphics.DrawText($"[DEBUG] CheckDashTerrain: dashReason={reason}", new Vector2(10, 340), SharpDX.Color.Lime);
                setNextBotAction(DateTime.Now.AddMilliseconds(500 + random.Next(Core.Settings.Follower.BotInputFrequency)));
                setCursorPosHuman2(Controls.GetScreenClampedGridPos(dashTarget));
                Thread.Sleep(50 + random.Next(Core.Settings.Follower.BotInputFrequency));
                Input.KeyDown(Core.Settings.Follower.DashKey);
                Thread.Sleep(15 + random.Next(Core.Settings.Follower.BotInputFrequency));
                Input.KeyUp(Core.Settings.Follower.DashKey);

                var moveTarget = followTarget?.GridPosNum ?? dashTarget;
                setCursorPosHuman2(Controls.GetScreenClampedGridPos(moveTarget));
                Thread.Sleep(30 + random.Next(Core.Settings.Follower.BotInputFrequency));
                Input.KeyDown(Core.Settings.Follower.MovementKey);
                Thread.Sleep(20 + random.Next(Core.Settings.Follower.BotInputFrequency));
                Input.KeyUp(Core.Settings.Follower.MovementKey);

                Core.ActionPerformed();
                return true;
            }

            if (followTarget != null)
            {
                var leaderDistance = Vector2.Distance(playerGridPos, followTarget.GridPosNum);
                if (leaderDistance > Core.Settings.Follower.DashLeaderDistance.Value)
                    return PerformDash(followTarget.GridPosNum, "leader-distance");
            }

            Core.Graphics.DrawText($"[DEBUG] CheckDashTerrain: player=({playerGridPos.X:F0},{playerGridPos.Y:F0}) target=({targetPosition.X:F0},{targetPosition.Y:F0})", new Vector2(10, 320), SharpDX.Color.Orange);
            var dir = targetPosition - playerGridPos;
            dir = Vector2.Normalize(dir);

            var distanceBeforeWall = 0;
            var distanceInWall = 0;
            var shouldDash = false;
            var points = new List<Point>();

            const int clearThreshold = 30;
            const int minWallDistance = 3;

            for (var i = 0; i < 300; i++)
            {
                var v2Point = playerGridPos + i * dir;
                var point = new Point((int)(playerGridPos.X + i * dir.X), (int)(playerGridPos.Y + i * dir.Y));

                if (points.Contains(point))
                    continue;
                if (Vector2.Distance(v2Point, targetPosition) < 2)
                    break;

                points.Add(point);

                if (point.X < 0 || point.X >= numCols || point.Y < 0 || point.Y >= numRows)
                    break;

                if (tiles == null)
                    break;

                var tile = tiles[point.X, point.Y];
                if (tile == 255)
                {
                    shouldDash = false;
                    break;
                }

                if (tile == 2)
                {
                    if (shouldDash)
                        distanceInWall++;
                    shouldDash = true;
                }
                else if (!shouldDash)
                {
                    distanceBeforeWall++;
                    if (distanceBeforeWall > clearThreshold)
                        break;
                }
            }

            if (distanceBeforeWall > clearThreshold || distanceInWall < minWallDistance)
                shouldDash = false;

            if (shouldDash)
                return PerformDash(targetPosition, "terrain");

            Core.Graphics.DrawText("[DEBUG] CheckDashTerrain: shouldDash=FALSE", new Vector2(10, 360), SharpDX.Color.Red);
            return false;
        }
    }
}