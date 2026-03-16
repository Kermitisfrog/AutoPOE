using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System;
using System.Linq;

namespace AutoPOE.Logic.Helpers
{
    public static class EntityHelper
    {
        private static Entity? FindLootableWorldItem(Func<Entity, WorldItem, bool> predicate)
        {
            try
            {
                return Core.GameController.EntityListWrapper.Entities
                    .Where(e => e.Type == EntityType.WorldItem)
                    .Where(e => e.IsTargetable)
                    .Select(e => new { Entity = e, WorldItem = e.GetComponent<WorldItem>() })
                    .Where(x => x.WorldItem != null)
                    .Select(x => x.Entity)
                    .FirstOrDefault(e =>
                    {
                        var worldItem = e.GetComponent<WorldItem>();
                        return worldItem != null && predicate(e, worldItem);
                    });
            }
            catch
            {
                return null;
            }
        }

        public static Entity? GetFollowingTarget()
        {
            var leaderName = Core.Settings.Follower.LeaderName.Value?.Trim();
            return GetPlayerEntityByName(leaderName);
        }

        public static Entity? GetPlayerEntityByName(string? playerNameToFind)
        {
            if (string.IsNullOrEmpty(playerNameToFind))
                return null;

            var playerNameLower = playerNameToFind.ToLowerInvariant();

            try
            {
                // During/after zone transitions some player entities can have temporarily invalid components.
                // Iterate defensively so one bad entity does not prevent reacquiring the actual leader.
                foreach (var playerEntity in Core.GameController.EntityListWrapper.Entities.Where(x => x.Type == EntityType.Player))
                {
                    try
                    {
                        var playerComponent = playerEntity.GetComponent<Player>();
                        var playerName = playerComponent?.PlayerName;
                        if (!string.IsNullOrEmpty(playerName) && playerName.ToLowerInvariant() == playerNameLower)
                            return playerEntity;

                        var renderName = playerEntity.RenderName;
                        if (!string.IsNullOrEmpty(renderName) && renderName.ToLowerInvariant() == playerNameLower)
                            return playerEntity;
                    }
                    catch
                    {
                        // Ignore malformed entities and continue searching.
                    }
                }

                foreach (var playerEntity in Core.GameController.Entities.Where(x => x.Type == EntityType.Player))
                {
                    try
                    {
                        var playerComponent = playerEntity.GetComponent<Player>();
                        var playerName = playerComponent?.PlayerName;
                        if (!string.IsNullOrEmpty(playerName) && playerName.ToLowerInvariant() == playerNameLower)
                            return playerEntity;

                        var renderName = playerEntity.RenderName;
                        if (!string.IsNullOrEmpty(renderName) && renderName.ToLowerInvariant() == playerNameLower)
                            return playerEntity;
                    }
                    catch
                    {
                        // Ignore malformed entities and continue searching.
                    }
                }
            }
            catch (Exception ex)
            {
                Core.LogError("EntityHelper.GetPlayerEntityByName", ex);
            }

            return null;
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

        public static Entity? GetLootableQuestItem()
        {
            return FindLootableWorldItem((_, worldItem) =>
            {
                Entity itemEntity = worldItem.ItemEntity;
                var className = Core.GameController.Files.BaseItemTypes.Translate(itemEntity.Path).ClassName;
                var icon = itemEntity.GetComponent<WorldItem>()?.Icon;
                return className == "QuestItem" || icon == MapIconsIndex.LootFilterLargeGreenPentagon;
            });
        }

        public static Entity? GetLootableQuestItemById(uint? entityId)
        {
            if (entityId == null)
                return null;

            return FindLootableWorldItem((entity, worldItem) =>
            {
                if (entity.Id != entityId.Value)
                    return false;

                Entity itemEntity = worldItem.ItemEntity;
                var className = Core.GameController.Files.BaseItemTypes.Translate(itemEntity.Path).ClassName;
                var icon = itemEntity.GetComponent<WorldItem>()?.Icon;
                return className == "QuestItem" || icon == MapIconsIndex.LootFilterLargeGreenPentagon;
            });
        }

        public static Entity? GetLootableRegularItem()
        {
            return FindLootableWorldItem((_, worldItem) =>
                !worldItem.AllocatedToSomeoneElse &&
                worldItem.IsPermanentlyAllocated &&
                worldItem.AllocatedToPlayer != 0);
        }

        public static Entity? GetLootableRegularItemById(uint? entityId)
        {
            if (entityId == null)
                return null;

            return FindLootableWorldItem((entity, worldItem) =>
                entity.Id == entityId.Value &&
                !worldItem.AllocatedToSomeoneElse &&
                worldItem.IsPermanentlyAllocated &&
                worldItem.AllocatedToPlayer != 0);
        }

        public static Entity? GetNearbyHostileEnemy()
        {
            try
            {
                return Core.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                    .Where(m => m.IsHostile && m.IsTargetable && m.IsAlive &&
                               m.GridPosNum.Distance(Core.GameController.Player.GridPosNum) < Core.Settings.Follower.ClearPathDistance.Value)
                    .OrderBy(m => m.GridPosNum.Distance(Core.GameController.Player.GridPosNum))
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
    }
}
