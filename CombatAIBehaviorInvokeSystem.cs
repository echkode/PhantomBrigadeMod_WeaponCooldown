// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;

using PhantomBrigade;
using PhantomBrigade.AI.Components;
using PhantomBrigade.Data;
using PBActionUtility = PhantomBrigade.ActionUtility;
using PBAIUtility = PhantomBrigade.AI.AIUtility;
using PBDataHelperStats = PhantomBrigade.Data.DataHelperStats;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	public static class CombatAIBehaviorInvokeSystem
	{
		public static bool CheckSecondaryTrack(AIEntity agent, PlannedEquipmentUseRecord step, float turnStartTime)
		{
			if (step.m_ignoreActionCreation)
			{
				return false;
			}

			var lastAction = agent.hasLastSecondaryTrackActionTime
				? IDUtility.GetActionEntity((int)agent.lastSecondaryTrackActionTime.t)
				: null;
			if (lastAction == null)
			{
				return step.m_startTime >= turnStartTime;
			}

			var actionEndTime = lastAction.startTime.f + lastAction.duration.f;
			if (!lastAction.hasActiveEquipmentPart)
			{
				return step.m_startTime > actionEndTime;
			}

			var actionPartID = lastAction.activeEquipmentPart.equipmentID;
			if (step is ExtendedPlannedEquipmentUseRecord extended && extended.m_partID == actionPartID)
			{
				actionEndTime += extended.m_activationLockoutDuration;
			}

			return step.m_startTime > actionEndTime;
		}

		public static bool CollapseEquipmentUse(AIEntity agent, float startTime)
		{
			if (!agent.hasSuggestedEquipmentUse)
			{
				return false;
			}

			var suggestion = agent.suggestedEquipmentUse.suggestion;
			if (!PBAIUtility.IsPlannedActionUIDValid(suggestion.m_useRefUID))
			{
				Debug.LogErrorFormat(
					"Mod {0} ({1}) CollapseEquipmentUse(): suggested action has invalid UID | AI action type: {2}",
					ModLink.modIndex,
					ModLink.modID,
					suggestion.m_actionType);
				return false;
			}

			var unit = IDUtility.GetPersistentEntity(agent.contextLinkPersistent.id);
			if (unit == null)
			{
				return false;
			}

			if (LoggingToggles.AIBehaviorInvoke)
			{
				var combatant = IDUtility.GetCombatEntity(agent.contextLinkCombat.id);
				Debug.LogFormat(
					"Mod {0} ({1}) equipment use suggestion | agent ID: {2} | behavior: {3} | unit ID: {4} ({5}) | unit class: {6} | AI action type: {7} | ref ID: {8}",
					ModLink.modIndex,
					ModLink.modID,
					agent.id.id,
					combatant != null && combatant.hasAIBehaviorKey ? combatant.aIBehaviorKey.key : "<unknown>",
					unit.id.id,
					unit.hasNameInternal ? unit.nameInternal.s : "<no-name>",
					unit.hasDataKeyUnitClass ? unit.dataKeyUnitClass.s : "<unknown>",
					suggestion.m_actionType,
					suggestion.m_useRefUID);
			}

			var actionFromTable = PBAIUtility.GetActionFromTable(agent, suggestion.m_actionType);
			if (string.IsNullOrEmpty(actionFromTable))
			{
				return false;
			}

			var actionData = DataMultiLinker<DataContainerAction>.GetEntry(actionFromTable, false);
			if (actionData == null)
			{
				return false;
			}

			var dataCore = actionData.dataCore;
			if (dataCore.durationType == DurationType.Variable)
			{
				return false;
			}

			var useEquipmentDuration = dataCore.durationType == DurationType.Equipment
				&& actionData.dataEquipment != null
				&& actionData.dataEquipment.partUsed;
			var (partOK, part) = DataHelperAction.TryGetEquipmentPart(unit, actionData);
			if (useEquipmentDuration && !partOK)
			{
				return false;
			}

			var duration = useEquipmentDuration
				? PBDataHelperStats.GetCachedStatForPart(UnitStats.activationDuration, part)
				: dataCore.duration;

			if (partOK && agent.hasPlannedEquipmentUse)
			{
				var steps = agent.plannedEquipmentUse.steps;
				for (var i = steps.Count - 1; i >= 0; i -= 1)
				{
					var lastUseStep = steps[i];
					if (lastUseStep is ExtendedPlannedEquipmentUseRecord extended
						&& part.id.id == extended.m_partID)
					{
						var endTime = extended.EndTime + extended.m_activationLockoutDuration;
						if (extended.m_startTime <= startTime && startTime < endTime)
						{
							return false;
						}
					}
				}
			}
			else
			{
				var lastUseStep = PBAIUtility.GetLastPlannedEquipmentAction(agent);
				if (lastUseStep != null && lastUseStep.IsHappeningAtTime(startTime))
				{
					return false;
				}
			}

			var hasLockout = false;
			var lockoutDuration = 0f;
			if (partOK)
			{
				(hasLockout, lockoutDuration) = DataHelperStats.TryGetActivationLockoutDuration(part, dataCore);
			}
			var useStep = !hasLockout
				? new PlannedEquipmentUseRecord()
				: new ExtendedPlannedEquipmentUseRecord()
				{
					m_partID = part.id.id,
					m_activationLockoutDuration = lockoutDuration,
				};
			useStep.m_useRefUID = suggestion.m_useRefUID;
			useStep.m_actionType = suggestion.m_actionType;
			useStep.m_targetID = suggestion.m_targetID;
			useStep.m_startTime = startTime;
			useStep.m_duration = duration;
			useStep.m_heatDelta = PBActionUtility.GetHeatChange(actionFromTable, false, unit);
			useStep.m_endsPlanning = actionData.dataAI != null && actionData.dataAI.actionEndsPlanning;

			if (!agent.hasPlannedEquipmentUse)
			{
				agent.AddPlannedEquipmentUse(new List<PlannedEquipmentUseRecord>());
			}
			agent.plannedEquipmentUse.steps.Add(useStep);
			agent.ReplaceLastPlannedActionType(useStep.m_actionType);

			if (LoggingToggles.AIBehaviorInvoke)
			{
				var combatant = IDUtility.GetCombatEntity(agent.contextLinkCombat.id);
				Debug.LogFormat(
					"Mod {0} ({1}) planned use step | agent ID: {2} | behavior: {3} | action: {4} | AI action type: {5} | ref ID: {6} | ends planning: {7} | start time: {8:F3}s | duration: {9:F3}s | target ID: {10}",
					ModLink.modIndex,
					ModLink.modID,
					agent.id.id,
					combatant != null && combatant.hasAIBehaviorKey ? combatant.aIBehaviorKey.key : "<unknown>",
					actionData.key,
					useStep.m_actionType,
					useStep.m_useRefUID,
					useStep.m_endsPlanning,
					useStep.m_startTime,
					useStep.m_duration,
					useStep.m_targetID);
			}

			return true;
		}

		public static void CreatePlannedStepsForActions(
			HashSet<ActionEntity> ownedActions,
			PersistentEntity unit,
			Dictionary<string, AIActionType> lookupTable,
			List<PlannedEquipmentUseRecord> steps)
		{
			foreach (var action in ownedActions)
			{
				if (action.isDisposed)
				{
					continue;
				}
				if (action.isDestroyed)
				{
					continue;
				}
				if (!action.hasStartTime)
				{
					continue;
				}
				if (!action.hasDuration)
				{
					continue;
				}

				var endsPlanning = false;
				if (action.hasDataLinkAction)
				{
					var data = action.dataLinkAction.data;
					if (data.dataCore != null && data.dataCore.trackType == TrackType.Primary)
					{
						continue;
					}
					endsPlanning = data.dataAI != null && data.dataAI.actionEndsPlanning;
				}

				if (!lookupTable.TryGetValue(action.dataKeyAction.s, out var actionType))
				{
					actionType = AIActionType.None;
				}

				var partID = IDUtility.invalidID;
				var lockoutDuration = 0f;
				if (action.hasHitPredictions && action.hitPredictions.hitPredictions.Count != 0)
				{
					var hp = action.hitPredictions.hitPredictions[0];
					partID = hp.combatID;
					lockoutDuration = hp.time;
				}

				var plannedUse = partID == IDUtility.invalidID
					? new PlannedEquipmentUseRecord()
					: new ExtendedPlannedEquipmentUseRecord()
					{
						m_partID = partID,
						m_activationLockoutDuration = lockoutDuration,
					};
				plannedUse.m_actionType = actionType;
				plannedUse.m_useRefUID = action.hasAIActionRefID
					? action.aIActionRefID.id
					: 0;
				if (action.hasTargetedEntity)
				{
					plannedUse.m_targetID = action.targetedEntity.combatID;
				}
				plannedUse.m_startTime = action.startTime.f;
				plannedUse.m_duration = action.duration.f;
				plannedUse.m_heatDelta = PBActionUtility.GetHeatChange(action.dataKeyAction.s, false, unit);
				plannedUse.m_endsPlanning = endsPlanning;
				plannedUse.m_ignoreActionCreation = true;
				steps.Add(plannedUse);
			}
		}

		public static void LogEquipmentUseStep(AIEntity agent, PlannedEquipmentUseRecord step, float turnStartTime)
		{
			var unit = IDUtility.GetPersistentEntity(agent.contextLinkPersistent.id);
			var combatant = IDUtility.GetCombatEntity(agent.contextLinkCombat.id);
			Debug.LogFormat(
				"Mod {0} ({1}) attempting to create action for use step | turn start time: {2:F3}s | agent ID: {3} | behavior: {4} | unit: {5} ({6}) | AI action type: {7} | ref ID: {8} | ignore: {9} | ends planning: {10} | start time: {11:F3}s | duration: {12:F3}s | target ID: {13}",
				ModLink.modIndex,
				ModLink.modID,
				turnStartTime,
				agent.id.id,
				combatant != null && combatant.hasAIBehaviorKey ? combatant.aIBehaviorKey.key : "<unknown>",
				unit != null ? unit.id.id : IDUtility.invalidID,
				unit != null && unit.hasDataKeyUnitClass ? unit.dataKeyUnitClass.s : "<unknown>",
				step.m_actionType,
				step.m_useRefUID,
				step.m_ignoreActionCreation,
				step.m_endsPlanning,
				step.m_startTime,
				step.m_duration,
				step.m_targetID);
		}

		public static void LogActionUnavailable(AIEntity agent, CombatEntity combatant, PlannedEquipmentUseRecord step, DataContainerAction actionData, float turnStartTime)
		{
			var unit = IDUtility.GetPersistentEntity(agent.contextLinkPersistent.id);
			Debug.LogFormat(
				"Mod {0} ({1}) equipment action unavailable | turn start time: {2:F3}s | agent ID: {3} | behavior: {4} | unit ID: {5} ({6}) | action: {7} | AI action type: {8} | ref ID: {9} | start time: {10:F3}s",
				ModLink.modIndex,
				ModLink.modID,
				turnStartTime,
				agent.id.id,
				combatant != null && combatant.hasAIBehaviorKey ? combatant.aIBehaviorKey.key : "<unknown>",
				unit != null ? unit.id.id : IDUtility.invalidID,
				unit != null && unit.hasDataKeyUnitClass ? unit.dataKeyUnitClass.s : "<unknown>",
				actionData.key,
				step.m_actionType,
				step.m_useRefUID,
				step.m_startTime);
		}

		public static void LogEquipmentUseAction(AIEntity agent, PlannedEquipmentUseRecord step, ActionEntity action)
		{
			var unit = IDUtility.GetPersistentEntity(agent.contextLinkPersistent.id);
			var combatant = IDUtility.GetCombatEntity(agent.contextLinkCombat.id);
			Debug.LogFormat(
				"Mod {0} ({1}) equipment action created | agent ID: {2} | behavior: {3} | unit ID: {4} ({5}) | action ID: {6} ({7}) | start time: {8:F3}s | AI action type: {9} | ref ID: {10} | target ID: {11}",
				ModLink.modIndex,
				ModLink.modID,
				agent.id.id,
				combatant != null && combatant.hasAIBehaviorKey ? combatant.aIBehaviorKey.key : "<unknown>",
				unit != null ? unit.id.id : IDUtility.invalidID,
				unit != null && unit.hasDataKeyUnitClass ? unit.dataKeyUnitClass.s : "<unknown>",
				action.id.id,
				action.dataKeyAction.s,
				action.startTime.f,
				step.m_actionType,
				step.m_useRefUID,
				step.m_targetID);
		}
	}
}
