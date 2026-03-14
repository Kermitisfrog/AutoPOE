using ExileCore;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;

namespace AutoPOE
{
    public class Settings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(false);
        public HotkeyNode StartBot { get; set; } = (HotkeyNode)Keys.Insert;

        [Menu("Action Frequency", "What is the minimm time between inputs?")]
        public RangeNode<int> ActionFrequency { get; set; } = new RangeNode<int>(100, 25, 500);

        [Menu("Clamp Size")]
        public RangeNode<int> ClampSize { get; set; } = new RangeNode<int>(400, 100, 1000);

        public FollowerSettings Follower { get; set; } = new FollowerSettings();

        [Submenu(CollapsedByDefault = true)]
        public class FollowerSettings
        {
            [Menu("Leader Name", "Name of the player to follow.")]
            public TextNode LeaderName { get; set; } = new TextNode("");

            [Menu("Movement Key", "Skill hotkey for movement (e.g., dash, walk).")]
            public HotkeyNode MovementKey { get; set; } = (HotkeyNode)Keys.T;

            [Menu("Movement Frequency", "Delay in milliseconds between movement inputs.")]
            public RangeNode<int> BotInputFrequency { get; set; } = new RangeNode<int>(50, 10, 250);

            [Menu("Close Path Distance", "Range threshold: close=direct follow, far=pathfind. (Grid coordinates - use smaller values than world pos)")]
            public RangeNode<int> ClearPathDistance { get; set; } = new RangeNode<int>(100, 10, 500);

            [Menu("Leader Reacquire Delay", "Time to pause stale tasks after area load while waiting to find leader (ms).")]
            public RangeNode<int> LeaderReacquireDelayMs { get; set; } = new RangeNode<int>(4000, 0, 10000);

            [Menu("Allow Dash", "Allow automated dashing through walls based on terrain.")]
            public ToggleNode IsDashEnabled { get; set; } = new ToggleNode(true);

            [Menu("Dash Key", "Key to trigger dash/charge skill.")]
            public HotkeyNode DashKey { get; set; } = (HotkeyNode)Keys.W;

            [Menu("Dash Leader Distance", "Minimum distance to leader before forcing a dash toward leader. (Grid coordinates)")]
            public RangeNode<int> DashLeaderDistance { get; set; } = new RangeNode<int>(40, 5, 200);

            [Menu("Enable Combat Tasks", "Allow follower to create combat tasks when close to leader.")]
            public ToggleNode IsCombatEnabled { get; set; } = new ToggleNode(true);

            [Menu("Combat Reengage Delay", "Cooldown after combat task ends before reattempting (ms).")]
            public RangeNode<int> CombatReengageDelay { get; set; } = new RangeNode<int>(1500, 0, 5000);

            [Menu("Combat Leash Distance", "Max enemy distance from leader to allow combat task. (Grid coordinates)")]
            public RangeNode<int> CombatLeashDistance { get; set; } = new RangeNode<int>(60, 20, 200);

            [Menu("Combat Key", "Skill hotkey for attacking enemies while following.")]
            public HotkeyNode CombatKey { get; set; } = (HotkeyNode)Keys.LButton;

            [Menu("Enable Weapon Swap", "Allow follower to weapon swap before combat tasks.")]
            public ToggleNode IsWeaponSwapEnabled { get; set; } = new ToggleNode(true);

            [Menu("Weapon Swap Key", "Key to trigger weapon swap.")]
            public HotkeyNode WeaponSwapKey { get; set; } = (HotkeyNode)Keys.X;

            [Menu("Enable Buff Tasks", "Allow follower to cast buffs on the leader.")]
            public ToggleNode IsBuffEnabled { get; set; } = new ToggleNode(true);

            [Menu("Buff Key", "Skill hotkey for casting buffs on leader.")]
            public HotkeyNode BuffKey { get; set; } = (HotkeyNode)Keys.Z;

            [Menu("Buff Target Name", "Buff name to check on target before queuing buff task (e.g., critical_link_target).")]
            public TextNode BuffTargetBuffName { get; set; } = new TextNode("critical_link_target");

            [Menu("Buff Refresh Interval", "Seconds before proactively recasting buff on tracked targets.")]
            public RangeNode<int> BuffRefreshIntervalSeconds { get; set; } = new RangeNode<int>(8, 1, 30);

            [Menu("Extra Buff Target Name", "Optional additional player name to keep linked (PlayerName or RenderName).")]
            public TextNode ExtraBuffTargetName { get; set; } = new TextNode("");

            [Menu("Enable Gem Leveling", "Allow follower to auto level available skill gems.")]
            public ToggleNode IsGemLevelingEnabled { get; set; } = new ToggleNode(true);
        }
    }
}
