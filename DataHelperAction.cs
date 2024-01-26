// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;

using PhantomBrigade;
using PhantomBrigade.Data;
using PhantomBrigade.Functions;
using PBDataHelperAction = PhantomBrigade.Data.DataHelperAction;
using PBDataHelperStats = PhantomBrigade.Data.DataHelperStats;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	public static class DataHelperAction
	{
		public static bool IsAvailableAtTime(
			DataContainerAction actionData,
			CombatEntity combatEntity,
			float startTime)
		{
			var unit = IDUtility.GetLinkedPersistentEntity(combatEntity);
			if (!PBDataHelperAction.IsAvailable(actionData, unit))
			{
				if (LoggingToggles.AIBehaviorInvoke)
				{
					Debug.LogFormat(
						"Mod {0} ({1}) IsAvailableAtTime(): action is not available at time | time: {2:F3}s | unit: {3} | action: {4}",
						ModLink.modIndex,
						ModLink.modID,
						startTime,
						unit != null ? "P-" + unit.id.id : "C-" + combatEntity.id.id,
						actionData.key);
				}
				return false;
			}

			var dataCore = actionData.dataCore;
			if (dataCore.durationType == DurationType.Variable)
			{
				// !!! Assumes that this function is called for fixed duration actions.
				return false;
			}

			var (partOK, part) = TryGetEquipmentPart(unit, actionData);
			var useEquipmentDuration = dataCore.durationType == DurationType.Equipment
				&& actionData.dataEquipment != null
				&& actionData.dataEquipment.partUsed;
			if (!partOK && useEquipmentDuration)
			{
				if (LoggingToggles.AIBehaviorInvoke)
				{
					Debug.LogFormat(
						"Mod {0} ({1}) IsAvailableAtTime(): action uses equipment but part is not OK | time: {2:F3}s | unit: {3} | action: {4}",
						ModLink.modIndex,
						ModLink.modID,
						startTime,
						unit != null ? "P-" + unit.id.id : "C-" + combatEntity.id.id,
						actionData.key);
				}
				return false;
			}

			var (hasDependentAction, dependentActionKey) = TryGetActionDependency(dataCore);
			if (!partOK && hasDependentAction)
			{
				var dependentActionData = DataMultiLinkerAction.GetEntry(dependentActionKey, false);
				if (dependentActionData == null)
				{
					Debug.LogWarningFormat(
						"Mod {0} ({1}) IsAvailableAtTime(): dependent data key should exist | time: {2:F3}s | action: {3} | dependent data key: {4}",
						ModLink.modIndex,
						ModLink.modID,
						startTime,
						actionData.key,
						dependentActionKey);
					return false;
				}
				if (!PBDataHelperAction.IsAvailable(dependentActionData, unit))
				{
					if (LoggingToggles.AIBehaviorInvoke)
					{
						Debug.LogFormat(
							"Mod {0} ({1}) IsAvailableAtTime(): dependent action is not available at time | time: {2:F3}s | unit: {3} | action: {4} | dependent action: {5}",
							ModLink.modIndex,
							ModLink.modID,
							startTime,
							unit != null ? "P-" + unit.id.id : "C-" + combatEntity.id.id,
							actionData.key,
							dependentActionData.key);
					}
					return false;
				}
			}

			var duration = partOK && useEquipmentDuration
				? PBDataHelperStats.GetCachedStatForPart(UnitStats.activationDuration, part)
				: dataCore.duration;
			var endTime = startTime + duration;

			var (hasLockout, lockoutDuration) = DataHelperStats.TryGetActivationLockoutDuration(part, dataCore);

			var primaryTrack = dataCore.trackType == TrackType.Primary;
			var secondaryTrack = dataCore.trackType == TrackType.Secondary;

			var available = true;
			var ownedActions = Contexts.sharedInstance.action.GetEntitiesWithActionOwner(combatEntity.id.id);
			orderedActions.Clear();
			orderedActions.AddRange(ownedActions);
			orderedActions.Sort(ActionUtility.CompareByStartTime);
			foreach (var action in orderedActions)
			{
				if (primaryTrack && !action.isOnPrimaryTrack)
				{
					continue;
				}
				if (secondaryTrack && !action.isOnSecondaryTrack)
				{
					continue;
				}

				var samePart = hasLockout && ActionUtility.IsSamePart(action, part);
				var actionStartTime = action.startTime.f;
				var adjustedEndTime = endTime;
				if (samePart)
				{
					endTime += lockoutDuration;
				}
				if (adjustedEndTime < actionStartTime)
				{
					break;
				}

				var actionEndTime = actionStartTime + action.duration.f;
				if (samePart && action.hasHitPredictions && action.hitPredictions.hitPredictions.Count != 0)
				{
					actionEndTime += action.hitPredictions.hitPredictions[0].time;
				}
				if (actionEndTime < startTime)
				{
					continue;
				}

				if (LoggingToggles.AIBehaviorInvoke)
				{
					Debug.LogFormat(
						"Mod {0} ({1}) IsAvailableAtTime(): action is not available at time due to overlap with existing action | time: {2:F3}s | unit: {3} | action: {4} | existing action: {5}\n  action | start time: {6:F3}s | end time: {7:F3}s\n  existing | start time: {8:F3}s | end time: {9:F3}s",
						ModLink.modIndex,
						ModLink.modID,
						startTime,
						unit != null ? "P-" + unit.id.id : "C-" + combatEntity.id.id,
						actionData.key,
						action.dataKeyAction.s,
						startTime,
						endTime,
						actionStartTime,
						actionEndTime);
				}

				available = false;
				break;
			}

			orderedActions.Clear();
			return available;
		}

		public static (bool, string) TryGetActionDependency(DataBlockActionCore dataCore)
		{
			if (dataCore.functionsOnCreation == null || dataCore.functionsOnCreation.Count == 0)
			{
				return (false, "");
			}

			foreach (var func in dataCore.functionsOnCreation)
			{
				if (func is CombatActionCreateReaction react)
				{
					var key = react.dependencyActionKey;
					return (!string.IsNullOrWhiteSpace(key), key);
				}
			}

			return (false, "");
		}

		public static (bool, EquipmentEntity) TryGetEquipmentPart(
			int unitPersistentID,
			DataContainerAction actionData)
		{
			if (unitPersistentID == IDUtility.invalidID)
			{
				return (false, null);
			}

			var unit = IDUtility.GetPersistentEntity(unitPersistentID);
			return TryGetEquipmentPart(unit, actionData);
		}

		public static (bool, EquipmentEntity) TryGetEquipmentPart(
			PersistentEntity unit,
			DataContainerAction actionData)
		{
			if (unit == null)
			{
				return (false, null);
			}
			if (actionData == null)
			{
				return (false, null);
			}
			if (actionData.dataCore == null)
			{
				return (false, null);
			}
			if (actionData.dataCore.durationType == DurationType.Variable)
			{
				return (false, null);
			}
			if (actionData.dataEquipment == null)
			{
				return (false, null);
			}
			if (!actionData.dataEquipment.partUsed)
			{
				return (false, null);
			}

			var part = EquipmentUtility.GetPartInUnit(unit, actionData.dataEquipment.partSocket);
			return (part != null, part);
		}

		static readonly List<ActionEntity> orderedActions = new List<ActionEntity>();
	}
}
