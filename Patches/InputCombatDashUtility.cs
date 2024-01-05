using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Combat.Systems;
using PBCombatUIUtility = PhantomBrigade.CombatUIUtility;

namespace EchKode.PBMods.WeaponCooldown
{
	using OkFloat = System.ValueTuple<bool, float>;

	static partial class Patch
	{
		[HarmonyPatch(typeof(InputCombatDashUtility), nameof(InputCombatDashUtility.AttemptTargeting))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Icdu_AttemptTargetingTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Check that the dash action can be placed before max placement time. If not, cancel placement.

			var cm = new CodeMatcher(instructions, generator);
			var okLocal = generator.DeclareLocal(typeof(bool));
			var getDurationMethodInfo = AccessTools.DeclaredMethod(typeof(PBCombatUIUtility), nameof(PBCombatUIUtility.GetPaintedTimePlacementDuration));
			var getCombatIDMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatEntity), nameof(CombatEntity.id));
			var getPaintedPathMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(InputContext), nameof(InputContext.paintedPath));
			var getDurationMatch = new CodeMatch(OpCodes.Call, getDurationMethodInfo);
			var getCombatIDMatch = new CodeMatch(OpCodes.Callvirt, getCombatIDMethodInfo);
			var loadAddressMatch = new CodeMatch(OpCodes.Ldloca_S);
			var branchLessThanMatch = new CodeMatch(OpCodes.Blt);
			var branchMatch = new CodeMatch(OpCodes.Br_S);
			var getPaintedPathMatch = new CodeMatch(OpCodes.Callvirt, getPaintedPathMethodInfo);
			var storeStartTime = new CodeInstruction(OpCodes.Stloc_3);
			var loadConst2 = new CodeInstruction(OpCodes.Ldc_I4_2);
			var callTryPlaceAction = CodeInstruction.Call(typeof(CombatUIUtility), nameof(CombatUIUtility.TryPlaceAction));
			var dupe = new CodeInstruction(OpCodes.Dup);
			var loadOkField = CodeInstruction.LoadField(typeof(OkFloat), nameof(OkFloat.Item1));
			var loadStartTimeField = CodeInstruction.LoadField(typeof(OkFloat), nameof(OkFloat.Item2));
			var storeOk = new CodeInstruction(OpCodes.Stloc_S, okLocal);
			var loadOk = new CodeInstruction(OpCodes.Ldloc_S, okLocal);
			var ret = new CodeInstruction(OpCodes.Ret);

			cm.MatchEndForward(getDurationMatch)
				.Advance(2);
			var deleteStart = cm.Pos;
			cm.MatchStartForward(getCombatIDMatch)
				.Advance(-2);
			var offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0)
				.Advance(offset);

			cm.MatchStartForward(loadAddressMatch)
				.SetInstructionAndAdvance(loadConst2)  // CombatUIUtility.ActionOverlapCheck.SecondaryTrack
				.RemoveInstructions(3)
				.InsertAndAdvance(callTryPlaceAction)
				.InsertAndAdvance(dupe)
				.InsertAndAdvance(loadOkField)
				.InsertAndAdvance(storeOk)
				.InsertAndAdvance(loadStartTimeField)
				.InsertAndAdvance(storeStartTime)
				.InsertAndAdvance(loadOk);

			deleteStart = cm.Pos;
			cm.MatchStartForward(branchLessThanMatch)
				.MatchStartBackwards(branchMatch)
				.Advance(-1)
				.MatchStartBackwards(branchMatch);
			offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0)
				.Advance(offset);

			cm.CreateLabel(out var skipRetLabel);
			var skipRet = new CodeInstruction(OpCodes.Brtrue_S, skipRetLabel);
			cm.InsertAndAdvance(skipRet)
				.InsertAndAdvance(ret)
				.Advance(4);

			deleteStart = cm.Pos;
			cm.MatchStartForward(getPaintedPathMatch)
				.Advance(-1);
			cm.Labels.Clear();
			cm.Advance(-1);
			offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0);

			return cm.InstructionEnumeration();
		}
	}
}
