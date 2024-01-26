// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Combat.Systems;
using PhantomBrigade.Data;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(CombatExecutionEndLateSystem), nameof(CombatExecutionEndLateSystem.SplitWaitActions))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Ceels_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Don't split locked actions like crashes.

			var cm = new CodeMatcher(instructions, generator);
			var paintingTypeFieldInfo = AccessTools.DeclaredField(typeof(DataBlockActionCore), nameof(DataBlockActionCore.paintingType));
			var paintingTypeMatch = new CodeMatch(OpCodes.Ldfld, paintingTypeFieldInfo);
			var loadArgMatch = new CodeMatch(OpCodes.Ldarg_1);
			var loadLocking = CodeInstruction.LoadField(typeof(DataBlockActionCore), nameof(DataBlockActionCore.locking));
			var ret = new CodeInstruction(OpCodes.Ret);

			cm.MatchStartForward(paintingTypeMatch)
				.Advance(-1);
			var loadDataCore = cm.Instruction.Clone();

			cm.MatchStartForward(loadArgMatch);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.CreateLabel(out var skipRetLabel);
			var skipRet = new CodeInstruction(OpCodes.Brfalse_S, skipRetLabel);

			cm.Insert(loadDataCore)
				.AddLabels(labels)
				.Advance(1)
				.InsertAndAdvance(loadLocking)
				.InsertAndAdvance(skipRet)
				.InsertAndAdvance(ret);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(CombatExecutionEndLateSystem), "Execute", new System.Type[] { typeof(List<CombatEntity>) })]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Ceels_ExecuteTranspiler2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Patch in new function to adjust end time of an action with activation lockout. This is to prevent the action from
			// being destroyed too early.

			var cm = new CodeMatcher(instructions, generator);
			var getDurationMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.duration));
			var getDurationMatch = new CodeMatch(OpCodes.Callvirt, getDurationMethodInfo);
			var callAdjustEndTime = CodeInstruction.Call(
				typeof(ActionUtility),
				nameof(ActionUtility.AdjustEndTime),
				new System.Type[]
				{
					typeof(ActionEntity),
					typeof(float),
				});

			cm.MatchStartForward(getDurationMatch)
				.Advance(-1);
			var loadActionEntity = cm.Instruction.Clone();
			cm.Advance(4);
			var storeEndTime = cm.Instruction.Clone();
			cm.Advance(1);
			var loadEndTime = cm.Instruction.Clone();
			cm.InsertAndAdvance(loadActionEntity)
				.InsertAndAdvance(loadEndTime)
				.InsertAndAdvance(callAdjustEndTime)
				.InsertAndAdvance(storeEndTime);

			return cm.InstructionEnumeration();
		}
	}
}
