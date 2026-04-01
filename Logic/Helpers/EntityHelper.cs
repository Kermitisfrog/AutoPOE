using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using System;
using System.Linq;
using System.Numerics;

namespace AutoPOE.Logic.Helpers
{
    public static class EntityHelper
    {
        public static Entity? GetFollowingTarget()
        {
            var leaderName = Core.Settings.Follower.Movement.LeaderName.Value?.Trim();
            if (string.IsNullOrWhiteSpace(leaderName))
                return null;

            return GetPlayerEntityByName(leaderName);
        }

        public static Entity? GetPlayerEntityByName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return null;

            try
            {
                var playerNameLower = playerName.Trim().ToLowerInvariant();

                foreach (var playerEntity in Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player])
                {
                    try
                    {
                        var playerComponent = playerEntity.GetComponent<Player>();
                        var resolvedName = playerComponent?.PlayerName;
                        if (!string.IsNullOrEmpty(resolvedName) && resolvedName.ToLowerInvariant() == playerNameLower)
                            return playerEntity;

                        var renderName = playerEntity.RenderName;
                        if (!string.IsNullOrEmpty(renderName) && renderName.ToLowerInvariant() == playerNameLower)
                            return playerEntity;
                    }
                    catch
                    {
                        // Skip malformed entities and continue searching.
                    }
                }

                foreach (var playerEntity in Core.GameController.Entities.Where(x => x.Type == EntityType.Player))
                {
                    try
                    {
                        var playerComponent = playerEntity.GetComponent<Player>();
                        var resolvedName = playerComponent?.PlayerName;
                        if (!string.IsNullOrEmpty(resolvedName) && resolvedName.ToLowerInvariant() == playerNameLower)
                            return playerEntity;

                        var renderName = playerEntity.RenderName;
                        if (!string.IsNullOrEmpty(renderName) && renderName.ToLowerInvariant() == playerNameLower)
                            return playerEntity;
                    }
                    catch
                    {
                        // Skip malformed entities and continue searching.
                    }
                }
            }
            catch (Exception ex)
            {
                Core.LogError("EntityHelper.GetPlayerEntityByName", ex);
            }

            return null;
        }

        public static Vector2 GetLeaderMovementTargetPosition(Entity leader)
        {
            if (!Core.Settings.Follower.Movement.IsPathToLeaderCursorEnabled.Value)
                return leader.GridPosNum;

            try
            {
                var pathfindingComponent = leader.GetComponent<Pathfinding>();
                var wantedMovePosition = pathfindingComponent?.WantMoveToPosition ?? Vector2.Zero;
                if (wantedMovePosition != Vector2.Zero)
                    return wantedMovePosition;
            }
            catch
            {
                // Fallback to leader grid position when pathfinding data is unavailable.
            }

            return leader.GridPosNum;
        }

        public static bool HasBuff(Entity? entity, string buffName)
        {
            if (entity == null || string.IsNullOrWhiteSpace(buffName))
                return false;

            try
            {
                return entity.Buffs.Any(buff => string.Equals(buff.Name, buffName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public static Entity? GetNearbyHostileEnemy()
        {
            try
            {
                return Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                    .Where(m => m.IsHostile && m.IsTargetable && m.IsAlive &&
                                System.Numerics.Vector2.Distance(m.GridPosNum, Core.GameController.Player.GridPosNum) < Core.Settings.Follower.Movement.ClearPathDistance.Value)
                    .OrderBy(m => System.Numerics.Vector2.Distance(m.GridPosNum, Core.GameController.Player.GridPosNum))
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
    }
}
