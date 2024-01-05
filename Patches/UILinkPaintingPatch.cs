// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Data;
using PBCIViewCombatTimeline = CIViewCombatTimeline;
using PBActionUtility = PhantomBrigade.ActionUtility;

namespace EchKode.PBMods.WeaponCooldown
{
	using OkFloat = System.ValueTuple<bool, float>;

	static class UILinkPaintingPatch
	{
		internal static IEnumerable<CodeInstruction> Transpiler(
			IEnumerable<CodeInstruction> instructions,
			ILGenerator generator,
			OpCode loopBranch)
		{
			// Check that the action can be placed before max placement time. If not, set start time to max placement time
			// so the rest of the UI painting routine can complete.
			//
			// Show the late placement warning if the dash/melee action starts after max placement time.

			var cm = new CodeMatcher(instructions, generator);
			var okLocal = generator.DeclareLocal(typeof(bool));
			var getLastActionEndTimeMethodInfo = AccessTools.DeclaredMethod(typeof(PBActionUtility), nameof(PBActionUtility.GetLastActionTime));
			var getSimMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(DataShortcuts), nameof(DataShortcuts.sim));
			var getLastActionEndTimeMatch = new CodeMatch(OpCodes.Call, getLastActionEndTimeMethodInfo);
			var popMatch = new CodeMatch(OpCodes.Pop);
			var loadAddressMatch = new CodeMatch(OpCodes.Ldloca_S);
			var branchLessThanMatch = new CodeMatch(loopBranch);
			var branchMatch = new CodeMatch(OpCodes.Br_S);
			var loadConst2 = new CodeInstruction(OpCodes.Ldc_I4_2);
			var callTryPlaceAction = CodeInstruction.Call(typeof(CombatUIUtility), nameof(CombatUIUtility.TryPlaceAction));
			var dupe = new CodeInstruction(OpCodes.Dup);
			var loadOkField = CodeInstruction.LoadField(typeof(OkFloat), nameof(OkFloat.Item1));
			var loadStartTimeField = CodeInstruction.LoadField(typeof(OkFloat), nameof(OkFloat.Item2));
			var storeOk = new CodeInstruction(OpCodes.Stloc_S, okLocal);
			var loadOk = new CodeInstruction(OpCodes.Ldloc_S, okLocal);
			var getSim = new CodeInstruction(OpCodes.Call, getSimMethodInfo);
			var loadMaxPlacementTime = CodeInstruction.LoadField(typeof(DataContainerSettingsSimulation), nameof(DataContainerSettingsSimulation.maxActionTimePlacement));
			var ret = new CodeInstruction(OpCodes.Ret);
			var showWarningLate = CodeInstruction.Call(typeof(UILinkPaintingPatch), nameof(ShowWarningLate));
			var updatePredictionTimeTarget = loopBranch == OpCodes.Blt;

			cm.MatchEndForward(getLastActionEndTimeMatch)
				.Advance(1);
			var storeStartTime = cm.Instruction.Clone();

			cm.Advance(1);
			var deleteStart = cm.Pos;
			cm.MatchStartForward(popMatch);
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
			cm.MatchStartForward(branchLessThanMatch);
			if (updatePredictionTimeTarget)
			{
				cm.MatchStartBackwards(branchMatch)
					.Advance(-1)
					.MatchStartBackwards(branchMatch);
			}
			offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0)
				.Advance(offset);

			if (!updatePredictionTimeTarget)
			{
				cm.Labels.Clear();
			}

			cm.CreateLabel(out var skipMaxPlacementTimeLabel);
			var skipMaxPlacementTime = new CodeInstruction(OpCodes.Brtrue_S, skipMaxPlacementTimeLabel);
			cm.InsertAndAdvance(skipMaxPlacementTime)
				.InsertAndAdvance(getSim)
				.InsertAndAdvance(loadMaxPlacementTime)
				.InsertAndAdvance(storeStartTime)
				.InsertAndAdvance(showWarningLate);

			if (updatePredictionTimeTarget)
			{
				cm.Advance(4);
				deleteStart = cm.Pos;
				cm.MatchStartForward(branchLessThanMatch);
				offset = deleteStart - cm.Pos;
				cm.RemoveInstructionsWithOffsets(offset, 0)
					.Advance(offset);
				cm.Labels.Clear();
			}

			return cm.InstructionEnumeration();
		}

		internal static void ShowWarningLate()
		{
			PBCIViewCombatTimeline.ins.hideableWarningLate.SetVisible(true);
			var t = new Traverse(PBCIViewCombatTimeline.ins);
			t.Field<bool>("warningTimeoutLock").Value = true;
		}
	}
}
