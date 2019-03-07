using Harmony;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ModTitle("YourBalance")]
[ModDescription("Customize gameplay options at will")]
[ModAuthor("Akitake")]
[ModIconUrl("https://i.imgur.com/IJ8lgzF.png")]
[ModWallpaperUrl("https://i.imgur.com/oJn8uZi.png")]
[ModVersion("2.0.0")]
[RaftVersion("Update 9 (3602784)")]
public class YourBalance : Mod
{
    #region Variables
    public static YourBalance instance;

    // Harmony
    public HarmonyInstance harmony;
    public readonly string harmonyID = "com.github.akitakekun.raftmods.yourbalance";

    // Settings
    private ModSettings newSettings;
    public static ModSettings settings;
    private string settingsPath;

    // Bundle
    private AssetBundle menuBundle;
    private GameObject menu = null;
    private List<Slider> UI_sliders = new List<Slider>();
    private List<Text> UI_slidersText = new List<Text>();
    private List<Toggle> UI_checkboxes = new List<Toggle>();


    // Defaults
    private Dictionary<int, int> defaultStackSizes = new Dictionary<int, int>();
    private Dictionary<int, int> defaultMaxUses = new Dictionary<int, int>();
    private float defaultBiteRaftInterval = 300f;
    private int defaultSharkDamagePerson = 10;
    private int defaultSharkDamageRaft = 5;
    private float defaultHungerRate = 0.08f;
    private float defaultThirstRate = 0.11f;

    // Misc
    private Semih_Network network = ComponentManager<Semih_Network>.Value;
    private Rect windowRect = new Rect(20, 20, 300, 500);
    private bool hideMenu = true;

    // Console stuff
    public static string modColor = "#FE9A4C";
    public static string modPrefix = "[" + Utils.Colorize("YourBalance", modColor) + "] ";
    #endregion

    public void Start()
    {
        if (instance != null) { throw new Exception("YourBalance singleton was already set"); }
        instance = this;

        harmony = HarmonyInstance.Create(harmonyID);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        settingsPath = Directory.GetCurrentDirectory() + "\\mods\\ModData\\YourBalance.json";
        settings = LoadSettings();
        if (SceneManager.GetActiveScene().name == network.gameSceneName)
            StartCoroutine(ForceSettings());

        SceneManager.sceneLoaded += OnSceneLoaded;

        List<Item_Base> items = ItemManager.GetAllItems();
        foreach (var item in items)
        {
            defaultStackSizes.Add(item.UniqueIndex, item.settings_Inventory.StackSize);
            defaultMaxUses.Add(item.UniqueIndex, item.MaxUses);
        }

        #region UI Setup
        // Load Bundle
        if (File.Exists("mods/ModData/yourbalance.assets"))
        {
            menuBundle = AssetBundle.LoadFromFile("mods/ModData/yourbalance.assets");
            UISetup();
        }
        else
        {
            StartCoroutine(LoadBundle());
        }

        RConsole.Log(modPrefix + "loaded!");
        #endregion
    }

    public void OnModUnload()
    {
        RConsole.Log(modPrefix + "unloaded!");
        menuBundle.Unload(true);
        StopAllCoroutines();
        SaveSettings();
        harmony.UnpatchAll(harmonyID);
        Destroy(gameObject);
    }

    public void OnLeaveGame()
    {
        StopAllCoroutines();
    }

    public void Update()
    {
        if (SceneManager.GetActiveScene().name != network.gameSceneName) { return; }
        if (menu == null) { return; }
        if (Input.GetKeyDown(KeyCode.F11))
        {
            if (hideMenu)
            {
                ShowUI();
            }
            else
            {
                HideUI();
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == network.gameSceneName)
        {
            settings = LoadSettings();
            StartCoroutine(ForceSettings());
            RConsole.Log(modPrefix + "Scene " + scene.name + " loaded.");
        }
    }

    // UI
    IEnumerator LoadBundle()
    {
        var uwr = UnityWebRequestAssetBundle.GetAssetBundle("https://github.com/AkitakeKun/RaftMods/raw/master/RaftMods/yourbalance.assets");
        yield return uwr.SendWebRequest();

        // Get an asset from the bundle and instantiate it.
        menuBundle = DownloadHandlerAssetBundle.GetContent(uwr);
        var loadAsset = menuBundle.LoadAssetAsync<GameObject>("YourBalance_Canvas");
        yield return loadAsset.isDone;

        menu = (GameObject)Instantiate(loadAsset.asset, gameObject.transform);
        UISetup();
    }
    public void UISetup()
    {
        menu.SetActive(false);

        // Apply & Close buttons listeners
        menu.transform.Find("MainBG").GetComponentsInChildren<Button>().Where(x => x.name == "ApplyButton").FirstOrDefault().onClick.AddListener(UpdateSettings);
        menu.transform.Find("MainBG").GetComponentsInChildren<Button>().Where(x => x.name == "CloseButton").FirstOrDefault().onClick.AddListener(HideUI);

        // Sliders setup
        List<string> sliders = new List<string>
        {
            "Durability",
            "StackSize",
            "Hunger",
            "Thirst",
            "SharkDamage",
            "SharkAtkRate"
        };
        foreach (string sliderName in sliders)
        {
            var slider = menu.transform.Find("MainBG").GetComponentsInChildren<Image>().Where(x => x.name == sliderName).FirstOrDefault().GetComponentInChildren<Slider>();
            var sliderText = slider.GetComponentInChildren<Text>();
            UI_sliders.Add(slider);
            UI_slidersText.Add(sliderText);
        }
        UI_sliders[0].onValueChanged.AddListener(UI_DurabilitySlider_Changed);
        UI_sliders[1].onValueChanged.AddListener(UI_StackSizeSlider_Changed);
        UI_sliders[2].onValueChanged.AddListener(UI_HungerSlider_Changed);
        UI_sliders[3].onValueChanged.AddListener(UI_ThirstSlider_Changed);
        UI_sliders[4].onValueChanged.AddListener(UI_SharkDmgSlider_Changed);
        UI_sliders[5].onValueChanged.AddListener(UI_SharkAtkRateSlider_Changed);

        // Checkboxes setup
        List<string> checkboxes = new List<string>
        {
            "SharkPlyBiting",
            "SharkRaftBiting",
            "SharkRespawn",
            "DolphinJump",
            "FallDamage",
            "DevCheats"
        };
        foreach (string checkbox in checkboxes)
        {
            var toggle = menu.transform.Find("MainBG").GetComponentsInChildren<Image>().Where(x => x.name == checkbox).FirstOrDefault().GetComponentInChildren<Toggle>();
            UI_checkboxes.Add(toggle);
        }
        UI_checkboxes[0].onValueChanged.AddListener(UI_SharkPlayerBitingToggle_Changed);
        UI_checkboxes[1].onValueChanged.AddListener(UI_SharkRaftBitingToggle_Changed);
        UI_checkboxes[2].onValueChanged.AddListener(UI_SharkRespawnToggle_Changed);
        UI_checkboxes[3].onValueChanged.AddListener(UI_DolphinJumpToggle_Changed);
        UI_checkboxes[4].onValueChanged.AddListener(UI_FallDamageToggle_Changed);
        UI_checkboxes[5].onValueChanged.AddListener(UI_DevCheatsToggle_Changed);
    }
    public void RefreshUI()
    {
        UI_sliders[0].value = settings.durabilityMultiplier;
        UI_sliders[1].value = settings.stackSizeMultiplier;
        UI_sliders[2].value = settings.foodDecrementRateMultiplier;
        UI_sliders[3].value = settings.thirstDecrementRateMultiplier;
        UI_sliders[4].value = settings.sharkDamageMulitplier;
        UI_sliders[5].value = settings.biteRaftIntervalMultiplier;

        UI_checkboxes[0].isOn = settings.sharkDisabled;
        UI_checkboxes[1].isOn = settings.disableSharkBitingRaft;
        UI_checkboxes[2].isOn = settings.disableFastSharkSpawn;
        UI_checkboxes[3].isOn = settings.enableDolphinJumping;
        UI_checkboxes[4].isOn = settings.fallDmgDisabled;
        UI_checkboxes[5].isOn = settings.cheatsEnabled;
    }
    public void ShowUI()
    {
        menu.SetActive(true);
        newSettings = new ModSettings(settings);
        hideMenu = false;
        Helper.SetCursorVisibleAndLockState(true, CursorLockMode.Confined);
        CanvasHelper.ActiveMenu = MenuType.TextWriter;
    }
    public void HideUI()
    {
        menu.SetActive(false);
        Helper.SetCursorVisibleAndLockState(false, CursorLockMode.Locked);
        newSettings = new ModSettings(settings);
        hideMenu = true;
        CanvasHelper.ActiveMenu = MenuType.None;
        RefreshUI();
    }
    #region UI Sliders Changed
    private void UI_DurabilitySlider_Changed(float val)
    {
        newSettings.durabilityMultiplier = Convert.ToInt32(val);
        if (newSettings.durabilityMultiplier == 0) { newSettings.durabilityMultiplier = 1; }
        UI_slidersText[0].text = newSettings.durabilityMultiplier.ToString();
    }
    private void UI_StackSizeSlider_Changed(float val)
    {
        newSettings.stackSizeMultiplier = Convert.ToInt32(val);
        if (newSettings.stackSizeMultiplier == 0) { newSettings.stackSizeMultiplier = 1; }
        UI_slidersText[1].text = newSettings.stackSizeMultiplier.ToString();
    }
    private void UI_HungerSlider_Changed(float val)
    {
        newSettings.foodDecrementRateMultiplier = Convert.ToInt32(val);
        if (newSettings.foodDecrementRateMultiplier == 0) { newSettings.foodDecrementRateMultiplier = 1; }
        UI_slidersText[2].text = newSettings.foodDecrementRateMultiplier.ToString();
    }
    private void UI_ThirstSlider_Changed(float val)
    {
        newSettings.thirstDecrementRateMultiplier = Convert.ToInt32(val);
        if (newSettings.thirstDecrementRateMultiplier == 0) { newSettings.thirstDecrementRateMultiplier = 1; }
        UI_slidersText[3].text = newSettings.thirstDecrementRateMultiplier.ToString();
    }
    private void UI_SharkDmgSlider_Changed(float val)
    {
        newSettings.sharkDamageMulitplier = Convert.ToInt32(val);
        if (newSettings.sharkDamageMulitplier == 0) { newSettings.sharkDamageMulitplier = 1; }
        UI_slidersText[4].text = newSettings.sharkDamageMulitplier.ToString();
    }
    private void UI_SharkAtkRateSlider_Changed(float val)
    {
        newSettings.biteRaftIntervalMultiplier = Convert.ToInt32(val);
        if (newSettings.biteRaftIntervalMultiplier == 0) { newSettings.biteRaftIntervalMultiplier = 1; }
        UI_slidersText[5].text = newSettings.biteRaftIntervalMultiplier.ToString();
    }
    #endregion
    #region UI Toggles Changed
    private void UI_SharkPlayerBitingToggle_Changed(bool val) { newSettings.sharkDisabled = val; }
    private void UI_SharkRaftBitingToggle_Changed(bool val) { newSettings.disableSharkBitingRaft = val; }
    private void UI_SharkRespawnToggle_Changed(bool val) { newSettings.disableFastSharkSpawn = val; }
    private void UI_DolphinJumpToggle_Changed(bool val) { newSettings.enableDolphinJumping = val; }
    private void UI_FallDamageToggle_Changed(bool val) { newSettings.fallDmgDisabled = val; }
    private void UI_DevCheatsToggle_Changed(bool val) { newSettings.cheatsEnabled = val; }
    #endregion

    public void UpdateSettings()
    {
        if (settings.durabilityMultiplier != newSettings.durabilityMultiplier) { SetDurability(newSettings.durabilityMultiplier); }
        if (settings.stackSizeMultiplier != newSettings.stackSizeMultiplier) { SetStackSize(newSettings.stackSizeMultiplier); }
        if (settings.foodDecrementRateMultiplier != newSettings.foodDecrementRateMultiplier) { SetHunger(newSettings.foodDecrementRateMultiplier); }
        if (settings.thirstDecrementRateMultiplier != newSettings.thirstDecrementRateMultiplier) { SetThirst(newSettings.thirstDecrementRateMultiplier); }
        if (settings.sharkDamageMulitplier != newSettings.sharkDamageMulitplier) { SetSharkAttackDamage(newSettings.sharkDamageMulitplier); }
        if (settings.biteRaftIntervalMultiplier != newSettings.biteRaftIntervalMultiplier) { SetSharkAttackInterval(newSettings.biteRaftIntervalMultiplier); }
        if (settings.cheatsEnabled != newSettings.cheatsEnabled) { SetCheats(newSettings.cheatsEnabled); }
        settings = new ModSettings(newSettings);
        SaveSettings();
        RefreshUI();
    }

    public IEnumerator ForceSettings()
    {
        yield return new WaitForSeconds(5);
        SetDurability(settings.durabilityMultiplier);
        SetStackSize(settings.stackSizeMultiplier);
        SetHunger(settings.foodDecrementRateMultiplier);
        SetThirst(settings.thirstDecrementRateMultiplier);
        SetSharkAttackDamage(settings.sharkDamageMulitplier);
        SetSharkAttackInterval(settings.biteRaftIntervalMultiplier);
        RefreshUI();
    }

    public void SetCheats(bool value)
    {
        GameManager.UseCheats = value;
        SaveSettings();
        RConsole.Log(modPrefix + "Developer cheats are " + Utils.Colorize((GameManager.UseCheats ? "enabled" : "disabled"), modColor));
    }

    public void SetDurability(int multiplier)
    {
        List<Item_Base> items = ItemManager.GetAllItems();
        foreach (var item in items)
        {
            int defaultSize = defaultMaxUses[item.UniqueIndex];
            if (defaultSize <= 1) { continue; } // Avoid changing durability of items that "dont have" durability (like cups)
            int adjustedSize = (multiplier > 0) ? (defaultSize * multiplier) : (defaultSize / (Math.Abs(multiplier) + 1));
            PrivateValueAccessor.GetPrivateFieldInfo(typeof(Item_Base), "maxUses").SetValue(item, adjustedSize);
        }
        RConsole.Log(modPrefix + "Durability multiplier set.");
    }

    public void SetStackSize(int multiplier)
    {
        List<Item_Base> items = ItemManager.GetAllItems();
        // Change base object 
        foreach (var item in items)
        {
            int defaultSize = defaultStackSizes[item.UniqueIndex];
            if (defaultSize <= 1) { continue; } // Avoid changing stack size of items that shouldn't stack (like cups)
            int adjustedSize = (multiplier > 0) ? (defaultSize * multiplier) : (defaultSize / (Math.Abs(multiplier) + 1));
            ItemInstance_Inventory inventory = item.settings_Inventory;
            PrivateValueAccessor.GetPrivateFieldInfo(typeof(ItemInstance_Inventory), "stackSize").SetValue(inventory, adjustedSize);
        }
        RConsole.Log(modPrefix + "BaseItem stack size set.");

        // Change items in players inventory
        foreach (Slot slot in network.GetLocalPlayer().Inventory.allSlots)
        {
            if (!slot.HasValidItemInstance()) { continue; }

            Item_Base item = slot.itemInstance.baseItem;
            int defaultSize = defaultStackSizes[item.UniqueIndex];
            if (defaultSize <= 1) { continue; } // Avoid changing stack size of items that shouldn't stack (like cups)
            int adjustedSize = (multiplier > 0) ? (defaultSize * multiplier) : (defaultSize / (Math.Abs(multiplier) + 1));
            ItemInstance_Inventory settings = slot.itemInstance.settings_Inventory;
            PrivateValueAccessor.GetPrivateFieldInfo(typeof(ItemInstance_Inventory), "stackSize").SetValue(settings, adjustedSize);
        }
        RConsole.Log(modPrefix + "Inventory items stack size set.");

        // Change items in storage boxes
        foreach (var storage in StorageManager.allStorages)
        {
            foreach (var slot in storage.GetInventoryReference().allSlots)
            {
                if (!slot.HasValidItemInstance()) { continue; }

                Item_Base item = slot.itemInstance.baseItem;
                int defaultSize = defaultStackSizes[item.UniqueIndex];
                if (defaultSize <= 1) { continue; } // Avoid changing stack size of items that shouldn't stack (like cups)
                int adjustedSize = (multiplier > 0) ? (defaultSize * multiplier) : (defaultSize / (Math.Abs(multiplier) + 1));
                ItemInstance_Inventory settings = slot.itemInstance.settings_Inventory;
                PrivateValueAccessor.GetPrivateFieldInfo(typeof(ItemInstance_Inventory), "stackSize").SetValue(settings, adjustedSize);
            }
        }
        RConsole.Log(modPrefix + "Storage items stack size set.");
    }

    public void SetHunger(int multiplier)
    {
        var adjustedRate = (multiplier > 0) ? (defaultHungerRate / multiplier) : (defaultHungerRate * (Math.Abs(multiplier) + 1));
        var hunger = network.GetLocalPlayer().Stats.stat_hunger;
        PrivateValueAccessor.GetPrivateFieldInfo(typeof(Stat_Hunger), "hungerLostPerSecondDefault").SetValue(hunger.Bonus, adjustedRate);
        PrivateValueAccessor.GetPrivateFieldInfo(typeof(Stat_Hunger), "hungerLostPerSecondDefault").SetValue(hunger.Normal, adjustedRate);
        RConsole.Log(modPrefix + "Hunger rate set.");
    }

    public void SetThirst(int multiplier)
    {
        var adjustedRate = (multiplier > 0) ? (defaultThirstRate / multiplier) : (defaultThirstRate * (Math.Abs(multiplier) + 1));
        Stat_Thirst thirst = network.GetLocalPlayer().Stats.stat_thirst;
        PrivateValueAccessor.GetPrivateFieldInfo(typeof(Stat_Thirst), "thirstLostPerSecondDefault").SetValue(thirst, adjustedRate);
        RConsole.Log(modPrefix + "Thirst rate set.");
    }

    public void SetSharkAttackDamage(int multipler)
    {
        var sharks = FindObjectsOfType<Shark>();
        int adjustedRaftDamage = (multipler > 0) ? (defaultSharkDamageRaft / multipler) : (defaultSharkDamageRaft * (Math.Abs(multipler) + 1));
        int adjustedPlayerDamage = (multipler > 0) ? (defaultSharkDamageRaft / multipler) : (defaultSharkDamagePerson * (Math.Abs(multipler) + 1));
        foreach (Shark shark in sharks)
        {
            PrivateValueAccessor.GetPrivateFieldInfo(typeof(Shark), "biteRaftDamage").SetValue(shark, adjustedRaftDamage);
            PrivateValueAccessor.GetPrivateFieldInfo(typeof(Shark), "attackPlayerDamage").SetValue(shark, adjustedPlayerDamage);
        }
        RConsole.Log(modPrefix + "Shark damage set.");
    }

    public void SetSharkAttackInterval(int multipler)
    {
        var sharks = FindObjectsOfType<Shark>();
        float adjustedInterval = (multipler > 0) ? (defaultBiteRaftInterval * multipler) : (defaultBiteRaftInterval / (Math.Abs(multipler) + 1));
        foreach (Shark shark in sharks)
        {
            PrivateValueAccessor.GetPrivateFieldInfo(typeof(Shark), "searchBlockInterval").SetValue(shark, adjustedInterval);
        }
        RConsole.Log(modPrefix + "Shark bite raft interval set.");
    }

    public string FormatLabel(string body, string @default, string smaller, string larger, int value)
    {
        if (value == 1)
        {
            return String.Format(body, @default);
        }
        string tail = (value > 1) ? larger : smaller;
        return String.Format(body, String.Format(tail, Math.Abs(value)));
    }

    public string FormatLabel(string body, string @default, string smaller, string larger, float value)
    {
        if (value == 1)
        {
            return String.Format(body, @default);
        }
        string tail = (value > 1) ? larger : smaller;
        return String.Format(body, String.Format(tail, Math.Abs(value)));
    }

    public ModSettings LoadSettings()
    {
        ModSettings savedSettings = new ModSettings();
        if (File.Exists(settingsPath) && new FileInfo(settingsPath).Length > 0L)
        {
            savedSettings = JsonUtility.FromJson<ModSettings>(File.ReadAllText(settingsPath));
        }
        return savedSettings;
    }

    public void SaveSettings()
    {
        RConsole.Log(modPrefix + "Saving settings");
        try
        {
            string serializedSettings = JsonUtility.ToJson(settings);
            File.WriteAllText(settingsPath, serializedSettings);
            RConsole.Log(modPrefix + "Settings saved to " + Utils.Colorize(settingsPath, modColor));
        }
        catch
        {
            RConsole.Log(modPrefix + Utils.Colorize("Settings were unable to be saved to file " + settingsPath, "#FF0000"));
        }
    }
}

#region Child Classes
class Utils
{
    private static List<string> positiveBools = new List<string>() { "true", "1", "yes", "y" };
    private static List<string> negativeBools = new List<string>() { "false", "0", "no", "n" };

    #region TypeChecks
    public static bool IsBool(string text)
    {
        text = text.ToLowerInvariant().Replace(" ", "");
        if (!positiveBools.Contains(text) && !negativeBools.Contains(text))
            return false;
        return true;
    }
    public static bool Bool(string text, bool original)
    {
        text = text.ToLowerInvariant().Replace(" ", "");
        if (positiveBools.Contains(text))
            return true;
        if (negativeBools.Contains(text))
            return false;
        return original;
    }
    #endregion

    #region Colorize
    public static string Colorize(string text, string col)
    {
        string s = string.Concat(new string[]
        {
            "<color=",
            col,
            ">",
            text,
            "</color>"
        });
        return s;
    }
    #endregion
}

public class PrivateValueAccessor
{
    public static BindingFlags Flags = BindingFlags.Instance
                                       | BindingFlags.GetProperty
                                       | BindingFlags.SetProperty
                                       | BindingFlags.GetField
                                       | BindingFlags.SetField
                                       | BindingFlags.NonPublic;

    public static PropertyInfo GetPrivatePropertyInfo(Type type, string propertyName)
    {
        var props = type.GetProperties(Flags);
        return props.FirstOrDefault(propInfo => propInfo.Name == propertyName);
    }

    public static FieldInfo GetPrivateFieldInfo(Type type, string fieldName)
    {
        var fields = type.GetFields(Flags);
        return fields.FirstOrDefault(feildInfo => feildInfo.Name == fieldName);
    }

    public static object GetPrivateFieldValue(Type type, string fieldName, object o)
    {
        return GetPrivateFieldInfo(type, fieldName).GetValue(o);
    }
}

[Serializable]
public class ModSettings
{
    public bool sharkDisabled;
    public bool fallDmgDisabled;
    public int foodDecrementRateMultiplier;
    public int thirstDecrementRateMultiplier;
    public int durabilityMultiplier;
    public int stackSizeMultiplier;
    public int sharkDamageMulitplier;
    public int biteRaftIntervalMultiplier;
    public bool disableSharkBitingRaft;
    public bool disableFastSharkSpawn;
    public bool enableDolphinJumping;
    public bool cheatsEnabled;

    public ModSettings()
    {
        sharkDisabled = false;
        fallDmgDisabled = false;
        foodDecrementRateMultiplier = 1;
        thirstDecrementRateMultiplier = 1;
        durabilityMultiplier = 1;
        stackSizeMultiplier = 1;
        sharkDamageMulitplier = 1;
        biteRaftIntervalMultiplier = 1;
        disableSharkBitingRaft = false;
        disableFastSharkSpawn = false;
        enableDolphinJumping = false;
        cheatsEnabled = false;
    }
    public ModSettings(ModSettings clone)
    {
        sharkDisabled = clone.sharkDisabled;
        fallDmgDisabled = clone.fallDmgDisabled;
        foodDecrementRateMultiplier = clone.foodDecrementRateMultiplier;
        thirstDecrementRateMultiplier = clone.thirstDecrementRateMultiplier;
        durabilityMultiplier = clone.durabilityMultiplier;
        stackSizeMultiplier = clone.stackSizeMultiplier;
        sharkDamageMulitplier = clone.sharkDamageMulitplier;
        biteRaftIntervalMultiplier = clone.biteRaftIntervalMultiplier;
        disableSharkBitingRaft = clone.disableSharkBitingRaft;
        disableFastSharkSpawn = clone.disableFastSharkSpawn;
        enableDolphinJumping = clone.enableDolphinJumping;
        cheatsEnabled = clone.cheatsEnabled;
    }
}
#endregion

#region Harmony Patches
[HarmonyPatch(typeof(Helper))]
[HarmonyPatch("BroadCastToScene")]
class BroadCastToScenePatch
{
    static void Prefix(SceneEvent sceneEvent)
    {
        if (sceneEvent == SceneEvent.LeaveGame)
        {
            YourBalance.instance.OnLeaveGame();
        }
    }
}

[HarmonyPatch(typeof(PersonController))]
[HarmonyPatch("CalculateFallDamage")]
class DisableFalldamagePatch
{
    static bool Prefix(float p_fallDuration)
    {
        if (YourBalance.settings.fallDmgDisabled)
        {
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Shark))]
[HarmonyPatch("ChangeState")]
class SharkPlayerBitingPatch
{
    static bool Prefix(SharkState newState)
    {
        if (newState == SharkState.AttackPlayer && YourBalance.settings.sharkDisabled)
        {
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Shark))]
[HarmonyPatch("AttackRaftUpdate")]
static class SharkRaftBitingPatch
{
    static void Postfix(Shark __instance) // Saved state
    {
        if (__instance.state == SharkState.AttackRaft)
        {
            bool attackingRaft = __instance.targetBlock != null && !(__instance.targetBlock is SharkBait);
            if (attackingRaft && YourBalance.settings.disableSharkBitingRaft)
            {
                __instance.ChangeState(SharkState.PassiveWater);
            }
        }
    }
}

[HarmonyPatch(typeof(Network_Host_Entities))]
[HarmonyPatch("CreateShark")]
[HarmonyPatch(new Type[] { typeof(float), typeof(Vector3), typeof(uint), typeof(uint), typeof(uint) })]
static class SharkRespawnPatch
{
    static void Prefix(ref float timeDelay)
    {
        if (YourBalance.settings.disableFastSharkSpawn)
        {
            int extraTime = UnityEngine.Random.Range(5, 10);
            timeDelay = extraTime * 60;
        }
        YourBalance.instance.SetSharkAttackDamage(YourBalance.settings.sharkDamageMulitplier);
        YourBalance.instance.SetSharkAttackInterval(YourBalance.settings.biteRaftIntervalMultiplier);
    }
}

[HarmonyPatch(typeof(PersonController))]
[HarmonyPatch("WaterControll")]
class WaterJumpingPatch
{
    static public void Postfix(ref PersonController __instance)
    {
        if (!YourBalance.settings.enableDolphinJumping) { return; }
        var ypos = __instance.transform.position.y;
        if (ypos >= -2.5 && ypos <= 1.0f)
        {
            if (MyInput.GetButton("Jump"))
            {
                __instance.SwitchControllerType(ControllerType.Ground);
                __instance.yVelocity = (-10 * __instance.transform.position.y);
            }
        }
    }
}
#endregion