﻿using BepInEx.Bootstrap;
using HarmonyLib;
using Jotunn.Managers;
using System;
using UnityEngine;
using static PlanBuild.PlanBuild;
using static PlanBuild.ShaderHelper;

namespace PlanBuild
{
    class Patches
    {
        public const string buildCameraGUID = "org.dkillebrew.plugins.valheim.buildCamera";
        public const string buildShareGUID = "com.valheim.cr_advanced_builder";
        public const string craftFromContainersGUID = "aedenthorn.CraftFromContainers";

        [HarmonyPatch(typeof(PieceManager), "RegisterInPieceTables")]
        [HarmonyPrefix]
        static void PieceManager_RegisterInPieceTables_Prefix()
        {
            PlanBuild.Instance.ScanHammer();
        }

        [HarmonyPatch(declaringType: typeof(Player), methodName: "HaveRequirements", argumentTypes: new Type[] { typeof(Piece), typeof(Player.RequirementMode) })]
        [HarmonyPrefix]
        static bool Player_HaveRequirements_Prefix(Player __instance, Piece piece, ref bool __result)
        {
            if (PlanBuild.showAllPieces.Value)
            {
                return true;
            }
            if (PlanPiecePrefabConfig.planToOriginalMap.TryGetValue(piece, out Piece originalPiece))
            {
                __result = __instance.HaveRequirements(originalPiece, Player.RequirementMode.IsKnown);
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Player), "SetupPlacementGhost")]
        [HarmonyPrefix]
        static void Player_SetupPlacementGhost_Prefix()
        { 
            PlanPiece.m_forceDisableInit = true;
        }

        [HarmonyPatch(typeof(Player), "SetupPlacementGhost")]
        [HarmonyPostfix]
        static void Player_SetupPlacementGhost_Postfix(GameObject ___m_placementGhost)
        { 
            PlanPiece.m_forceDisableInit = false;
            if (___m_placementGhost != null && configTransparentGhostPlacement.Value)
            {
                ShaderHelper.UpdateTextures(___m_placementGhost, ShaderState.Supported);
            }
        }

        private static bool interceptGetPrefab = true;
         
        [HarmonyPatch(typeof(ZNetScene), "GetPrefab", new Type[] { typeof(int) })]
        [HarmonyPostfix]
        static void ZNetScene_GetPrefab_Postfix(ZNetScene __instance, int hash, ref GameObject __result)
        {
            if(__result == null
                && interceptGetPrefab)
            {
                interceptGetPrefab = false;
                PlanBuild.Instance.ScanHammer(true);
                __result = __instance.GetPrefab(hash);
                interceptGetPrefab = true;
            } 
        }

        internal static void Apply(Harmony harmony)
        {
            harmony.PatchAll(typeof(Patches));
            harmony.PatchAll(typeof(PlanPiece));
            if (Chainloader.PluginInfos.ContainsKey(buildCameraGUID))
            {
                logger.LogInfo("Applying BuildCamera patches");
                harmony.PatchAll(typeof(PatcherBuildCamera));
            }
            if (Chainloader.PluginInfos.ContainsKey(craftFromContainersGUID))
            {
                logger.LogInfo("Applying CraftFromContainers patches");
                harmony.PatchAll(typeof(PatcherCraftFromContainers));
            }
            HarmonyLib.Patches patches = Harmony.GetPatchInfo(typeof(Player).GetMethod("OnSpawned"));
            if (patches?.Owners.Contains(buildShareGUID) == true)
            {
                logger.LogInfo("Applying BuildShare patches");
                harmony.PatchAll(typeof(PatcherBuildShare));
            }
        }
    }
}
