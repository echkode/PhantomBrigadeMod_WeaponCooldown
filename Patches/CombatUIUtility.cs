using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Data;
using PBCombatUIUtility = PhantomBrigade.CombatUIUtility;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	using OkFloat = System.ValueTuple<bool, float>;
	using OkPartFloat = System.ValueTuple<bool, EquipmentEntity, float>;

	static partial class Patch
	{
		[HarmonyPatch(typeof(PBCombatUIUtility), nameof(PBCombatUIUtility.AttemptToFinishTimePlacement))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Cuiu_AttemptToFinishTimePlacementTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Replace logic that tries to place an action with a bit of nudging. There's some moving lines around to get
			// the start time clamped between turn start and max placement time in turn before calling a replacement function
			// that does the nudging and all. The replacement function is smart about detecting overlaps between actions with
			// activation lockout.

			var cm = new CodeMatcher(instructions, generator);
			var okLocal = generator.DeclareLocal(typeof(bool));
			var getDurationMethodInfo = AccessTools.DeclaredMethod(typeof(PBCombatUIUtility), nameof(PBCombatUIUtility.GetPaintedTimePlacementDuration));
			var clampMethodInfo = AccessTools.DeclaredMethod(
				typeof(Mathf),
				nameof(Mathf.Clamp),
				new System.Type[]
				{
					typeof(float),
					typeof(float),
					typeof(float),
				});
			var replaceTimeTargetMethodInfo = AccessTools.DeclaredMethod(typeof(CombatContext), nameof(CombatContext.ReplacePredictionTimeTarget));
			var getDurationMatch = new CodeMatch(OpCodes.Call, getDurationMethodInfo);
			var clampMatch = new CodeMatch(OpCodes.Call, clampMethodInfo);
			var load0Match = new CodeMatch(OpCodes.Ldc_I4_0);
			var loadAddressMatch = new CodeMatch(OpCodes.Ldloca_S);
			var replaceTimeTargetMatch = new CodeMatch(OpCodes.Callvirt, replaceTimeTargetMethodInfo);
			var branchMatch = new CodeMatch(OpCodes.Brfalse);
			var dupe = new CodeInstruction(OpCodes.Dup);
			var loadOkField = CodeInstruction.LoadField(typeof(OkFloat), nameof(OkFloat.Item1));
			var loadStartTimeField = CodeInstruction.LoadField(typeof(OkFloat), nameof(OkFloat.Item2));
			var storeOk = new CodeInstruction(OpCodes.Stloc_S, okLocal);
			var loadOk = new CodeInstruction(OpCodes.Ldloc_S, okLocal);
			var load0 = new CodeInstruction(OpCodes.Ldc_I4_0);
			var ret = new CodeInstruction(OpCodes.Ret);

			cm.MatchEndForward(getDurationMatch)
				.MatchEndForward(clampMatch)
				.Advance(1);
			var clampEnd = cm.Pos;
			cm.MatchEndBackwards(load0Match)
				.Advance(2);
			var offset = clampEnd - cm.Pos;
			var clampInstructions = cm.InstructionsWithOffsets(0, offset);
			cm.RemoveInstructionsWithOffsets(0, offset);

			cm.MatchStartBackwards(getDurationMatch)
				.InsertAndAdvance(clampInstructions)
				.Advance(-1);
			var storeStartTime = cm.Instruction.Clone();
			var loadStartTime = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);

			cm.MatchStartForward(loadAddressMatch)
				.RemoveInstructions(3);
			cm.SetInstruction(CodeInstruction.Call(typeof(CombatUIUtility), nameof(CombatUIUtility.TryPlaceActionWithNudge)));

			cm.Advance(-2)
				.SetInstruction(loadStartTime)
				.Advance(3)
				.InsertAndAdvance(dupe)
				.InsertAndAdvance(loadOkField)
				.InsertAndAdvance(storeOk)
				.InsertAndAdvance(loadStartTimeField)
				.InsertAndAdvance(storeStartTime)
				.InsertAndAdvance(loadOk)
				.RemoveInstructions(3);

			cm.CreateLabel(out var skipRetLabel);
			var skipRet = new CodeInstruction(OpCodes.Brtrue_S, skipRetLabel);
			cm.InsertAndAdvance(skipRet)
				.InsertAndAdvance(load0)
				.InsertAndAdvance(ret);

			cm.MatchEndForward(replaceTimeTargetMatch)
				.Advance(1);
			var deleteStart = cm.Pos;
			cm.MatchEndForward(replaceTimeTargetMatch)
				.MatchStartForward(branchMatch);
			offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatUIUtility), nameof(PBCombatUIUtility.AttemptToFinishTimePlacement))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Cuiu_AttemptToFinishTimePlacementTranspiler2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var cm = new CodeMatcher(instructions, generator);
			var onRangeEndMethodInfo = AccessTools.DeclaredMethod(
				typeof(WorldUICombat),
				nameof(WorldUICombat.OnRangeEnd),
				new System.Type[]
				{
					typeof(int),
					typeof(int),
				});
			var onRangeEndMatch = new CodeMatch(OpCodes.Call, onRangeEndMethodInfo);
			var hideOverlap = CodeInstruction.Call(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.HidePaintedOverlap));

			cm.MatchStartForward(onRangeEndMatch)
				.Advance(-2);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.Insert(hideOverlap)
				.AddLabels(labels);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatUIUtility), nameof(PBCombatUIUtility.IsIntervalOverlapped))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Cuiu_IsIntervalOverlappedTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Normal actions can compute their end times by adding act_duration to start time. Activation lockout changes that simple
			// calculation. An action with activation lockout may extend its end time by the activation lockout duration when it is being
			// compared against another action for the same part.
			//
			// If the proposed action and an existing action are for the same part and either one has activation lockout,
			// one or both end times will have to be adjusted to incorporate the lockout duration. Otherwise the comparison uses the
			// unadjusted end times.

			var cm = new CodeMatcher(instructions, generator);
			var okLocal = generator.DeclareLocal(typeof(bool));
			var partLocal = generator.DeclareLocal(typeof(EquipmentEntity));
			var adjustedEndTimeLocal = generator.DeclareLocal(typeof(float));
			var endTimeCompareLocal = generator.DeclareLocal(typeof(float));
			var endTimeTempLocal = generator.DeclareLocal(typeof(float));
			var getSharedInstanceMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(Contexts), nameof(Contexts.sharedInstance));
			var getDurationMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.duration));
			var getSharedInstanceMatch = new CodeMatch(OpCodes.Call, getSharedInstanceMethodInfo);
			var getDurationMatch = new CodeMatch(OpCodes.Callvirt, getDurationMethodInfo);
			var addMatch = new CodeMatch(OpCodes.Add);
			var endTimeMatch = new CodeMatch(OpCodes.Ldloc_3);
			var loadEndTime = new CodeInstruction(OpCodes.Ldloc_3);
			var storeEndTimeCompare = new CodeInstruction(OpCodes.Stloc_S, endTimeCompareLocal);
			var loadCombatEntity = new CodeInstruction(OpCodes.Ldloc_2);
			var loadActionData = new CodeInstruction(OpCodes.Ldarg_1);
			var adjustEndTimeFromData = CodeInstruction.Call(
				typeof(ActionUtility),
				nameof(ActionUtility.TryAdjustEndTime),
				new System.Type[]
				{
					typeof(CombatEntity),
					typeof(DataContainerAction),
					typeof(float),
				});
			var dupe = new CodeInstruction(OpCodes.Dup);
			var loadOKField = CodeInstruction.LoadField(typeof(OkPartFloat), nameof(OkPartFloat.Item1));
			var storeOK = new CodeInstruction(OpCodes.Stloc_S, okLocal);
			var loadOK = new CodeInstruction(OpCodes.Ldloc_S, okLocal);
			var loadPartField = CodeInstruction.LoadField(typeof(OkPartFloat), nameof(OkPartFloat.Item2));
			var storePart = new CodeInstruction(OpCodes.Stloc_S, partLocal);
			var loadPart = new CodeInstruction(OpCodes.Ldloc_S, partLocal);
			var loadEndTimeField = CodeInstruction.LoadField(typeof(OkPartFloat), nameof(OkPartFloat.Item3));
			var storeAdjustedEndTime = new CodeInstruction(OpCodes.Stloc_S, adjustedEndTimeLocal);
			var isSamePart = CodeInstruction.Call(typeof(ActionUtility), nameof(ActionUtility.IsSamePart));
			var adjustEndTimeForAction = CodeInstruction.Call(
				typeof(ActionUtility),
				nameof(ActionUtility.AdjustEndTime),
				new System.Type[]
				{
					typeof(ActionEntity),
					typeof(EquipmentEntity),
					typeof(float),
				});
			var loadAdjustedEndTime = new CodeInstruction(OpCodes.Ldloc_S, adjustedEndTimeLocal);
			var loadEndTimeCompare = new CodeInstruction(OpCodes.Ldloc_S, endTimeCompareLocal);

			cm.MatchStartForward(getSharedInstanceMatch)
				.InsertAndAdvance(loadCombatEntity)
				.InsertAndAdvance(loadActionData)
				.InsertAndAdvance(loadEndTime)
				.InsertAndAdvance(adjustEndTimeFromData)
				.InsertAndAdvance(dupe)
				.InsertAndAdvance(dupe)
				.InsertAndAdvance(loadOKField)
				.InsertAndAdvance(storeOK)
				.InsertAndAdvance(loadPartField)
				.InsertAndAdvance(storePart)
				.InsertAndAdvance(loadEndTimeField)
				.InsertAndAdvance(storeAdjustedEndTime);

			cm.MatchEndForward(getDurationMatch)
				.Advance(-1);
			var loadAction = cm.Instruction.Clone();
			cm.MatchEndForward(addMatch)
				.Advance(1);
			var storeActionEndTime = cm.Instruction.Clone();
			var loadActionEndTime = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);
			cm.Advance(1);
			cm.CreateLabel(out var skipAdjustLabel);
			var skipAdjust = new CodeInstruction(OpCodes.Brfalse_S, skipAdjustLabel);

			cm.InsertAndAdvance(loadEndTime)
				.InsertAndAdvance(storeEndTimeCompare)
				.InsertAndAdvance(loadAction)
				.InsertAndAdvance(loadPart)
				.InsertAndAdvance(isSamePart)
				.InsertAndAdvance(skipAdjust)
				.InsertAndAdvance(loadAction)
				.InsertAndAdvance(loadPart)
				.InsertAndAdvance(loadActionEndTime)
				.InsertAndAdvance(adjustEndTimeForAction)
				.InsertAndAdvance(storeActionEndTime)
				.InsertAndAdvance(loadOK)
				.InsertAndAdvance(skipAdjust)
				.InsertAndAdvance(loadAdjustedEndTime)
				.InsertAndAdvance(storeEndTimeCompare);
			cm.MatchStartForward(endTimeMatch)
				.Repeat(m => m.SetInstructionAndAdvance(loadEndTimeCompare));

			return cm.InstructionEnumeration();
		}
	}
}
