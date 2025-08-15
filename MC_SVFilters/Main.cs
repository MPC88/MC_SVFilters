using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections;
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
        public const string pluginVersion = "0.0.1";

        // Star Valor modes, codes and fields
        private const int marketPanelCode = 2;
        private const int hangerPanelCode = 3;
        private static FieldInfo inventoryCargoMode = AccessTools.Field(typeof(Inventory), "cargoMode");
        private static FieldInfo inventoryItemPanel = AccessTools.Field(typeof(Inventory), "itemPanel");
        private static FieldInfo inventoryBtnCargo = AccessTools.Field(typeof(Inventory), "btnCargo");
        private static FieldInfo inventoryBtnPassengers = AccessTools.Field(typeof(Inventory), "btnPassengers");
        private static FieldInfo inventoryBtnStashMaterials = AccessTools.Field(typeof(Inventory), "btnStashMaterials");
        private static MethodInfo inventoryAddItemSlot = AccessTools.Method(typeof(Inventory), "AddItemSlot");
        private const int cargoModeCargo = 0;
        private const int cargoModeCrew = 1;
        private const int cargoModeFleet = 2;
        private static Dictionary<int, string> crewPositions = new Dictionary<int, string>()
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

        // Debug
        public static ConfigEntry<bool> cfgDebug;
        internal static BepInEx.Logging.ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("SV Filters");

        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Main));

            cfgDebug = Config.Bind<bool>(
                "Debug",
                "Debug",
                false,
                "Debug");
        }

        [HarmonyPatch(typeof(DockingUI), nameof(DockingUI.OpenPanel))]
        [HarmonyPostfix]
        private static void DocingUIOpenPanel_Post(DockingUI __instance, Inventory ___inventory, int code)
        {
            if ((code == hangerPanelCode || code == marketPanelCode) && (int)inventoryCargoMode.GetValue(___inventory) < 2)
            {
                if (invFilterInput == null)
                    CreateUI(___inventory.transform.Find("InventoryUI").Find("Credits"));

                inventoryRef = ___inventory;

                invFilterInput.gameObject.SetActive(true);
            }
            else
            {
                if (invFilterInput != null)
                    invFilterInput.gameObject.SetActive(false);
            }
        }

        private static void CreateUI(Transform creditsTrans)
        {
            InputField source = ((GameObject)AccessTools.Field(typeof(InputDialog), "panel").GetValue(InputDialog.inst)).transform.Find("TextInput").GetComponent<InputField>();
            invFilterInput = Object.Instantiate<InputField>(source);            
            invFilterInput.transform.SetParent(creditsTrans.parent);
            invFilterInput.gameObject.layer = creditsTrans.gameObject.layer;
            invFilterInput.transform.localPosition = creditsTrans.transform.localPosition + new Vector3(115,15,0);
            invFilterInput.transform.localScale = creditsTrans.transform.localScale;
            invFilterInput.enabled = true;

            InputField.SubmitEvent ifSubmitEvent = new InputField.SubmitEvent();
            ifSubmitEvent.AddListener(DoFilter);
            invFilterInput.onEndEdit = ifSubmitEvent;

            EventTrigger component = invFilterInput.GetComponentInChildren<EventTrigger>();
            Debug.Log(component == null);

            EventTrigger.Entry selectTrig = new EventTrigger.Entry();
            selectTrig.eventID = EventTriggerType.Select;
            selectTrig.callback.AddListener((data) => { FilterInputSelect(data); });            
            component.triggers.RemoveAll(t => t.eventID == EventTriggerType.Select);
            component.triggers.Add(selectTrig);

            EventTrigger.Entry deselectTrig = new EventTrigger.Entry();
            deselectTrig.eventID = EventTriggerType.Deselect;
            deselectTrig.callback.AddListener((data) => { FilterInputDeSelect(data); });            
            component.triggers.RemoveAll(t => t.eventID == EventTriggerType.Deselect);
            component.triggers.Add(deselectTrig);
        }

        private static void DoFilter(string text)
        {
            if (cfgDebug.Value) log.LogInfo("DoFilter");

            if(inventoryRef == null)
            {
                if (cfgDebug.Value) log.LogError("Null inventoryRef");
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
            if (cargoMode == 2)
                return;

            CargoSystem cs = PlayerControl.inst.GetCargoSystem;            
            Transform itemPanel = (Transform)inventoryItemPanel.GetValue(__instance);
            Transform btnCargo = (Transform)inventoryBtnCargo.GetValue(__instance);
            Transform btnPassengers = (Transform)inventoryBtnPassengers.GetValue(__instance);
            GameObject btnStashMaterials = (GameObject)inventoryBtnStashMaterials.GetValue(__instance);

            foreach (CargoItem item in cs.cargo)
            {
                if (item.stockStationID == -1 && ((item.itemType == 5) ^ (cargoMode == 0)))
                {
                    if (item.itemType == 5 && IsCrewFiltered(item))
                        continue;
                    if (item.itemType < 5 && IsCargoItemFiltered(item))
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
                if (cargoMode < 2)
                {
                    foreach (CargoItem item3 in cs.cargo)
                    {
                        if (item3.itemType == 5 && IsCrewFiltered(item3))
                            continue;
                        if (item3.itemType < 5 && IsCargoItemFiltered(item3))
                            continue;

                        if (item3.stockStationID == __instance.currStation.id && ((item3.itemType == 5) ^ (cargoMode == 0)))
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
            if (__instance.currStation != null && cargoMode == 0)
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
                        if (item5.itemType == 5 && IsCrewFiltered(item5))
                            continue;
                        if (item5.itemType < 5 && IsCargoItemFiltered(item5))
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
            if (cargoMode == 0)
            {
                __instance.newItemCount = 0;
                text = cs.FreeSpace(passengers: false).ToString("0.0#");
                btnCargo.Find("Selection").gameObject.SetActive(value: true);
                btnCargo.Find("ItemCount").gameObject.SetActive(value: false);
            }
            if (cargoMode == 1)
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
                if (cfgDebug.Value) log.LogInfo("Allowed crew.  No filter");
                return false;
            }

            CrewMember crew = CrewDB.GetCrewMember(item.itemID);

            if (crew.aiChar.name.ToLower().Contains(invFilterInput.text.ToLower()))
            {
                if (cfgDebug.Value) log.LogInfo("Allowed crew: " + crew.aiChar.name);
                return false;
            }

            if (crew.aiChar.nickname.ToLower().Contains(invFilterInput.text.ToLower()))
            {
                if (cfgDebug.Value) log.LogInfo("Allowed crew: " + crew.aiChar.name + "with nickname " + crew.aiChar.nickname);
                return false;
            }

            foreach (CrewSkill skill in crew.skills)
            {
                if (crewPositions[(int)skill.ID].ToLower().Contains(invFilterInput.text.ToLower()))
                {
                    if (cfgDebug.Value) log.LogInfo("Allowed crew: " + crew.aiChar.name + " with skill " + crewPositions[(int)skill.ID]);
                    return false;
                }
            }

            if (cfgDebug.Value) log.LogInfo("Filtered crew: " + crew.aiChar.name);
            return true;
        }

        private static bool IsCargoItemFiltered(CargoItem item)
        {
            if (invFilterInput.text.IsNullOrWhiteSpace())
            {
                if (cfgDebug.Value) log.LogInfo("Allowed item.  No filter");
                return false;
            }

            string name = "";
            switch(item.itemType)
            {
                case 1: // Weapon
                    name = GameData.data.weaponList[item.itemID].name;
                    break;
                case 2: // Equipment
                    name = EquipmentDB.GetEquipment(item.itemID).equipName;
                    break;
                case 3: // Item
                    name = ItemDB.GetItem(item.itemID).itemName;
                    break;
                case 4: // Ship
                    name = ShipDB.GetModel(item.itemID).modelName;
                    break;
            }

            if (name.ToLower().Contains(invFilterInput.text.ToLower()))
            {
                if (cfgDebug.Value) log.LogInfo("Allowed item: " + name);
                return false;
            }

            if (cfgDebug.Value) log.LogInfo("Filtered item: " + name);
            return true;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.SwitchCargoMode))]
        [HarmonyPostfix]
        private static void InventorySwitchMode_Post(int mode)
        {
            if (mode < 2 && !invFilterInput.gameObject.activeSelf)
            {
                if (cfgDebug.Value) log.LogInfo("Switched to cargo/crew, set active");
                invFilterInput.gameObject.SetActive(true);
            }
            else if (mode == 2 && invFilterInput.gameObject.activeSelf)
            {
                if (cfgDebug.Value) log.LogInfo("Switched to fleet, set inactive");
                invFilterInput.gameObject.SetActive(false);
            }
        }

        private static void FilterInputSelect(BaseEventData data)
        {
            if (cfgDebug.Value) log.LogInfo("Filter input select block");
            PlayerControl.inst.BlockControls(true);
            PlayerControl.inst.blockKeyboard = true;
        }

        private static void FilterInputDeSelect(BaseEventData data)
        {
            if (cfgDebug.Value) log.LogInfo("Filter input deselect unblock");
            PlayerControl.inst.BlockControls(false);
            PlayerControl.inst.blockKeyboard = false;
        }
    }
}
