using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Color = UnityEngine.Color;
using Object = UnityEngine.Object;

namespace TierSpawnConfig
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    internal class PluginLoader : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        private static bool initialized;

        public static PluginLoader Instance { get; private set; }

        internal static ManualLogSource StaticLogger { get; private set; }
        internal static ConfigFile StaticConfig { get; private set; }
        
        public static ConfigEntry<bool> overlayEnabled;
        
        public static ConfigEntry<int> tier1EnemyCount;
        public static ConfigEntry<int> tier2EnemyCount;
        public static ConfigEntry<int> tier3EnemyCount;
        public static ConfigEntry<string> blacklistedEnemies;
        public static ConfigEntry<bool> closestSpawnPoints;
        public static ConfigEntry<bool> skipCollisionCheck;
        public static ConfigEntry<int> despawnedTimer;
        public static ConfigEntry<int> spawnedTimer;

        private void Awake()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            Instance = this;
            StaticLogger = Logger;
            StaticConfig = Config;
            
            StaticLogger.LogInfo("Patches Loaded");
            
            tier1EnemyCount = StaticConfig.Bind("Spawn Count", "Tier 1", 50, new ConfigDescription("How many tier 1 enemy groups should be spawned? -1 = Vanilla, 0 = None", new AcceptableValueRange<int>(-1, 500)));
            tier2EnemyCount = StaticConfig.Bind("Spawn Count", "Tier 2", 50, new ConfigDescription("How many tier 2 enemy groups should be spawned? -1 = Vanilla, 0 = None", new AcceptableValueRange<int>(-1, 500)));
            tier3EnemyCount = StaticConfig.Bind("Spawn Count", "Tier 3", 50, new ConfigDescription("How many tier 3 enemy groups should be spawned? -1 = Vanilla, 0 = None", new AcceptableValueRange<int>(-1, 500)));
            blacklistedEnemies = StaticConfig.Bind("Spawn Count", "Blacklisted Groups", "Enemy - Ceiling Eye,Enemy - Hidden,Enemy - Thin Man", "Which enemy groups should be disabled? This is a comma-separated list of enemy spawn groups. For the full list check the mod's thunderstore page.");
            
            closestSpawnPoints = StaticConfig.Bind("Spawn Location", "Prioritize Closest Points", true, "Reverse the order that spawn points are picked from. If enabled, the closest spawn points will be used first, otherwise the furthest ones will be used first.");
            skipCollisionCheck = StaticConfig.Bind("Spawn Location", "Bypass Collision Check", true, "Should enemies be able to spawn on top of each other?");
            
            despawnedTimer = StaticConfig.Bind("Spawn Timer", "Respawn Timer", 0, new ConfigDescription("How many seconds should enemies take to respawn? -1 = Vanilla, 0 = Instant", new AcceptableValueRange<int>(-1, 600)));
            despawnedTimer.SettingChanged += (sender, args) =>
            {
                foreach (EnemyParent __instance in Object.FindObjectsOfType<EnemyParent>(true))
                {
                    EnemyPatches.UpdateEnemyTimers(__instance);
                }
            };
            
            spawnedTimer = StaticConfig.Bind("Spawn Timer", "Despawn Timer", 600, new ConfigDescription("How many seconds should enemies take to despawn if nobody is near? -1 = Vanilla, 0 = Disabled", new AcceptableValueRange<int>(-1, 600)));
            spawnedTimer.SettingChanged += (sender, args) =>
            {
                foreach (EnemyParent __instance in Object.FindObjectsOfType<EnemyParent>(true))
                {
                    EnemyPatches.UpdateEnemyTimers(__instance);
                }
            };
            
            harmony.PatchAll(typeof(EnemyPatches));
            
            overlayEnabled = StaticConfig.Bind("Overlay", "Enabled", true, "Should the overlay be shown?");
            
            GameObject testerOverlayObj = new GameObject("TSCOverlay");
            testerOverlayObj.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(testerOverlayObj);
            testerOverlayObj.AddComponent<TesterOverlay>();
        }
    }
    
    public class TesterOverlay : MonoBehaviour
    {
        private string text;
        private float updateTimer;

        private void OnGUI()
        {
            if (!PluginLoader.overlayEnabled.Value)
            {
                text = "";
                return;
            }

            if (SemiFunc.RunIsLevel())
            {
                updateTimer += Time.deltaTime;
                if (updateTimer >= 5f) // Every 5 seconds
                {
                    updateTimer = 0f;
                    
                    Enemy[] enemies = Object.FindObjectsOfType<Enemy>(true);
                    if (enemies.Length > 0)
                    {
                        text = string.Join('\n', enemies.GroupBy(x => x.EnemyParent.enemyName).OrderBy(x => x.Key).ToList().Select(x => $"{x.Key}: {x.Count(y => y.isActiveAndEnabled)}/{x.Count()}"));
                        text += $"\nTotal: {enemies.Count(y => y.isActiveAndEnabled)}/{enemies.Length}";
                    }
                    else
                    {
                        text = null;
                    }
                }
            }
            else
            {
                text = null;
            }
            
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerRight,
                wordWrap = false,
                normal = new GUIStyleState { textColor = Color.white }
            };
            GUIStyle shadowStyle = new GUIStyle(style)
            {
                normal = new GUIStyleState { textColor = Color.black }
            };

            float width = 400f;
            float x = Screen.width - width - 4f;
            
            float height = style.CalcHeight(new GUIContent(text), width);
            float y = Screen.height - height - 20f;

            Rect rect = new Rect(x, y, width, height);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, shadowStyle);
            GUI.Label(rect, text, style);
        }
    }
}
