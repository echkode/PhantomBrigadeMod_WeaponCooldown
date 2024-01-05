using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PBCombatExecutionEndLateSystem = PhantomBrigade.Combat.Systems.CombatExecutionEndLateSystem;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(PBCombatExecutionEndLateSystem), "Execute", new System.Type[] { typeof(List<CombatEntity>) })]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Ceels_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Set up call to split long wait actions that cross into new turn on turn start boundary.

			var cm = new CodeMatcher(instructions, generator);
			var getStartTimeMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.startTime));
			var setDisposedMethodInfo = AccessTools.DeclaredPropertySetter(typeof(ActionEntity), nameof(ActionEntity.isDisposed));
			var getStartTimeMatch = new CodeMatch(OpCodes.Callvirt, getStartTimeMethodInfo);
			var setDisposedMatch = new CodeMatch(OpCodes.Callvirt, setDisposedMethodInfo);
			var convertMatch = new CodeMatch(OpCodes.Conv_R4);
			var addMatch = new CodeMatch(OpCodes.Add);
			var loadTurnStart = new CodeInstruction(OpCodes.Ldloc_2);
			var splitCall = CodeInstruction.Call(typeof(CombatExecutionEndLateSystem), nameof(CombatExecutionEndLateSystem.SplitWaitAction));

			cm.MatchStartForward(getStartTimeMatch)
				.Advance(-1);
			var actionEntityLocal = cm.Operand;
			var loadActionEntity = new CodeInstruction(OpCodes.Ldloc_S, actionEntityLocal);

			cm.MatchStartForward(addMatch)
				.Advance(1);
			var actionEndTimeLocal = cm.Operand;
			var loadActionEndTime = new CodeInstruction(OpCodes.Ldloc_S, actionEndTimeLocal);

			cm.MatchEndForward(setDisposedMatch)
				.MatchEndForward(addMatch)
				.Advance(2);
			var oldJumpTarget = new List<Label>(cm.Labels);

			cm.Labels.Clear();
			cm.CreateLabel(out var newJumpTarget);

			var branchIncrement = new CodeInstruction(OpCodes.Br_S, newJumpTarget);
			cm.InsertAndAdvance(branchIncrement)
				.Insert(loadActionEntity)
				.AddLabels(oldJumpTarget)
				.Advance(1)
				.InsertAndAdvance(loadActionEndTime)
				.InsertAndAdvance(loadTurnStart)
				.InsertAndAdvance(splitCall);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatExecutionEndLateSystem), "Execute", new System.Type[] { typeof(List<CombatEntity>) })]
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
