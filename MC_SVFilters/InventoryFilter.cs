using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MC_SVFilters
{
    class InventoryFilter
    {
        // Star Valor modes, codes, fields and such
        // Inventory / DockingUI
        private const int marketPanelCode = 2;
        private const int hangerPanelCode = 3;
        private const int cargoModeCargo = 0;
        private const int cargoModeCrew = 1;
        private const int cargoModeFleet = 2;
        private static readonly FieldInfo inventoryCargoMode = AccessTools.Field(typeof(Inventory), "cargoMode");
        private static readonly FieldInfo inventoryItemPanel = AccessTools.Field(typeof(Inventory), "itemPanel");
        private static readonly FieldInfo inventoryBtnCargo = AccessTools.Field(typeof(Inventory), "btnCargo");
        private static readonly FieldInfo inventoryBtnPassengers = AccessTools.Field(typeof(Inventory), "btnPassengers");
        private static readonly FieldInfo inventoryBtnStashMaterials = AccessTools.Field(typeof(Inventory), "btnStashMaterials");
        private static readonly MethodInfo inventoryAddItemSlot = AccessTools.Method(typeof(Inventory), "AddItemSlot");
        // Skills
        private static readonly Dictionary<int, string> crewPositions = new Dictionary<int, string>()
        {
            {-1, "None"},
            {0, "Engineer"},
            {1, "Pilot"},
            {2, "Navigator"},
            {3, "Supervisor"},
            {4, "Gunner"},
            {5, "Instructor"},
            {6, "Tactician"},
            {7, "Steward"},
            {8, "Adviser"},
            {9, "RelationsOfficer"},
            {10, "other1"},
            {11, "other2"},
            {12, "other3"},
            {13, "other4"},
            {14, "other5"},
            {15, "Co_Pilot"},
            {16, "FirstOfficer"},
            {17, "Primary"},
            {18, "Staff"},
            {19, "Captain"}
        };

        // Mod
        private static InputField invFilterInput;
        private static Inventory inventoryRef;

        [HarmonyPatch(typeof(DockingUI), nameof(DockingUI.OpenPanel))]
        [HarmonyPostfix]
        private static void DocingUIOpenPanel_Post(DockingUI __instance, Inventory ___inventory, int code)
        {
            if ((code == hangerPanelCode || code == marketPanelCode) && (int)inventoryCargoMode.GetValue(___inventory) < cargoModeFleet)
            {
                if (invFilterInput == null)
                    CreateInvfilterUI(___inventory.transform.Find("InventoryUI").Find("Credits"));

                inventoryRef = ___inventory;

                invFilterInput.gameObject.SetActive(true);
            }
            else
            {
                if (invFilterInput != null)
                    invFilterInput.gameObject.SetActive(false);
            }
        }

        private static void CreateInvfilterUI(Transform creditsTrans)
        {
            InputField source = ((GameObject)AccessTools.Field(typeof(InputDialog), "panel").GetValue(InputDialog.inst)).transform.Find("TextInput").GetComponent<InputField>();
            invFilterInput = UnityEngine.Object.Instantiate<InputField>(source);
            invFilterInput.transform.SetParent(creditsTrans.parent);
            invFilterInput.gameObject.layer = creditsTrans.gameObject.layer;
            invFilterInput.transform.localPosition = creditsTrans.transform.localPosition + new Vector3(115, 15, 0);
            invFilterInput.transform.localScale = creditsTrans.transform.localScale;
            invFilterInput.enabled = true;

            InputField.OnChangeEvent ifOnChangeEvent = new InputField.OnChangeEvent();
            ifOnChangeEvent.AddListener(DoInvFilter);
            invFilterInput.onValueChanged = ifOnChangeEvent;

            EventTrigger component = invFilterInput.GetComponentInChildren<EventTrigger>();

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

        private static void DoInvFilter(string text)
        {
            if (Main.cfgDebug.Value) Main.log.LogInfo("DoInvFilter");

            if (inventoryRef == null)
            {
                if (Main.cfgDebug.Value) Main.log.LogError("Null inventoryRef");
                return;
            }

            InventoryLoadItems_Post(inventoryRef);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.LoadItems))]
        [HarmonyPostfix]
        private static void InventoryLoadItems_Post(Inventory __instance)
        {
            if (invFilterInput == null || !invFilterInput.gameObject.activeSelf)
                return;

            string text = "";
            int i = 1;

            int cargoMode = (int)inventoryCargoMode.GetValue(__instance);
            if (cargoMode == cargoModeFleet)
                return;

            CargoSystem cs = PlayerControl.inst.GetCargoSystem;
            Transform itemPanel = (Transform)inventoryItemPanel.GetValue(__instance);
            Transform btnCargo = (Transform)inventoryBtnCargo.GetValue(__instance);
            Transform btnPassengers = (Transform)inventoryBtnPassengers.GetValue(__instance);
            GameObject btnStashMaterials = (GameObject)inventoryBtnStashMaterials.GetValue(__instance);

            foreach (CargoItem item in cs.cargo)
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("================CargoSystem loop round 1================");
                if (item.stockStationID == -1 && ((item.itemType == Main.itemTypeCrew) ^ (cargoMode == cargoModeCargo)))
                {
                    if (item.itemType == Main.itemTypeCrew && IsCrewFiltered(item))
                        continue;
                    if (item.itemType < Main.itemTypeCrew && IsCargoItemFiltered(item))
                        continue;

                    if (i >= itemPanel.childCount)
                    {
                        inventoryAddItemSlot.Invoke(__instance, null);
                    }
                    else
                    {
                        itemPanel.GetChild(i).gameObject.SetActive(value: true);
                    }

                    InventorySlot component = itemPanel.GetChild(i).GetComponent<InventorySlot>();
                    itemPanel.GetChild(i).transform.GetChild(0).GetComponentInChildren<Text>().text = item.GetNameWithQnt();
                    component.SetTextAlignLeft();
                    itemPanel.GetChild(i).transform.GetChild(0).GetChild(2).GetComponent<Image>()
                        .sprite = item.GetSprite();
                    itemPanel.GetChild(i).transform.GetChild(0).GetChild(2).GetComponent<Image>()
                        .enabled = true;
                    itemPanel.GetChild(i).transform.GetChild(0).GetComponent<Button>().interactable = true;
                    component.itemIndex = cs.cargo.IndexOf(item);
                    component.slotIndex = i;
                    component.isFleet = false;
                    i++;
                }
            }

            if (__instance.currStation != null)
            {
                if (i >= itemPanel.childCount)
                {
                    inventoryAddItemSlot.Invoke(__instance, null);
                }
                else
                {
                    itemPanel.GetChild(i).gameObject.SetActive(value: true);
                }
                InventorySlot component = itemPanel.GetChild(i).GetComponent<InventorySlot>();
                text = ColorSys.mediumGray + "<size=15>" + Lang.Get(0, 177) + "</size></color>";
                itemPanel.GetChild(i).transform.GetChild(0).GetComponentInChildren<Text>().text = text;
                component.SetTextAlignCenter();
                itemPanel.GetChild(i).transform.GetChild(0).GetChild(2).GetComponent<Image>()
                    .enabled = false;
                itemPanel.GetChild(i).transform.GetChild(0).GetComponent<Button>().interactable = false;
                component.itemIndex = -1;
                component.isFleet = false;
                i++;
                if (cargoMode < cargoModeFleet)
                {
                    foreach (CargoItem item3 in cs.cargo)
                    {
                        if (Main.cfgDebug.Value) Main.log.LogInfo("================CargoSystem loop round 2================");
                        if (item3.itemType == Main.itemTypeCrew && IsCrewFiltered(item3))
                            continue;
                        if (item3.itemType < Main.itemTypeCrew && IsCargoItemFiltered(item3))
                            continue;

                        if (item3.stockStationID == __instance.currStation.id && ((item3.itemType == Main.itemTypeCrew) ^ (cargoMode == cargoModeCargo)))
                        {
                            if (i >= itemPanel.childCount)
                            {
                                inventoryAddItemSlot.Invoke(__instance, null);
                            }
                            component = itemPanel.GetChild(i).GetComponent<InventorySlot>();
                            if (item3.itemType != 99)
                            {
                                itemPanel.GetChild(i).gameObject.SetActive(value: true);
                                itemPanel.GetChild(i).transform.GetChild(0).GetComponentInChildren<Text>().text = item3.GetNameWithQnt();
                                component.SetTextAlignLeft();
                                itemPanel.GetChild(i).transform.GetChild(0).GetChild(2).GetComponent<Image>()
                                    .sprite = item3.GetSprite();
                                itemPanel.GetChild(i).transform.GetChild(0).GetChild(2).GetComponent<Image>()
                                    .enabled = true;
                                itemPanel.GetChild(i).transform.GetChild(0).GetComponent<Button>().interactable = true;
                                component.itemIndex = cs.cargo.IndexOf(item3);
                                component.slotIndex = i;
                                component.isFleet = false;
                            }
                            else
                            {
                                itemPanel.GetChild(i).gameObject.SetActive(value: false);
                            }
                            i++;
                        }
                    }
                }
            }
            if (__instance.currStation != null && cargoMode == cargoModeCargo)
            {
                for (int j = 1; j < 4; j++)
                {
                    if (i >= itemPanel.childCount)
                    {
                        inventoryAddItemSlot.Invoke(__instance, null);
                    }
                    else
                    {
                        itemPanel.GetChild(i).gameObject.SetActive(value: true);
                    }
                    InventorySlot component = itemPanel.GetChild(i).GetComponent<InventorySlot>();
                    text = ColorSys.mediumGray + "<size=15>" + Lang.Get(0, 352 + j) + "</size></color>";
                    itemPanel.GetChild(i).transform.GetChild(0).GetComponentInChildren<Text>().text = text;
                    component.SetTextAlignCenter();
                    itemPanel.GetChild(i).transform.GetChild(0).GetChild(2).GetComponent<Image>()
                        .enabled = false;
                    itemPanel.GetChild(i).transform.GetChild(0).GetComponent<Button>().interactable = false;
                    component.itemIndex = -1;
                    component.isFleet = false;
                    i++;
                    int num = (j + 1) * -1;
                    foreach (CargoItem item5 in cs.cargo)
                    {
                        if (Main.cfgDebug.Value) Main.log.LogInfo("================CargoSystem loop round 3================");
                        if (item5.itemType == Main.itemTypeCrew && IsCrewFiltered(item5))
                            continue;
                        if (item5.itemType < Main.itemTypeCrew && IsCargoItemFiltered(item5))
                            continue;

                        if (item5.stockStationID == num)
                        {
                            if (i >= itemPanel.childCount)
                            {
                                inventoryAddItemSlot.Invoke(__instance, null);
                            }
                            else
                            {
                                itemPanel.GetChild(i).gameObject.SetActive(value: true);
                            }
                            component = itemPanel.GetChild(i).GetComponent<InventorySlot>();
                            itemPanel.GetChild(i).transform.GetChild(0).GetComponentInChildren<Text>().text = item5.GetNameWithQnt();
                            component.SetTextAlignLeft();
                            itemPanel.GetChild(i).transform.GetChild(0).GetChild(2).GetComponent<Image>()
                                .sprite = item5.GetSprite();
                            itemPanel.GetChild(i).transform.GetChild(0).GetChild(2).GetComponent<Image>()
                                .enabled = true;
                            itemPanel.GetChild(i).transform.GetChild(0).GetComponent<Button>().interactable = true;
                            component.itemIndex = cs.cargo.IndexOf(item5);
                            component.slotIndex = i;
                            component.isFleet = false;
                            i++;
                        }
                    }
                }
            }
            Vector2 sizeDelta = new Vector2(itemPanel.GetComponent<RectTransform>().sizeDelta.x, i * 18 + 5);
            itemPanel.GetComponent<RectTransform>().sizeDelta = sizeDelta;
            for (; i < itemPanel.childCount; i++)
            {
                itemPanel.GetChild(i).gameObject.SetActive(value: false);
            }

            btnCargo.Find("Selection").gameObject.SetActive(value: false);
            btnPassengers.Find("Selection").gameObject.SetActive(value: false);
            if (cargoMode == cargoModeCargo)
            {
                __instance.newItemCount = 0;
                text = cs.FreeSpace(passengers: false).ToString("0.0#");
                btnCargo.Find("Selection").gameObject.SetActive(value: true);
                btnCargo.Find("ItemCount").gameObject.SetActive(value: false);
            }
            if (cargoMode == cargoModeCrew)
            {
                __instance.newCrewCount = 0;
                text = cs.FreeSpace(passengers: true).ToString("0");
                btnPassengers.Find("Selection").gameObject.SetActive(value: true);
                btnPassengers.Find("ItemCount").gameObject.SetActive(value: false);
            }
            GameManager.instance.CheckMouseOver();
            btnStashMaterials.SetActive(__instance.inStation);
        }

        private static bool IsCrewFiltered(CargoItem item)
        {
            if (invFilterInput.text.IsNullOrWhiteSpace())
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("Allowed crew.  No filter");
                return false;
            }

            CrewMember crew = CrewDB.GetCrewMember(item.itemID);

            if (crew.aiChar.name.ToLower().Contains(invFilterInput.text.ToLower()))
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("Allowed crew: " + crew.aiChar.name);
                return false;
            }

            if (crew.aiChar.nickname.ToLower().Contains(invFilterInput.text.ToLower()))
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("Allowed crew: " + crew.aiChar.name + "with nickname " + crew.aiChar.nickname);
                return false;
            }

            foreach (CrewSkill skill in crew.skills)
            {
                if (crewPositions[(int)skill.ID].ToLower().Contains(invFilterInput.text.ToLower()))
                {
                    if (Main.cfgDebug.Value) Main.log.LogInfo("Allowed crew: " + crew.aiChar.name + " with skill " + crewPositions[(int)skill.ID]);
                    return false;
                }
            }

            if (Main.cfgDebug.Value) Main.log.LogInfo("Filtered crew: " + crew.aiChar.name);
            return true;
        }

        private static bool IsCargoItemFiltered(CargoItem item)
        {
            if (invFilterInput.text.IsNullOrWhiteSpace())
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("Allowed item.  No filter");
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

            if (name.ToLower().Contains(invFilterInput.text.ToLower()))
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("Allowed item: " + name);
                return false;
            }

            if (Main.cfgDebug.Value) Main.log.LogInfo("Filtered item: " + name);
            return true;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.SwitchCargoMode))]
        [HarmonyPostfix]
        private static void InventorySwitchMode_Post(int mode)
        {
            if (mode < cargoModeFleet && !invFilterInput.gameObject.activeSelf)
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("Switched to cargo/crew, set active");
                invFilterInput.gameObject.SetActive(true);
            }
            else if (mode == cargoModeFleet && invFilterInput.gameObject.activeSelf)
            {
                if (Main.cfgDebug.Value) Main.log.LogInfo("Switched to fleet, set inactive");
                invFilterInput.gameObject.SetActive(false);
            }
        }
    }
}
