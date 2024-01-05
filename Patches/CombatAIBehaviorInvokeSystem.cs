using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.AI.Components;
using PhantomBrigade.Components;
using PhantomBrigade.Data;
using PBAIUtility = PhantomBrigade.AI.AIUtility;
using PBCombatAIBehaviorInvokeSystem = PhantomBrigade.AI.Systems.CombatAIBehaviorInvokeSystem;
using PBDataHelperAction = PhantomBrigade.Data.DataHelperAction;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(PBCombatAIBehaviorInvokeSystem), "CollapseEquipmentUse")]
		[HarmonyPrefix]
		static bool Caibis_CollapseEquipmentUsePrefix(AIEntity p_entityToAdvance, float p_startTime, ref bool __result)
		{
			__result = CombatAIBehaviorInvokeSystem.CollapseEquipmentUse(p_entityToAdvance, p_startTime);
			return false;
		}

		[HarmonyPatch(typeof(PBCombatAIBehaviorInvokeSystem), "CreateStartingPlannedSteps")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Caibis_CreateStartingPlannedStepsTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Use the extended planning record to store part ID and lockout duration for actions with those.
			// Replace the loop through the owned actions with a call to a function.

			var cm = new CodeMatcher(instructions, generator);
			var getPersistentEntityMethodInfo = AccessTools.DeclaredMethod(
				typeof(IDUtility),
				nameof(IDUtility.GetPersistentEntity),
				new System.Type[]
				{
					typeof(int),
				});
			var getPlannedUseMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(AIEntity), nameof(AIEntity.plannedEquipmentUse));
			var tmpTableFieldInfo = AccessTools.DeclaredField(typeof(PBCombatAIBehaviorInvokeSystem), "m_tmpReverseActonTable");
			var getPersistentEntityMatch = new CodeMatch(OpCodes.Call, getPersistentEntityMethodInfo);
			var getPlannedUseMatch = new CodeMatch(OpCodes.Callvirt, getPlannedUseMethodInfo);
			var tmpTableMatch = new CodeMatch(OpCodes.Ldfld, tmpTableFieldInfo);
			var popMatch = new CodeMatch(OpCodes.Pop);
			var endFinallyMatch = new CodeMatch(OpCodes.Endfinally);
			var createPlannedSteps = CodeInstruction.Call(typeof(CombatAIBehaviorInvokeSystem), nameof(CombatAIBehaviorInvokeSystem.CreatePlannedStepsForActions));

			cm.MatchEndForward(getPersistentEntityMatch)
				.Advance(2);
			var loadUnit = cm.Instruction.Clone();

			cm.MatchStartForward(getPlannedUseMatch)
				.Advance(4);
			var deleteStart = cm.Pos;
			cm.MatchStartForward(tmpTableMatch)
				.Advance(-1);
			cm.Labels.Clear();
			cm.Advance(-1);
			var offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0)
				.Advance(offset)
				.InsertAndAdvance(loadUnit)
				.Advance(2);

			deleteStart = cm.Pos;
			cm.MatchStartForward(popMatch);
			offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0)
				.Advance(offset)
				.Advance(1)
				.InsertAndAdvance(createPlannedSteps);

			deleteStart = cm.Pos;
			cm.MatchStartForward(endFinallyMatch);
			offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatAIBehaviorInvokeSystem), "CreateUseActions")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Caibis_LogCreateUseActionsTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Add call to log created action.

			var cm = new CodeMatcher(instructions, generator);
			var contextLinkCombatMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(AIEntity), nameof(AIEntity.contextLinkCombat));
			var startTimeFieldInfo = AccessTools.DeclaredField(typeof(PlannedEquipmentUseRecord), nameof(PlannedEquipmentUseRecord.m_startTime));
			var instantiateActionMethodInfo = AccessTools.DeclaredMethod(
				typeof(PBDataHelperAction),
				nameof(PBDataHelperAction.InstantiateAction),
				new System.Type[]
				{
					typeof(CombatEntity),
					typeof(string),
					typeof(float),
					typeof(bool).MakeByRefType(),
					typeof(bool),
				});
			var hasDurationMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.hasDuration));
			var contextLinkCombatMatch = new CodeMatch(OpCodes.Callvirt, contextLinkCombatMethodInfo);
			var startTimeMatch = new CodeMatch(OpCodes.Ldfld, startTimeFieldInfo);
			var instantiateActionMatch = new CodeMatch(OpCodes.Call, instantiateActionMethodInfo);
			var branchTrueMatch = new CodeMatch(OpCodes.Brtrue_S);
			var hasDurationMatch = new CodeMatch(OpCodes.Callvirt, hasDurationMethodInfo);
			var loadAddressMatch = new CodeMatch(OpCodes.Ldloca_S);
			var loadToggle = CodeInstruction.LoadField(typeof(LoggingToggles), nameof(LoggingToggles.AIBehaviorInvoke));
			var logEquipmentUseAction = CodeInstruction.Call(typeof(CombatAIBehaviorInvokeSystem), nameof(CombatAIBehaviorInvokeSystem.LogEquipmentUseAction));

			cm.MatchStartForward(contextLinkCombatMatch)
				.Advance(-1);
			var loadAgent = cm.Instruction.Clone();

			cm.MatchStartForward(startTimeMatch)
				.Advance(-1);
			var loadStep = cm.Instruction.Clone();

			cm.MatchEndForward(instantiateActionMatch)
				.MatchEndForward(branchTrueMatch)
				.Advance(1);
			var loadAction = cm.Instruction.Clone();

			cm.MatchEndForward(hasDurationMatch)
				.MatchStartForward(loadAddressMatch);
			var skipLogLabel = cm.Labels[0];
			var skipLog = new CodeInstruction(OpCodes.Brfalse_S, skipLogLabel);

			cm.Insert(loadToggle);
			cm.CreateLabel(out var logLabel);
			cm.Advance(1)
				.InsertAndAdvance(skipLog)
				.InsertAndAdvance(loadAgent)
				.InsertAndAdvance(loadStep)
				.InsertAndAdvance(loadAction)
				.InsertAndAdvance(logEquipmentUseAction);

			cm.MatchEndBackwards(hasDurationMatch)
				.Advance(1)
				.SetOperandAndAdvance(logLabel);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatAIBehaviorInvokeSystem), "CreateUseActions")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Caibis_LogCreateUseActionsTranspiler1(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Add call if action isn't available at planning time.

			var cm = new CodeMatcher(instructions, generator);
			var contextLinkCombatMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(AIEntity), nameof(AIEntity.contextLinkCombat));
			var getCombatEntityMethodInfo = AccessTools.DeclaredMethod(typeof(IDUtility), nameof(IDUtility.GetCombatEntity));
			var startTimeFieldInfo = AccessTools.DeclaredField(typeof(PlannedEquipmentUseRecord), nameof(PlannedEquipmentUseRecord.m_startTime));
			var getEntryMethodInfo = AccessTools.DeclaredMethod(typeof(DataMultiLinker<DataContainerAction>), nameof(DataMultiLinker<DataContainerAction>.GetEntry));
			var isAvailableMethodInfo = AccessTools.DeclaredMethod(typeof(PBDataHelperAction), nameof(DataHelperAction.IsAvailableAtTime));
			var contextLinkCombatMatch = new CodeMatch(OpCodes.Callvirt, contextLinkCombatMethodInfo);
			var getCombatEntityMatch = new CodeMatch(OpCodes.Call, getCombatEntityMethodInfo);
			var startTimeMatch = new CodeMatch(OpCodes.Ldfld, startTimeFieldInfo);
			var getEntryMatch = new CodeMatch(OpCodes.Call, getEntryMethodInfo);
			var isAvailableMatch = new CodeMatch(OpCodes.Call, isAvailableMethodInfo);
			var loadToggle = CodeInstruction.LoadField(typeof(LoggingToggles), nameof(LoggingToggles.AIBehaviorInvoke));
			var loadTurnStartTime = new CodeInstruction(OpCodes.Ldloc_3);
			var logUnavailable = CodeInstruction.Call(typeof(CombatAIBehaviorInvokeSystem), nameof(CombatAIBehaviorInvokeSystem.LogActionUnavailable));

			cm.MatchStartForward(contextLinkCombatMatch)
				.Advance(-1);
			var loadAgent = cm.Instruction.Clone();

			cm.MatchEndForward(getCombatEntityMatch)
				.Advance(1);
			var loadCombatEntity = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);

			cm.MatchStartForward(startTimeMatch)
				.Advance(-1);
			var loadStep = cm.Instruction.Clone();

			cm.MatchEndForward(getEntryMatch)
				.Advance(2);
			var loadActionData = cm.Instruction.Clone();

			cm.MatchEndForward(isAvailableMatch)
				.Advance(2);
			cm.CreateLabel(out var availableLabel);
			var jumpToAvailable = new CodeInstruction(OpCodes.Brtrue_S, availableLabel);

			cm.Advance(-1);
			var notAvailableLabel = cm.Operand;
			var skipLog = new CodeInstruction(OpCodes.Brfalse_S, notAvailableLabel);
			var jumpToNotAvailable = new CodeInstruction(OpCodes.Br, notAvailableLabel);

			cm.SetInstructionAndAdvance(jumpToAvailable)
				.InsertAndAdvance(loadToggle)
				.InsertAndAdvance(skipLog)
				.InsertAndAdvance(loadAgent)
				.InsertAndAdvance(loadCombatEntity)
				.InsertAndAdvance(loadStep)
				.InsertAndAdvance(loadActionData)
				.InsertAndAdvance(loadTurnStartTime)
				.InsertAndAdvance(logUnavailable)
				.InsertAndAdvance(jumpToNotAvailable);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatAIBehaviorInvokeSystem), "CreateUseActions")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Caibis_LogCreateUseActionsTranspiler2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Add call to planned equipment use step.

			var cm = new CodeMatcher(instructions, generator);
			var contextLinkCombatMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(AIEntity), nameof(AIEntity.contextLinkCombat));
			var startTimeFieldInfo = AccessTools.DeclaredField(typeof(PlannedEquipmentUseRecord), nameof(PlannedEquipmentUseRecord.m_startTime));
			var contextLinkCombatMatch = new CodeMatch(OpCodes.Callvirt, contextLinkCombatMethodInfo);
			var startTimeMatch = new CodeMatch(OpCodes.Ldfld, startTimeFieldInfo);
			var loadToggle = CodeInstruction.LoadField(typeof(LoggingToggles), nameof(LoggingToggles.AIBehaviorInvoke));
			var loadTurnStartTime = new CodeInstruction(OpCodes.Ldloc_3);
			var logStep = CodeInstruction.Call(typeof(CombatAIBehaviorInvokeSystem), nameof(CombatAIBehaviorInvokeSystem.LogEquipmentUseStep));

			cm.MatchStartForward(contextLinkCombatMatch)
				.Advance(-1);
			var loadAgent = cm.Instruction.Clone();

			cm.MatchStartForward(startTimeMatch)
				.Advance(-1);
			var loadStep = cm.Instruction.Clone();
			cm.CreateLabel(out var skipLogLabel);
			var skipLog = new CodeInstruction(OpCodes.Brfalse_S, skipLogLabel);
			cm.InsertAndAdvance(loadToggle)
				.InsertAndAdvance(skipLog)
				.InsertAndAdvance(loadAgent)
				.InsertAndAdvance(loadStep)
				.InsertAndAdvance(loadTurnStartTime)
				.InsertAndAdvance(logStep);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatAIBehaviorInvokeSystem), "CreateUseActions")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Caibis_CreateUseActionsTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Store the secondary track action that gets created so it can be used the next time around the loop
			// in CheckSecondaryTrack.

			var cm = new CodeMatcher(instructions, generator);
			var hasSecondaryTrackActionMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(AIEntity), nameof(AIEntity.hasLastSecondaryTrackActionTime));
			var hasDurationMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.hasDuration));
			var getIDMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.id));
			var replaceSecondaryTrackActionMethodInfo = AccessTools.DeclaredMethod(typeof(AIEntity), nameof(AIEntity.ReplaceLastSecondaryTrackActionTime));
			var hasSecondaryTrackActionMatch = new CodeMatch(OpCodes.Callvirt, hasSecondaryTrackActionMethodInfo);
			var hasDurationMatch = new CodeMatch(OpCodes.Callvirt, hasDurationMethodInfo);
			var addMatch = new CodeMatch(OpCodes.Add);
			var getID = new CodeInstruction(OpCodes.Callvirt, getIDMethodInfo);
			var loadID = CodeInstruction.LoadField(typeof(Id), nameof(Id.id));
			var convertFloat = new CodeInstruction(OpCodes.Conv_R4);
			var replaceSecondaryTrackAction = new CodeInstruction(OpCodes.Callvirt, replaceSecondaryTrackActionMethodInfo);

			cm.MatchStartForward(hasSecondaryTrackActionMatch)
				.Advance(-1);
			var loadAIEntity = cm.Instruction.Clone();

			cm.MatchEndForward(hasDurationMatch)
				.Advance(2)
				.InsertAndAdvance(loadAIEntity)
				.Advance(1)
				.InsertAndAdvance(getID)
				.InsertAndAdvance(loadID)
				.InsertAndAdvance(convertFloat)
				.InsertAndAdvance(replaceSecondaryTrackAction);
			var deletePos = cm.Pos;
			cm.MatchEndForward(addMatch)
				.Advance(1);
			var offset = deletePos - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatAIBehaviorInvokeSystem), "CreateUseActions")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Caibis_CreateUseActionsTranspiler2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Don't set targetedEntity on action if the step targetID is invalid.

			var cm = new CodeMatcher(instructions, generator);
			var replaceTargetedEntityMethodInfo = AccessTools.DeclaredMethod(typeof(ActionEntity), nameof(ActionEntity.ReplaceTargetedEntity));
			var replaceTargetedEntityMatch = new CodeMatch(OpCodes.Callvirt, replaceTargetedEntityMethodInfo);
			var loadInvalidID = new CodeInstruction(OpCodes.Ldc_I4, IDUtility.invalidID);

			cm.MatchEndForward(replaceTargetedEntityMatch)
				.Advance(1);
			cm.CreateLabel(out var skipLabel);
			var skipReplace = new CodeInstruction(OpCodes.Beq_S, skipLabel);

			var loadActionMatch = new CodeMatch(cm.Opcode, cm.Operand);
			cm.Advance(-1)
				.MatchStartBackwards(loadActionMatch);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			var loadTargetID = cm.InstructionsWithOffsets(1, 2);

			cm.Insert(loadTargetID)
				.AddLabels(labels)
				.Advance(loadTargetID.Count)
				.InsertAndAdvance(loadInvalidID)
				.InsertAndAdvance(skipReplace);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatAIBehaviorInvokeSystem), "CreateUseActions")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Caibis_CreateUseActionsTranspiler3(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Replace conditional check on step start time and ignore flag with a call to CheckSecondaryTrack().

			var cm = new CodeMatcher(instructions, generator);
			var hasSecondaryTrackActionMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(AIEntity), nameof(AIEntity.hasLastSecondaryTrackActionTime));
			var getSecondaryTrackActionMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(AIEntity), nameof(AIEntity.lastSecondaryTrackActionTime));
			var startTimeFieldInfo = AccessTools.DeclaredField(typeof(PlannedEquipmentUseRecord), nameof(PlannedEquipmentUseRecord.m_startTime));
			var hasSecondaryTrackActionMatch = new CodeMatch(OpCodes.Callvirt, hasSecondaryTrackActionMethodInfo);
			var getSecondaryTrackActionMatch = new CodeMatch(OpCodes.Callvirt, getSecondaryTrackActionMethodInfo);
			var branchMatch = new CodeMatch(OpCodes.Brtrue);
			var startTimeMatch = new CodeMatch(OpCodes.Ldfld, startTimeFieldInfo);
			var loadTurnStartTime = new CodeInstruction(OpCodes.Ldloc_3);
			var checkSecondaryTrack = CodeInstruction.Call(typeof(CombatAIBehaviorInvokeSystem), nameof(CombatAIBehaviorInvokeSystem.CheckSecondaryTrack));

			cm.MatchStartForward(hasSecondaryTrackActionMatch)
				.Advance(-1);
			var loadAIEntity = cm.Instruction.Clone();

			var deleteStart = cm.Pos;
			cm.MatchEndForward(getSecondaryTrackActionMatch)
				.Advance(2);
			var offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0)
				.Advance(offset);

			cm.MatchStartForward(startTimeMatch)
				.Advance(-1);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.Insert(loadAIEntity)
				.AddLabels(labels)
				.Advance(2)
				.InsertAndAdvance(loadTurnStartTime)
				.InsertAndAdvance(checkSecondaryTrack);

			deleteStart = cm.Pos;
			cm.MatchStartForward(branchMatch)
				.Advance(-1);
			offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0)
				.Advance(offset)
				.SetOpcodeAndAdvance(OpCodes.Brfalse);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCombatAIBehaviorInvokeSystem.PlanningNode), nameof(PBCombatAIBehaviorInvokeSystem.PlanningNode.UpdateTimes))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Caibis_Pn_UpdateTimesTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Add a small fudge factor to end times of actions. A number of routines check for action overlap and may use
			// less than or greater than when comparing start and end times. If no buffer is added, some of those checks
			// may fail because the next action start time is equal to the previous action end time.

			var cm = new CodeMatcher(instructions, generator);
			var getLastPlannedMovementTimeMethodInfo = AccessTools.DeclaredMethod(typeof(PBAIUtility), nameof(PBAIUtility.GetLastPlannedMovementTime));
			var getLastPlannedEquipmentTimeMethodInfo = AccessTools.DeclaredMethod(typeof(PBAIUtility), nameof(PBAIUtility.GetLastPlannedEquipmentTime));
			var getLastPlannedMovementTimeMatch = new CodeMatch(OpCodes.Call, getLastPlannedMovementTimeMethodInfo);
			var getLastPlannedEquipmentTimeMatch = new CodeMatch(OpCodes.Call, getLastPlannedEquipmentTimeMethodInfo);
			var loadExtraTime = new CodeInstruction(OpCodes.Ldc_R4, 0.0005f);
			var add = new CodeInstruction(OpCodes.Add);

			cm.MatchEndForward(getLastPlannedMovementTimeMatch)
				.Advance(1)
				.InsertAndAdvance(loadExtraTime)
				.InsertAndAdvance(add);

			cm.MatchEndForward(getLastPlannedEquipmentTimeMatch)
				.Advance(1)
				.InsertAndAdvance(loadExtraTime)
				.InsertAndAdvance(add);

			return cm.InstructionEnumeration();
		}
	}
}
