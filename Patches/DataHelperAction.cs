using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Data;
using PBDataHelperAction = PhantomBrigade.Data.DataHelperAction;
using PBDataHelperStats = PhantomBrigade.Data.DataHelperStats;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(PBDataHelperAction), "InstantiateAction", new System.Type[]
		{
			typeof(DataContainerAction),
			typeof(CombatEntity),
			typeof(string),
			typeof(float),
			typeof(bool),
		})]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Dha_InstantiateActionTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Store activation lockout duration and part ID when action is created. This is only for actions that use a part.
			// ActionEntity component HitPredictions is repurposed to store this information. I couldn't find anywhere else that
			// it's used so it seems safe to repurpose.

			var cm = new CodeMatcher(instructions, generator);
			var okLocal = generator.DeclareLocal(typeof(bool));
			var durationLocal = generator.DeclareLocal(typeof(float));
			var getCachedStatMethodInfo = AccessTools.DeclaredMethod(typeof(PBDataHelperStats), nameof(PBDataHelperStats.GetCachedStatForPart));
			var replaceDurationMethodInfo = AccessTools.DeclaredMethod(typeof(ActionEntity), nameof(ActionEntity.ReplaceDuration));
			var durationTypeFieldInfo = AccessTools.DeclaredField(typeof(DataBlockActionCore), nameof(DataBlockActionCore.durationType));
			var getCachedStatMatch = new CodeMatch(OpCodes.Call, getCachedStatMethodInfo);
			var replaceDurationMatch = new CodeMatch(OpCodes.Callvirt, replaceDurationMethodInfo);
			var durationTypeMatch = new CodeMatch(OpCodes.Ldfld, durationTypeFieldInfo);
			var branchMatch = new CodeMatch(OpCodes.Bne_Un);
			var loadEntity = new CodeInstruction(OpCodes.Ldloc_0);
			var loadPart = new CodeInstruction(OpCodes.Ldloc_2);
			var storeActivationLockout = CodeInstruction.Call(typeof(ActionUtility), nameof(ActionUtility.StoreActivationLockout));

			cm.MatchEndForward(getCachedStatMatch)
				.MatchEndForward(replaceDurationMatch)
				.Advance(1);
			cm.Insert(loadEntity);
			cm.CreateLabel(out var storeActivationLabel);
			cm.Advance(1)
				.InsertAndAdvance(loadPart)
				.InsertAndAdvance(storeActivationLockout);

			cm.MatchEndBackwards(durationTypeMatch)
				.MatchStartForward(branchMatch)
				.SetOperandAndAdvance(storeActivationLabel);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBDataHelperAction), nameof(PBDataHelperAction.IsAvailableAtTime))]
		[HarmonyPrefix]
		static bool Dha_IsAvailableAtTimePrefix(
			DataContainerAction data,
			CombatEntity combatEntity,
			float startTime,
			ref bool __result)
		{
			__result = DataHelperAction.IsAvailableAtTime(data, combatEntity, startTime);
			return false;
		}
	}
}
