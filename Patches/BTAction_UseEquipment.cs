// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.AI.BT.Nodes;
using PhantomBrigade.AI.Components;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(BTAction_UseEquipment), "OnUpdate")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Bta_Ue_OnUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var cm = new CodeMatcher(instructions, generator);
			var isHappeningAtTimeMethodInfo = AccessTools.DeclaredMethod(typeof(PlannedEquipmentUseRecord), nameof(PlannedEquipmentUseRecord.IsHappeningAtTime));
			var isHappeningAtTimeMatch = new CodeMatch(OpCodes.Callvirt, isHappeningAtTimeMethodInfo);
			var retMatch = new CodeMatch(OpCodes.Ret);
			var branchMatch = new CodeMatch(OpCodes.Brfalse_S);
			var isInLockout = CodeInstruction.Call(typeof(AIUtility), nameof(AIUtility.IsInLockout));
			var load1 = new CodeInstruction(OpCodes.Ldc_I4_1);
			var ret = new CodeInstruction(OpCodes.Ret);

			var loadArguments = cm.Start().Instructions(3);

			cm.MatchEndForward(isHappeningAtTimeMatch)
				.MatchEndForward(retMatch)
				.Advance(1);
			var skipRetLabel = cm.Labels[cm.Labels.Count - 1];
			var skipRet = new CodeInstruction(OpCodes.Brfalse_S, skipRetLabel);

			cm.Insert(loadArguments);
			cm.CreateLabel(out var label);
			cm.MatchStartBackwards(branchMatch)
				.SetOperandAndAdvance(label)
				.MatchEndForward(retMatch)
				.Advance(1)
				.Advance(loadArguments.Count)
				.InsertAndAdvance(isInLockout)
				.InsertAndAdvance(skipRet)
				.InsertAndAdvance(load1)  // BTStatus.Running
				.InsertAndAdvance(ret);

			return cm.InstructionEnumeration();
		}
	}
}
