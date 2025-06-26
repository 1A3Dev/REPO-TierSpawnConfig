using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TierSpawnConfig
{
    [HarmonyPatch]
    public class EnemyPatches
    {
	    private static readonly int reservedViewCount = 100; // Reserve 100 photon views for other objects
	    
	    [HarmonyPatch(typeof(EnemyDirector), "FirstSpawnPointAdd")]
	    [HarmonyPrefix]
	    private static bool EnemyDirector_FirstSpawnPointAdd_Prefix(EnemyDirector __instance, EnemyParent _enemyParent)
	    {
		    if (!PluginLoader.closestSpawnPoints.Value)
			    return true;
		    
		    List<LevelPoint> list = SemiFunc.LevelPointsGetAll();
		    float num = float.MaxValue;
		    LevelPoint levelPoint = null;
		    foreach (LevelPoint item in list)
		    {
			    if (item.Truck || __instance.enemyFirstSpawnPoints.Contains(item))
				    continue;
			    
			    float num2 = Vector3.Distance(item.transform.position, LevelGenerator.Instance.LevelPathTruck.transform.position);
			    if (num2 < num)
			    {
				    num = num2;
				    levelPoint = item;
			    }
		    }
		    if (levelPoint)
		    {
			    _enemyParent.firstSpawnPoint = levelPoint;
			    __instance.enemyFirstSpawnPoints.Add(levelPoint);
		    }
		    
		    return false;
	    }
	    
	    [HarmonyPatch(typeof(EnemyDirector), "FirstSpawnPointAdd")]
	    [HarmonyPostfix]
	    private static void EnemyDirector_FirstSpawnPointAdd_Postfix(EnemyDirector __instance)
	    {
		    // Clear list if all spawn points used
		    int totalSpawnPoints = SemiFunc.LevelPointsGetAll().Count(x => !x.Truck);
		    if (__instance.enemyFirstSpawnPoints.Count >= totalSpawnPoints)
		    {
			    PluginLoader.StaticLogger.LogDebug($"{__instance.enemyFirstSpawnPoints.Count}/{totalSpawnPoints} spawn points are used, clearing list");
			    __instance.enemyFirstSpawnPoints.Clear();
		    }
	    }

	    [HarmonyPatch(typeof(EnemyDirector), "AmountSetup")]
	    [HarmonyPrefix]
	    private static bool EnemyDirector_AmountSetup(EnemyDirector __instance)
	    {
		    __instance.amountCurve1Value = PluginLoader.tier1EnemyCount.Value;
		    __instance.amountCurve2Value = PluginLoader.tier2EnemyCount.Value;
		    __instance.amountCurve3Value = PluginLoader.tier3EnemyCount.Value;
		    __instance.enemyListCurrent.Clear();
		    List<string> enemyBlacklist = PluginLoader.blacklistedEnemies.Value.Split(',').Select(x => x.Trim()).ToList();
		    PluginLoader.StaticLogger.LogInfo($"Setting enemy counts: Tier 1: {__instance.amountCurve1Value}, Tier 2: {__instance.amountCurve2Value}, Tier 3: {__instance.amountCurve3Value}");
		    PluginLoader.StaticLogger.LogInfo($"Blacklisted Enemies: {string.Join(", ", enemyBlacklist)}");
		    for (int i = 0; i < __instance.amountCurve3Value; i++)
		    {
			    __instance.PickEnemies(__instance.enemiesDifficulty3.Where(x => !x.name.StartsWith("Enemy Group - ") && !enemyBlacklist.Contains(x.name)).ToList());
		    }
		    for (int j = 0; j < __instance.amountCurve2Value; j++)
		    {
			    __instance.PickEnemies(__instance.enemiesDifficulty2.Where(x => !enemyBlacklist.Contains(x.name)).ToList());
		    }
		    for (int k = 0; k < __instance.amountCurve1Value; k++)
		    {
			    __instance.PickEnemies(__instance.enemiesDifficulty1.Where(x => !enemyBlacklist.Contains(x.name)).ToList());
		    }
		    if (SemiFunc.RunGetDifficultyMultiplier3() > 0f)
		    {
			    __instance.despawnedTimeMultiplier = __instance.despawnTimeCurve_2.Evaluate(SemiFunc.RunGetDifficultyMultiplier3());
		    }
		    else if (SemiFunc.RunGetDifficultyMultiplier2() > 0f)
		    {
			    __instance.despawnedTimeMultiplier = __instance.despawnTimeCurve_1.Evaluate(SemiFunc.RunGetDifficultyMultiplier2());
		    }
		    else
		    {
			    __instance.despawnedTimeMultiplier = 1f;
		    }
		    __instance.totalAmount = __instance.amountCurve1Value + __instance.amountCurve2Value + __instance.amountCurve3Value;
		    
		    return false;
	    }

	    internal static void UpdateEnemyTimers(EnemyParent __instance)
	    {
		    // How long should enemies take to respawn after despawning?
		    // If the value is -1, use the vanilla timer
		    // If the value is 0, respawn instantly
		    // If the value is > 0, use that value
		    if (PluginLoader.despawnedTimer.Value >= 0f)
		    {
			    __instance.DespawnedTimeMin = PluginLoader.despawnedTimer.Value;
			    __instance.DespawnedTimeMax = PluginLoader.despawnedTimer.Value;
		    }
		    else
		    {
			    __instance.DespawnedTimeMin = 240f;
			    __instance.DespawnedTimeMax = 300f;
		    }
		    if (__instance.DespawnedTimer < __instance.DespawnedTimeMin || __instance.DespawnedTimer > __instance.DespawnedTimeMax)
		    {
			    __instance.DespawnedTimer = Random.Range(__instance.DespawnedTimeMin, __instance.DespawnedTimeMax);
		    }

		    // How long should enemies stay spawned?
		    // If the value is -1, use the vanilla timer
		    // If the value is 0, despawn instantly
		    // If the value is > 0, use that value
		    if (PluginLoader.spawnedTimer.Value >= 0f)
		    {
			    __instance.SpawnedTimeMin = PluginLoader.spawnedTimer.Value;
			    __instance.SpawnedTimeMax = PluginLoader.spawnedTimer.Value;
		    }
		    else
		    {
			    __instance.SpawnedTimeMin = 20f;
			    __instance.SpawnedTimeMax = 40f;
		    }
		    if (__instance.SpawnedTimer < __instance.SpawnedTimeMin || __instance.SpawnedTimer > __instance.SpawnedTimeMax)
		    {
			    __instance.SpawnedTimer = Random.Range(__instance.SpawnedTimeMin, __instance.SpawnedTimeMax);
		    }
	    }
	    
	    [HarmonyPatch(typeof(EnemyParent), "Awake")]
	    [HarmonyPostfix]
	    private static void EnemyParent_Awake(EnemyParent __instance)
	    {
		    UpdateEnemyTimers(__instance);
	    }
	    
	    [HarmonyPatch(typeof(EnemyDirector), "Start")]
	    [HarmonyPostfix]
	    private static void EnemyDirector_Start(EnemyDirector __instance)
	    {
		    __instance.debugNoSpawnedPause = true;
	    }
	    
	    [HarmonyPatch(typeof(SemiFunc), "EnemyPhysObjectSphereCheck")]
	    [HarmonyPrefix]
	    private static bool SemiFunc_EnemyPhysObjectSphereCheck(Vector3 _position, float _radius, ref bool __result)
	    {
		    if (!PluginLoader.skipCollisionCheck.Value)
			    return true;
	
		    __result = Physics.OverlapSphere(_position, _radius, SemiFunc.LayerMaskGetPhysGrabObject())
			    .Where(x => x.GetComponent<EnemyParent>() != null).ToArray().Length != 0;
		    return false;
	    }
	    
	    [HarmonyPatch(typeof(SemiFunc), "EnemyPhysObjectBoundingBoxCheck")]
	    [HarmonyPrefix]
	    private static bool SemiFunc_EnemyPhysObjectBoundingBoxCheck(Vector3 _currentPosition, Vector3 _checkPosition, Rigidbody _rigidbody, ref bool __result)
	    {
		    if (!PluginLoader.skipCollisionCheck.Value)
			    return true;

		    Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
		    foreach (Collider collider in _rigidbody.GetComponentsInChildren<Collider>())
		    {
			    if (bounds.size == Vector3.zero)
			    {
				    bounds = collider.bounds;
			    }
			    else
			    {
				    bounds.Encapsulate(collider.bounds);
			    }
		    }
		    Vector3 vector = _currentPosition - _rigidbody.transform.position;
		    Vector3 vector2 = bounds.center - _rigidbody.transform.position;
		    bounds.center = _checkPosition - vector + vector2;
		    bounds.size *= 1.2f;
		    Collider[] array = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, SemiFunc.LayerMaskGetPhysGrabObject());
		    for (int i = 0; i < array.Length; i++)
		    {
			    if (array[i].GetComponentInParent<Rigidbody>() != _rigidbody && array[i].GetComponentInParent<EnemyRigidbody>() == null)
			    {
					__result = true;
					return false;
			    }
		    }
		    __result = false;
		    return false;
	    }

	    private static bool viewCountWarning;
	    [HarmonyPatch(typeof(LevelGenerator), "EnemySpawn")]
	    [HarmonyPrefix]
	    private static bool LevelGenerator_EnemySpawn(LevelGenerator __instance, EnemySetup enemySetup)
	    {
		    // Ensure we don't use too many Photon views
		    if (PhotonNetwork.ViewCount < PhotonNetwork.MAX_VIEW_IDS - reservedViewCount)
		    {
			    viewCountWarning = false;
			    return true;
		    }

		    if (!viewCountWarning)
		    {
			    viewCountWarning = true;
			    PluginLoader.StaticLogger.LogWarning($"Multiplayer photon view limit reached! {__instance.EnemiesSpawnTarget}/{EnemyDirector.instance.totalAmount} enemies were spawned!");
		    }
		    return false;
	    }
    }
}
