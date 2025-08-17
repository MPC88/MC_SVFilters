using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MC_SVFilters
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        // BepInEx
        public const string pluginGuid = "mc.starvalor.filters";
        public const string pluginName = "SV Filters";
        public const string pluginVersion = "1.1.0";

        // Item Types
        internal const int itemTypeWeapon = 1;
        internal const int itemTypeEquipment = 2;
        internal const int itemTypeItem = 3;
        internal const int itemTypeShip = 4;
        internal const int itemTypeCrew = 5;

        // Debug
        public static ConfigEntry<bool> cfgDebug;
        internal static BepInEx.Logging.ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("SV Filters");

        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Main));
            Harmony.CreateAndPatchAll(typeof(InventoryFilter));
            Harmony.CreateAndPatchAll(typeof(CraftingFilter));

            cfgDebug = Config.Bind<bool>(
                "Debug",
                "Debug",
                false,
                "Debug");
        }

        internal static void FilterInputSelect(BaseEventData data)
        {
            if (cfgDebug.Value) log.LogInfo("Filter input select block");
            PlayerControl.inst.BlockControls(true);
            PlayerControl.inst.blockKeyboard = true;
        }

        internal static void FilterInputDeSelect(BaseEventData data)
        {
            if (cfgDebug.Value) log.LogInfo("Filter input deselect unblock");
            PlayerControl.inst.BlockControls(false);
            PlayerControl.inst.blockKeyboard = false;
        }
    }
}
