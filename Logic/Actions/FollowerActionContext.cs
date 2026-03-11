using AutoPOE.Logic.Sequences;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AutoPOE.Logic.Actions
{
    public sealed class FollowerActionContext
    {
        private readonly Func<DateTime> _getNextBotAction;
        private readonly Action<DateTime> _setNextBotAction;

        public FollowerActionContext(
            Random random,
            List<TaskNode> tasks,
            Entity? followTarget,
            Func<DateTime> getNextBotAction,
            Action<DateTime> setNextBotAction,
            Func<Vector2, bool> checkDashTerrain,
            Func<Entity?> getLootableQuestItem,
            Func<Entity?> getNearbyHostileEnemy,
            Func<List<Element>> getLevelableGems,
            Action<Entity> mouseoverItem,
            Action<Element> clickLevelableGem,
            Action<Vector2> setCursorPosHuman2,
            Action<DateTime> setCombatCooldown)
        {
            Random = random;
            Tasks = tasks;
            FollowTarget = followTarget;
            _getNextBotAction = getNextBotAction;
            _setNextBotAction = setNextBotAction;
            CheckDashTerrain = checkDashTerrain;
            GetLootableQuestItem = getLootableQuestItem;
            GetNearbyHostileEnemy = getNearbyHostileEnemy;
            GetLevelableGems = getLevelableGems;
            MouseoverItem = mouseoverItem;
            ClickLevelableGem = clickLevelableGem;
            SetCursorPosHuman2 = setCursorPosHuman2;
            SetCombatCooldown = setCombatCooldown;
        }

        public Random Random { get; }
        public List<TaskNode> Tasks { get; }
        public Entity? FollowTarget { get; }
        public Func<Vector2, bool> CheckDashTerrain { get; }
        public Func<Entity?> GetLootableQuestItem { get; }
        public Func<Entity?> GetNearbyHostileEnemy { get; }
        public Func<List<Element>> GetLevelableGems { get; }
        public Action<Entity> MouseoverItem { get; }
        public Action<Element> ClickLevelableGem { get; }
        public Action<Vector2> SetCursorPosHuman2 { get; }
        public Action<DateTime> SetCombatCooldown { get; }

        public DateTime NextBotAction
        {
            get => _getNextBotAction();
            set => _setNextBotAction(value);
        }
    }
}
