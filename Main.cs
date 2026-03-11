
using AutoPOE.Logic;
using AutoPOE.Logic.Sequences;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.Shared.Helpers;
using System;

namespace AutoPOE
{
    public class Main : BaseSettingsPlugin<Settings>
    {
        private readonly ISequence _followerSequence = new FollowerSequence();
        public override bool Initialise()
        {
            this.Name = "Auto POE";

            Core.Initialize(GameController, Settings, Graphics, this);

            return base.Initialise();
        }



        public override Job Tick()
        {
            if (!Core.IsBotRunning || !Settings.Enable || !GameController.InGame)
                return base.Tick();

            if (GameController.IsLoading)
                return base.Tick();

            if (Core.CanUseAction)
            {
                try
                {
                    _followerSequence.Tick();
                }
                catch (Exception ex)
                {
                    Core.IsBotRunning = false;
                    Core.LogError("Main.Tick", ex);
                }
            }

            return base.Tick();
        }

        public override void Render()
        {
            if (!Settings.Enable || !GameController.InGame || GameController.IngameState.Data == null)
                return;

            if (Settings.StartBot.PressedOnce())
                Core.IsBotRunning = !Core.IsBotRunning;

            try
            {
                _followerSequence.Render();
            }
            catch (Exception ex)
            {
                Core.LogError("Main.Render", ex);
            }
        }

        async public override void AreaChange(AreaInstance area)
        {
            // Initialize Follower terrain parsing
            if (_followerSequence is FollowerSequence followerSeq)
            {
                try
                {
                    followerSeq.Initialize();
                }
                catch (Exception ex)
                {
                    Core.IsBotRunning = false;
                    Core.LogError("Main.AreaChange", ex);
                }
            }
            await Task.Delay(100);
            Controls.BringGameWindowToFront();





        }
    }
}
