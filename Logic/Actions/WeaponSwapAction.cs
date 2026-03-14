using ExileCore;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System;
using System.Linq;
using System.Threading;

namespace AutoPOE.Logic.Actions
{
    public sealed class WeaponSwapAction
    {
        public void TryMaintain(Random random)
        {
            if (!Core.Settings.Follower.IsWeaponSwapEnabled.Value)
                return;

            var player = Core.GameController.Player;
            var hasSmiteBuff = player.Buffs.Any(buff => buff.Name == "smite_buff");
            if (!player.Stats.TryGetValue(GameStat.MainHandWeaponType, out var weaponType))
                return;

            var shouldSwapToTrypanon = hasSmiteBuff && weaponType != 4;
            var shouldSwapBack = !hasSmiteBuff && weaponType == 4;
            if (!shouldSwapToTrypanon && !shouldSwapBack)
                return;

            Input.KeyDown(Core.Settings.Follower.WeaponSwapKey);
            Thread.Sleep(random.Next(15) + 10);
            Input.KeyUp(Core.Settings.Follower.WeaponSwapKey);
            Thread.Sleep(random.Next(15) + 10);
        }
    }
}