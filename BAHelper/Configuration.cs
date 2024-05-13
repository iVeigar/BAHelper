﻿using System;
using Dalamud.Configuration;

namespace BAHelper
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public void Save() => DalamudApi.PluginInterface!.SavePluginConfig(this);

        /*
         * Baldesion Arsenal
         */

        public bool IsCNMoogleDCPlayer = false;
        public bool UsePartyChannel = true;
        public bool CheckStatusProtect = true;
        public bool CheckStatusShell = true;
        public int ShieldRemainingTimeThreshold = 15; // minutes

        public bool AdvancedModeEnabled = false;
        public float TrapViewDistance = 100f;
        public bool DrawRecordedTraps = false;
        public bool DrawTrapBlastCircle = false;
        public bool DrawTrap15m = false;
        public bool DrawTrap36m = false;
        public bool DrawRecommendedScanningSpots = false;
        public bool DrawScanningSpot15m = false;
        public bool DrawScanningSpot36m = false;
        public bool DrawMobViews = true;
        public uint TrapBigBombColor = Color.Red;
        public uint TrapSmallBombColor = Color.Orange;
        public uint TrapPortalColor = Color.Green;
        public uint RevealedTrapColor = Color.TransBlack;
        public uint Trap15mCircleColor = Color.LightCyan;
        public uint Trap36mCircleColor = Color.DarkCyan;
        public uint ScanningSpotColor = Color.Cyan;
        public uint ScanningSpot15mCircleColor = Color.White;
        public uint ScanningSpot36mCircleColor = Color.White;
        public uint NormalAggroColor = Color.Brown;
        public uint SoundAggroColor = Color.Magenta;
        public bool DrawAreaBorder = false;
    }
}
