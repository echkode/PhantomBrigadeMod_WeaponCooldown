// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Combat.Systems;
using PhantomBrigade.Data;
using PBDataHelperAction = PhantomBrigade.Data.DataHelperAction;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(InputCombatWaitDrawingUtility), nameof(InputCombatWaitDrawingUtility.AttemptFinish))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Icwdu_AttemptFinishTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Use max time placement instead of turn end.

			var cm = new CodeMatcher(instructions, generator);
			var getTurnLengthMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatContext), nameof(CombatContext.turnLength));
			var getSimMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(DataShortcuts), nameof(DataShortcuts.sim));
			var getTurnLengthMatch = new CodeMatch(OpCodes.Callvirt, getTurnLengthMethodInfo);
			var load1Match = new CodeMatch(OpCodes.Ldc_I4_1);
			var mulMatch = new CodeMatch(OpCodes.Mul);
			var getSim = new CodeInstruction(OpCodes.Call, getSimMethodInfo);
			var loadMaxTimePlacement = CodeInstruction.LoadField(typeof(DataContainerSettingsSimulation), nameof(DataContainerSettingsSimulation.maxActionTimePlacement));
			var add = new CodeInstruction(OpCodes.Add);

			cm.MatchEndForward(getTurnLengthMatch)
				.MatchStartForward(load1Match)
				.RemoveInstruction()  // Ldc_I4_1
				.RemoveInstruction()  // Add
				.MatchEndForward(mulMatch)
				.Advance(1)
				.InsertAndAdvance(getSim)
				.InsertAndAdvance(loadMaxTimePlacement)
				.InsertAndAdvance(add);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(InputCombatWaitDrawingUtility), nameof(InputCombatWaitDrawingUtility.AttemptFinish))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Icwdu_AttemptFinishTranspiler2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Use drawn duration instead of clamping to end of turn.

			var cm = new CodeMatcher(instructions, generator);
			var fieldInfo = AccessTools.DeclaredField(typeof(DataContainerSettingsSimulation), nameof(DataContainerSettingsSimulation.paintToSecondsScalar));
			var minMethodInfo = AccessTools.DeclaredMethod(typeof(Mathf), nameof(Mathf.Min), new System.Type[] { typeof(float), typeof(float) });
			var fieldMatch = new CodeMatch(OpCodes.Ldfld, fieldInfo);
			var mulMatch = new CodeMatch(OpCodes.Mul);
			var minMatch = new CodeMatch(OpCodes.Call, minMethodInfo);
			var pop = new CodeInstruction(OpCodes.Pop);

			cm.MatchEndForward(fieldMatch)
				.MatchEndForward(mulMatch)
				.Advance(1);
			var durationLocal = cm.Operand;
			var durationLoad = new CodeInstruction(OpCodes.Ldloc_S, durationLocal);
			cm.MatchEndForward(minMatch)
				.Advance(1)
				.InsertAndAdvance(pop)
				.Insert(durationLoad);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(InputCombatWaitDrawingUtility), nameof(InputCombatWaitDrawingUtility.AttemptFinish))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Icwdu_AttemptFinishTranspiler3(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Skip loop that splits wait action on turn boundary.

			var cm = new CodeMatcher(instructions, generator);
			var instantiateMethodInfo = AccessTools.DeclaredMethod(typeof(PBDataHelperAction), nameof(PBDataHelperAction.InstantiateSelectedActionEntity));
			var logMethodInfo = AccessTools.DeclaredMethod(typeof(Debug), nameof(Debug.Log), new System.Type[] { typeof(object) });
			var callMatch = new CodeMatch(OpCodes.Call, instantiateMethodInfo);
			var logMatch = new CodeMatch(OpCodes.Call, logMethodInfo);
			var branchMatch = new CodeMatch(OpCodes.Br);

			cm.MatchEndForward(callMatch)
				.MatchStartForward(branchMatch);
			var branchTarget = cm.Operand;
			var branch = new CodeInstruction(OpCodes.Br, branchTarget);
			cm.MatchEndForward(logMatch)
				.Advance(1)
				.Insert(branch);

			return cm.InstructionEnumeration();
		}
	}
}
