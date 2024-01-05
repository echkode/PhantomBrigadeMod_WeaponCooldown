// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;

using PhantomBrigade;
using PhantomBrigade.Action.Components;
using PhantomBrigade.Data;

namespace EchKode.PBMods.WeaponCooldown
{
	public static class ActionUtility
	{
		public static float AdjustEndTime(ActionEntity action, float endTime)
		{
			// Borrowing the hitPredictions component on ActionEntity to stash the activation lockout duration.
			if (!action.hasHitPredictions || action.hitPredictions.hitPredictions.Count == 0)
			{
				return endTime;
			}

			return endTime + action.hitPredictions.hitPredictions[0].time;
		}

		public static float AdjustEndTime(ActionEntity action, EquipmentEntity part, float endTime)
		{
			if (!action.hasHitPredictions)
			{
				return endTime;
			}
			if (action.hitPredictions.hitPredictions.Count == 0)
			{
				return endTime;
			}

			var hitPrediction = action.hitPredictions.hitPredictions[0];
			if (hitPrediction.combatID != part.id.id)
			{
				// We're repurposing combatID to mean equipmentID and point to the part with the
				// activation lockout.
				//
				// An action with lockout will only block future actions from the same part for the
				// duration of the lockout.
				return endTime;
			}

			return endTime + hitPrediction.time;
		}

		public static (bool, EquipmentEntity, float) TryAdjustEndTime(
			CombatEntity owner,
			DataContainerAction actionData,
			float endTime)
		{
			var unit = IDUtility.GetLinkedPersistentEntity(owner);
			return TryAdjustEndTime(unit, actionData, endTime);
		}

		public static (bool, EquipmentEntity, float) TryAdjustEndTime(
			PersistentEntity unit,
			DataContainerAction actionData,
			float endTime)
		{
			var (okPart, part) = DataHelperAction.TryGetEquipmentPart(unit, actionData);
			if (!okPart)
			{
				return (false, null, endTime);
			}

			var (ok, lockoutDuration) = DataHelperStats.TryGetActivationLockoutDuration(part, actionData.dataCore);
			if (!ok)
			{
				return (false, part, endTime);
			}

			return (true, part, endTime + lockoutDuration);
		}

		public static void DestroyActionsFromTime(
			CombatEntity actionOwner,
			float currentTime,
			bool primaryOnly,
			bool removeLocked)
		{
			if (actionOwner == null)
			{
				return;
			}

			var removeAll = actionOwner.isConcussed;
			if (!removeAll)
			{
				var unit = IDUtility.GetLinkedPersistentEntity(actionOwner);
				removeAll = unit == null || unit.isWrecked;
				if (!removeAll)
				{
					var linkedPilot = IDUtility.GetLinkedPilot(unit);
					removeAll = linkedPilot == null || linkedPilot.isEjected || linkedPilot.isKnockedOut;
				}
			}

			foreach (var action in Contexts.sharedInstance.action.GetEntitiesWithActionOwner(actionOwner.id.id))
			{
				if (removeAll)
				{
					action.isDisposed = true;
					continue;
				}

				if (primaryOnly && !action.isOnPrimaryTrack)
				{
					continue;
				}
				if (!removeLocked && action.isLocked)
				{
					continue;
				}

				if (action.hasHitPredictions
					&& action.hitPredictions.hitPredictions.Count != 0
					&& !action.hitPredictions.hitPredictions[0].time.RoughlyEqual(0f))
				{
					// Crashing doesn't cancel activation lockout. The lockout represents a property
					// of the weapon, not the unit, so it continues while the unit is crashed.
					continue;
				}

				var endTime = action.startTime.f + action.duration.f;
				if (endTime < currentTime)
				{
					continue;
				}

				action.isDisposed = true;
			}
		}

		public static void DisposeLockoutActionsPendingCrash(CombatEntity actionOwner)
		{
			if (actionOwner == null)
			{
				return;
			}

			var crashEndTime = 0f;
			cachedActions.Clear();
			foreach (var action in Contexts.sharedInstance.action.GetEntitiesWithActionOwner(actionOwner.id.id))
			{
				if (!action.hasDataKeyAction)
				{
					continue;
				}
				if (action.dataKeyAction.s == ActionKeys.crash)
				{
					crashEndTime = action.startTime.f + action.duration.f;
					continue;
				}
				if (!action.hasHitPredictions)
				{
					continue;
				}
				if (action.hitPredictions.hitPredictions.Count == 0)
				{
					continue;
				}

				cachedActions.Add(action);
			}

			foreach (var action in cachedActions)
			{
				var endTime = action.startTime.f + action.duration.f + action.hitPredictions.hitPredictions[0].time;
				if (endTime < crashEndTime)
				{
					// Lockout expires in crash so remove action.
					action.isDisposed = true;
					continue;
				}

				if (!action.hasActiveEquipmentPart)
				{
					continue;
				}

				var part = IDUtility.GetEquipmentEntity(action.activeEquipmentPart.equipmentID);
				if (part == null)
				{
					continue;
				}

				if (part.isWrecked)
				{
					action.isDisposed = true;
				}
			}

			cachedActions.Clear();
		}

		public static float GetLastSecondaryTrackAction(CombatEntity selectedEntity)
		{
			var ownedActions = Contexts.sharedInstance.action.GetEntitiesWithActionOwner(selectedEntity.id.id);
			if (ownedActions.Count == 0)
			{
				return IDUtility.invalidID;
			}

			cachedActions.Clear();
			cachedActions.AddRange(ownedActions);
			cachedActions.Sort(CompareByStartTime);
			for (var i = cachedActions.Count - 1; i >= 0; i -= 1)
			{
				var action = cachedActions[i];
				if (!action.isOnSecondaryTrack)
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
				return action.id.id;
			}

			return IDUtility.invalidID;
		}

		public static bool IsSamePart(ActionEntity action, EquipmentEntity part)
		{
			if (action == null || part == null)
			{
				return false;
			}
			if (!action.hasActiveEquipmentPart)
			{
				return false;
			}
			return action.activeEquipmentPart.equipmentID == part.id.id;
		}

		public static void StoreActivationLockout(ActionEntity action, EquipmentEntity part)
		{
			var (ok, duration) = DataHelperStats.TryGetActivationLockoutDuration(part, action.dataLinkActionCore.data);
			if (!ok)
			{
				return;
			}
			var predictions = new List<ActionHitPrediction>()
			{
				new ActionHitPrediction()
				{
					combatID = part.id.id,
					time = duration,
				},
			};
			action.ReplaceHitPredictions(predictions);
		}

		public static int CompareByStartTime(ActionEntity a, ActionEntity b) => a.startTime.f.CompareTo(b.startTime.f);

		static readonly List<ActionEntity> cachedActions = new List<ActionEntity>();
	}
}
