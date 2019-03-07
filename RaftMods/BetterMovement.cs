using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#region Main Class
[ModTitle("BetterMovement")]
[ModDescription("")]
[ModAuthor("Akitake")]
[ModIconUrl("https://i.imgur.com/bVh3Cch.png")]
[ModWallpaperUrl("https://i.imgur.com/R9tHDs3.png")]
[ModVersion("1.0.0")]
[RaftVersion("Update 9 (3602784)")]
public class BetterMovement : Mod
{
    #region Variables
    // Harmony
    public HarmonyInstance harmony;
    public readonly string harmonyID = "com.github.akitakekun.bettermovement";

    // Settings
    private ModSettings settings;
    private string settingsPath;

    // Bundles
    private AssetBundle menuBundle;
    private GameObject menu;

    // Console stuff
    private static Color modColor = new Color(0, 176, 254);
    private static string modPrefix = "[" + Utils.Colorize("BetterMovement", modColor) + "] ";

    // Misc
    private Semih_Network network = ComponentManager<Semih_Network>.Value;
    private Settings gameSettings = ComponentManager<Settings>.Value;
    #endregion

    public void Start()
    {
        harmony = HarmonyInstance.Create(harmonyID);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        settingsPath = Directory.GetCurrentDirectory() + "/mods/ModData/BetterMovement.json";
        settings = LoadSettings();
        PersonController_GroundControll_Patch.sprintByDefault = settings.sprintByDefault;

        RConsole.registerCommand(typeof(BetterMovement), "If true, sprint key becomes a walk key, and you sprint by default (true or false, default: true)", "sprintByDefault", SprintByDefault);
        RConsole.registerCommand(typeof(BetterMovement), "If true, you no longer need to hold the crouch key, just tap it to toggle (true or false, default: false)", "crouchIsToggle", CrouchIsToggle);

        //menuBundle = AssetBundle.LoadFromFile("mods/quitgamemod.assets");
        //menu = Instantiate(menuBundle.LoadAsset<GameObject>("QuitGameMod"), gameObject.transform);
        //menu.transform.Find("QuitGame").GetComponent<Button>().onClick.AddListener(QuitGame);

        RConsole.Log(modPrefix + "loaded!");
    }

    public void Update()
    {
        var ply = RAPI.getLocalPlayer();
        if (SceneManager.GetActiveScene().name != network.gameSceneName || ply == null) { return; }
        var localPlayerTPS = ply.currentModel.thirdPersonSettings;
        if (!localPlayerTPS.ThirdPersonState) { return; }

        localPlayerTPS.lerpCameraMovementBackSpeed = 9999;
        localPlayerTPS.lerpCameraMovementSpeed = 9999;
        localPlayerTPS.lerpCameraRotationSpeed = 9999;

        ThirdPerson TP = ply.gameObject.GetComponentInChildren<ThirdPerson>();
        Vector3 vec = (Vector3)Traverse.Create(TP).Field("localCameraRotation").GetValue();

        vec += new Vector3(Input.GetAxis("Mouse Y"), -Input.GetAxis("Mouse X"), 0f);
        vec += new Vector3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0f) * gameSettings.controls.MouseSensitivity;

        Traverse.Create(TP).Field("localCameraRotation").SetValue(vec);
    }

    public void OnModUnload()
    {
        RConsole.Log(modPrefix + "unloaded!");
        PersonController_GroundControll_Patch.sprintByDefault = false;
        PersonController_GroundControll_Patch.crouchIsToggle = false;
        //menuBundle.Unload(true);
        harmony.UnpatchAll(harmonyID);
        Destroy(gameObject);
    }

    public void SprintByDefault()
    {
        string[] args = RConsole.lcargs;
        if (args.Length > 1)
        {
            if (!Utils.IsBool(args[1]))
            {
                RConsole.Log(modPrefix + Utils.Colorize(args[1] + " is not a valid argument", 255));
                return;
            }
            bool arg = Utils.Bool(args[1], settings.crouchIsToggle);
            PersonController_GroundControll_Patch.sprintByDefault = arg;
            settings.sprintByDefault = arg;
            SaveSettings();
            RConsole.Log(modPrefix + "sprintByDefault is now set to: " + Utils.Colorize(arg.ToString(), modColor));
            return;
        }
        RConsole.Log(modPrefix + "sprintByDefault is currently set to: " + Utils.Colorize(settings.sprintByDefault.ToString(), modColor));
    }

    public void CrouchIsToggle()
    {
        string[] args = RConsole.lcargs;
        if (args.Length > 1)
        {
            RConsole.Log(args[1]);
            if (!Utils.IsBool(args[1]))
            {
                RConsole.Log(modPrefix + Utils.Colorize(args[1] + " is not a valid argument", 255));
                return;
            }
            bool arg = Utils.Bool(args[1], settings.crouchIsToggle);
            PersonController_GroundControll_Patch.crouchIsToggle = arg;
            settings.crouchIsToggle = arg;
            SaveSettings();
            RConsole.Log(modPrefix + "crouchIsToggle is now set to: " + Utils.Colorize(arg.ToString(), modColor));
            return;
        }
        RConsole.Log(modPrefix + "crouchIsToggle is currently set to: " + Utils.Colorize(settings.crouchIsToggle.ToString(), modColor));
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
        try
        {
            string contents = JsonUtility.ToJson(settings);
            File.WriteAllText(settingsPath, contents);
        }
        catch
        {
            RConsole.Log(modPrefix + Utils.Colorize("Settings were unable to be saved to file" + settingsPath, 255));
        }
    }
}
#endregion

#region Child Classes
[Serializable]
public class ModSettings
{
    public bool sprintByDefault;
    public bool crouchIsToggle;

    public ModSettings()
    {
        sprintByDefault = false;
        crouchIsToggle = false;
    }

    public ModSettings(ModSettings clone)
    {
        sprintByDefault = clone.sprintByDefault;
        crouchIsToggle = clone.crouchIsToggle;
    }
}

public class Utils
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

    #region Colorize variants
    public static string Colorize(string text, int r = 0, int g = 0, int b = 0)
    {
        return string.Concat(new string[]
        {
            "<color=#",
            ColorUtility.ToHtmlStringRGB(new Color(r, g, b)),
            ">",
            text,
            "</color>"
        });
    }
    public static string Colorize(string text, Color col)
    {
        return string.Concat(new string[]
        {
            "<color=#",
            ColorUtility.ToHtmlStringRGB(col),
            ">",
            text,
            "</color>"
        });
    }
    #endregion
}
#endregion

#region Harmony Patches
[HarmonyPatch(typeof(PersonController))]
[HarmonyPatch("GroundControll")]
public class PersonController_GroundControll_Patch
{
    public static bool sprintByDefault;

    public static bool crouchIsToggle;
    private static bool isCrouching;

    private static float defaultNormalSpeed = 3f;
    private static float defaultSprintSpeed = 6f;

    static void Postfix(PersonController __instance, ref Network_Player ___playerNetwork, ref Vector3 ___moveDirection)
    {
        if (sprintByDefault && !__instance.crouching && !isCrouching)
        {
            __instance.normalSpeed = defaultSprintSpeed;
            __instance.sprintSpeed = defaultNormalSpeed;
        }
        else
        {
            __instance.normalSpeed = defaultNormalSpeed;
            __instance.sprintSpeed = defaultSprintSpeed;
        }

        if (crouchIsToggle)
        {
            if (MyInput.GetButtonUp("Crouch"))
            {
                isCrouching = !isCrouching;
            }
            __instance.crouching = isCrouching;
            ___playerNetwork.Animator.anim.SetBool("Crouching", isCrouching);
            if (!MyInput.GetButton("Crouch") && isCrouching)
            {
                float speed = MyInput.GetButton("Sprint") ? __instance.sprintSpeed : __instance.normalSpeed;
                ___moveDirection /= speed * Stat_WellBeing.groundSpeedMultiplier;
            }
        }
    }
}
#endregion