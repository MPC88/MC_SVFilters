using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;

namespace MC_SVFilters
{
    class CraftingFilter
    {
        // Star Valor modes, codes, fields and such
        // Crafting Panel
        private const int craftingPanelBlueprint = 1;
        private static readonly FieldInfo craftingcpCurrPanel = AccessTools.Field(typeof(CraftingPanelControl), "currPanel");
        private static readonly FieldInfo craftingpcBlueprintCrafting = AccessTools.Field(typeof(CraftingPanelControl), "blueprintCrafting");
        private static readonly FieldInfo bpcraftingBlueprintGO = AccessTools.Field(typeof(BlueprintCrafting), "blueprintsGO");
        private static readonly FieldInfo bpcraftingCategory = AccessTools.Field(typeof(BlueprintCrafting), "category");
        private static readonly FieldInfo bpcraftingItemPanel = AccessTools.Field(typeof(BlueprintCrafting), "itemPanel");

        // Mod
        private static InputField craftingFilterInput;
        private static BlueprintCrafting bpCraftingRef;

        [HarmonyPatch(typeof(CraftingPanelControl), nameof(CraftingPanelControl.OpenBlueprintCrafting))]
        [HarmonyPostfix]
        private static void CraftingPanelOpenBPCraft_Post(CraftingPanelControl __instance)
        {
            bpCraftingRef = (BlueprintCrafting)craftingpcBlueprintCrafting.GetValue(__instance);

            if (bpCraftingRef == null)
            {
                if (Main.cfgDebug.Value) Main.log.LogError("Open BP Panel - Null bpCraftingRef");
                return;
            }

            if (craftingFilterInput == null)
                CreateCraftingFilterUI(((GameObject)bpcraftingBlueprintGO.GetValue(bpCraftingRef)).transform);

            craftingFilterInput.gameObject.SetActive(true);
        }

        [HarmonyPatch(typeof(CraftingPanelControl), nameof(CraftingPanelControl.OpenShipEnhancement))]
        [HarmonyPostfix]
        private static void CraftingPanelOpenSE_Post()
        {
            if (craftingFilterInput != null)
                craftingFilterInput.gameObject.SetActive(false);
        }

        [HarmonyPatch(typeof(CraftingPanelControl), nameof(CraftingPanelControl.OpenWeaponCrafting))]
        [HarmonyPostfix]
        private static void CraftingPanelOpenWC_Post()
        {
            if (craftingFilterInput != null)
                craftingFilterInput.gameObject.SetActive(false);
        }

        private static void CreateCraftingFilterUI(Transform knownBuleprintsTrans)
        {
            InputField source = ((GameObject)AccessTools.Field(typeof(InputDialog), "panel").GetValue(InputDialog.inst)).transform.Find("TextInput").GetComponent<InputField>();
            craftingFilterInput = UnityEngine.Object.Instantiate<InputField>(source);
            craftingFilterInput.transform.SetParent(knownBuleprintsTrans.parent);
            craftingFilterInput.gameObject.layer = knownBuleprintsTrans.gameObject.layer;
            craftingFilterInput.transform.localPosition = knownBuleprintsTrans.transform.localPosition + new Vector3(-365, 310, 0);
            craftingFilterInput.transform.localScale = knownBuleprintsTrans.transform.localScale;
            craftingFilterInput.enabled = true;

            InputField.OnChangeEvent cfOnChangeEvent = new InputField.OnChangeEvent();
            cfOnChangeEvent.AddListener(DoCraftingFilter);
            craftingFilterInput.onValueChanged = cfOnChangeEvent;

            EventTrigger component = craftingFilterInput.GetComponentInChildren<EventTrigger>();

            EventTrigger.Entry selectTrig = new EventTrigger.Entry();
            selectTrig.eventID = EventTriggerType.Select;
            selectTrig.callback.AddListener((data) => { Main.FilterInputSelect(data); });
            component.triggers.RemoveAll(t => t.eventID == EventTriggerType.Select);
            component.triggers.Add(selectTrig);

            EventTrigger.Entry deselectTrig = new EventTrigger.Entry();
            deselectTrig.eventID = EventTriggerType.Deselect;
            deselectTrig.callback.AddListener((data) => { Main.FilterInputDeSelect(data); });
            component.triggers.RemoveAll(t => t.eventID == EventTriggerType.Deselect);
            component.triggers.Add(deselectTrig);
        }

        private static void DoCraftingFilter(string text)
        {
            if (Main.cfgDebug.Value) Main.log.LogInfo("DoCraftingFilter");

            if (bpCraftingRef == null)
            {
                if (Main.cfgDebug.Value) Main.log.LogError("Null bpCraftingRef");
                return;
            }

            BlueprintCraftingLoadData_Post(bpCraftingRef);
        }

        [HarmonyPatch(typeof(BlueprintCrafting), "LoadData")]
        [HarmonyPostfix]
        private static void BlueprintCraftingLoadData_Post(BlueprintCrafting __instance)
        {
            if (craftingFilterInput == null || !craftingFilterInput.gameObject.activeSelf)
                return;

            int category = (int)bpcraftingCategory.GetValue(__instance);
            Transform itemPanel = (Transform)bpcraftingItemPanel.GetValue(__instance);

            List<Blueprint> blueprints = PChar.Char.blueprints;
            if (blueprints == null)
            {
                return;
            }
            int i = 0;
            foreach (Blueprint item in blueprints)
            {
                if (IsBPFiltered(item))
                    continue;

                if ((category == 0 || item.itemType == category) && item.level >= 1)
                {
                    if (i >= itemPanel.childCount)
                    {
                        UnityEngine.Object.Instantiate(__instance.blueprintSlot).transform.SetParent(itemPanel, worldPositionStays: false);
                    }
                    itemPanel.GetChild(i).gameObject.SetActive(value: true);
                    itemPanel.GetChild(i).GetComponent<BlueprintSlot>().Setup(item, __instance);
                    i++;
                }
            }
            Vector2 sizeDelta = new Vector2(itemPanel.GetComponent<RectTransform>().sizeDelta.x, i * 18 + 5);
            itemPanel.GetComponent<RectTransform>().sizeDelta = sizeDelta;
            for (; i < itemPanel.childCount; i++)
            {
                itemPanel.GetChild(i).gameObject.SetActive(value: false);
            }
        }

        private static bool IsBPFiltered(Blueprint item)
        {
            if (craftingFilterInput.text.IsNullOrWhiteSpace())
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("Allowed blueprint.  No filter");
                return false;
            }

            string name = "";

            switch (item.itemType)
            {
                case Main.itemTypeWeapon:
                    name = GameData.data.weaponList[item.itemID].name;
                    break;
                case Main.itemTypeEquipment:
                    name = EquipmentDB.GetEquipment(item.itemID).equipName;
                    break;
                case Main.itemTypeItem:
                    name = ItemDB.GetItem(item.itemID).itemName;
                    break;
                case Main.itemTypeShip:
                    name = ShipDB.GetModel(item.itemID).modelName;
                    break;
            }

            if (name.ToLower().Contains(craftingFilterInput.text.ToLower()))
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("Allowed bp: " + name);
                return false;
            }

            if (Main.cfgDebug.Value) Main.log.LogInfo("Filtered bp: " + name);
            return true;
        }
    }
}
