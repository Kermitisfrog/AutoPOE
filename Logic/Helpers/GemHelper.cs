using ExileCore;
using ExileCore.PoEMemory;
using System.Collections.Generic;
using System.Reflection;

namespace AutoPOE.Logic.Helpers
{
    public static class GemHelper
    {
        public static List<Element> GetLevelableGems()
        {
            var gemsToLevelUp = new List<Element>();

            var gemPanel = Core.GameController.IngameState.IngameUi?.GemLvlUpPanel;
            if (gemPanel == null)
                return gemsToLevelUp;

            var panelType = gemPanel.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var levelUpAllButton = panelType.GetProperty("LevelUpAllGemsButton", flags)?.GetValue(gemPanel)
                ?? panelType.GetField("LevelUpAllGemsButton", flags)?.GetValue(gemPanel);

            var clickableGem = levelUpAllButton as Element;

            if (clickableGem == null || !clickableGem.IsVisible)
                return gemsToLevelUp;

            gemsToLevelUp.Add(clickableGem);

            return gemsToLevelUp;
        }

        public static string GetLevelUpAllButtonTypeName()
        {
            var gemPanel = Core.GameController.IngameState.IngameUi?.GemLvlUpPanel;
            if (gemPanel == null)
                return "GemLvlUpPanel=null";

            var panelType = gemPanel.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var levelUpAllButton = panelType.GetProperty("LevelUpAllGemsButton", flags)?.GetValue(gemPanel)
                ?? panelType.GetField("LevelUpAllGemsButton", flags)?.GetValue(gemPanel);

            return levelUpAllButton?.GetType().FullName ?? "LevelUpAllGemsButton=null";
        }
    }
}
