// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PBActionUtility = PhantomBrigade.ActionUtility;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.OnPartDestruction))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Eu_OnPartDestructionTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Mark unit as wrecked before calling ActionUtility.CrashEntity(). This is because CrashEntity() has been patched
			// to preserve actions with activation lockouts. We don't want to keep any actions when a unit is destroyed so the
			// CrashEntity() function checks if the unit has been wrecked.

			var cm = new CodeMatcher(instructions, generator);
			var crashEntityMethodInfo = AccessTools.DeclaredMethod(typeof(PBActionUtility), nameof(PBActionUtility.CrashEntity));
			var crashEntityMatch = new CodeMatch(OpCodes.Call, crashEntityMethodInfo);
			var branchMatch = new CodeMatch(OpCodes.Br);
			var callMatch = new CodeMatch(OpCodes.Callvirt);

			cm.MatchEndForward(crashEntityMatch)
				.Advance(1)
				.MatchStartForward(crashEntityMatch);
			var deleteEnd = cm.Pos;
			cm.MatchEndBackwards(branchMatch)
				.Advance(1);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			var offset = deleteEnd - cm.Pos;
			var crashCallInstructions = cm.InstructionsWithOffsets(0, offset);
			cm.RemoveInstructionsWithOffsets(0, offset);
			cm.AddLabels(labels);
			cm.MatchEndForward(callMatch)
				.Advance(1)
				.MatchEndForward(callMatch)
				.Advance(1);
			cm.Insert(crashCallInstructions);

			return cm.InstructionEnumeration();
		}
	}
}
