// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;

using PhantomBrigade;
using PhantomBrigade.Data;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	public static class CombatUIUtility
	{
		[System.Flags]
		public enum ActionOverlapCheck
		{
			None = 0,
			PrimaryTrack = 1,
			SecondaryTrack = 2,
			BothTracks = PrimaryTrack | SecondaryTrack,
		}

		public static (float StartTime, float OverlapStart, float OverlapDuration)
			GetOverlap(
				PersistentEntity unit,
				DataContainerAction actionData,
				ActionEntity action,
				float startTime,
				float duration,
				float endTime)
		{
			var combat = Contexts.sharedInstance.combat;
			var turnStartTime = (float)combat.currentTurn.i * combat.turnLength.i;
			var actionStartTime = action.startTime.f;
			var actionDuration = action.duration.f;
			var actionEndTime = actionStartTime + actionDuration;
			var (ok, part) = DataHelperAction.TryGetEquipmentPart(unit, actionData);
			if (ok && ActionUtility.IsSamePart(action, part))
			{
				var (_, lockoutDuration) = DataHelperStats.TryGetActivationLockoutDuration(part, actionData.dataCore);
				duration += lockoutDuration;
				endTime += lockoutDuration;
				if (action.hasHitPredictions && action.hitPredictions.hitPredictions.Count != 0)
				{
					actionEndTime += action.hitPredictions.hitPredictions[0].time;
				}
			}

			if (actionStartTime > startTime)
			{
				if (actionStartTime - duration > turnStartTime)
				{
					return (actionStartTime - duration, actionStartTime, endTime - actionStartTime);
				}
				return (actionEndTime, actionStartTime, endTime - actionStartTime);
			}
			return (actionEndTime, startTime, Mathf.Min(actionEndTime, endTime) - startTime);
		}

		public static (bool, float) TryPlaceAction(
			int ownerID,
			DataContainerAction actionData,
			float startTime,
			float duration,
			ActionOverlapCheck overlapCheck)
		{
			if (overlapCheck == ActionOverlapCheck.None)
			{
				return CheckPlacementTime(startTime);
			}

			var primaryTrackOnly = overlapCheck == ActionOverlapCheck.PrimaryTrack;
			if (primaryTrackOnly && actionData.dataCore.trackType == TrackType.Secondary)
			{
				return CheckPlacementTime(startTime);
			}

			var secondaryTrackOnly = overlapCheck == ActionOverlapCheck.SecondaryTrack;
			if (secondaryTrackOnly && actionData.dataCore.trackType == TrackType.Primary)
			{
				return CheckPlacementTime(startTime);
			}

			var owner = IDUtility.GetCombatEntity(ownerID);
			if (owner == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) TryPlaceAction unable to resolve combat ID to entity | ID: C-{2}",
					ModLink.modIndex,
					ModLink.modID,
					ownerID);
				return (false, 0f);
			}

			var unit = IDUtility.GetLinkedPersistentEntity(owner);
			if (unit == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) TryPlaceAction unable to get persistent entity linked to combat ID | ID: C-{2}",
					ModLink.modIndex,
					ModLink.modID,
					ownerID);
				return (false, 0f);
			}

			var endTime = startTime + duration;
			var (partOK, part) = DataHelperAction.TryGetEquipmentPart(unit, actionData);
			var lockoutDuration = 0f;
			if (partOK)
			{
				var (ok, v) = DataHelperStats.TryGetActivationLockoutDuration(part, actionData.dataCore);
				if (ok)
				{
					lockoutDuration = v;
				}
			}
			var actions = new List<ActionEntity>(Contexts.sharedInstance.action.GetEntitiesWithActionOwner(ownerID));
			actions.Sort(ActionUtility.CompareByStartTime);
			foreach (var action in actions)
			{
				// Weed out single-track actions if we're not checking that track.
				if (primaryTrackOnly && !action.isOnPrimaryTrack)
				{
					continue;
				}
				if (secondaryTrackOnly && !action.isOnSecondaryTrack)
				{
					continue;
				}

				var et = endTime;
				var actionLockoutDuration = 0f;
				var samePart = false;
				if (action.hasHitPredictions && action.hitPredictions.hitPredictions.Count != 0)
				{
					var hp = action.hitPredictions.hitPredictions[0];
					if (part.id.id == hp.combatID)
					{
						et += lockoutDuration;
						actionLockoutDuration = hp.time;
						samePart = true;
					}
				}

				var actionStartTime = action.startTime.f;
				if (et < actionStartTime)
				{
					break;
				}

				var actionEndTime = actionStartTime + action.duration.f;
				if (samePart)
				{
					actionEndTime += actionLockoutDuration;
				}
				if (actionEndTime < startTime)
				{
					continue;
				}

				startTime = actionEndTime;
				endTime = startTime + duration;
			}

			return CheckPlacementTime(startTime);
		}

		public static (bool, float) TryPlaceActionWithNudge(
			int ownerID,
			DataContainerAction actionData,
			float startTime,
			float duration)
		{
			if (actionData.dataCore.trackType == TrackType.Primary)
			{
				return (false, 0f);
			}

			var owner = IDUtility.GetCombatEntity(ownerID);
			if (owner == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) TryPlaceActionWithNudge unable to resolve combat ID to entity | ID: C-{2}",
					ModLink.modIndex,
					ModLink.modID,
					ownerID);
				return (false, 0f);
			}

			var unit = IDUtility.GetLinkedPersistentEntity(owner);
			if (unit == null)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) TryPlaceActionWithNudge unable to get persistent entity linked to combat ID | ID: C-{2}",
					ModLink.modIndex,
					ModLink.modID,
					ownerID);
				return (false, 0f);
			}

			var (partOK, part) = DataHelperAction.TryGetEquipmentPart(unit, actionData);
			var lockoutDuration = 0f;
			if (partOK)
			{
				var (ok, v) = DataHelperStats.TryGetActivationLockoutDuration(part, actionData.dataCore);
				if (ok)
				{
					lockoutDuration = v;
				}
			}

			var secondaryOnly = actionData.dataCore.trackType == TrackType.Secondary;
			var endTime = startTime + duration;

			var combat = Contexts.sharedInstance.combat;
			var previousActionEndTime = (float)combat.currentTurn.i * combat.turnLength.i;
			var nudged = false;

			var actions = new List<ActionEntity>(Contexts.sharedInstance.action.GetEntitiesWithActionOwner(ownerID));
			actions.Sort(ActionUtility.CompareByStartTime);
			foreach (var action in actions)
			{
				if (secondaryOnly && !action.isOnSecondaryTrack)
				{
					continue;
				}

				var et = endTime;
				var actionLockoutDuration = 0f;
				var samePart = false;
				if (partOK
					&& action.hasHitPredictions
					&& action.hitPredictions.hitPredictions.Count != 0)
				{
					var hp = action.hitPredictions.hitPredictions[0];
					if (part.id.id == hp.combatID)
					{
						et += lockoutDuration;
						actionLockoutDuration = hp.time;
						samePart = true;
					}
				}

				var actionStartTime = action.startTime.f;
				if (et < actionStartTime)
				{
					// Proposed action ends before existing action.
					break;
				}

				var actionEndTime = actionStartTime + action.duration.f;
				if (samePart)
				{
					actionEndTime += actionLockoutDuration;
				}
				if (actionEndTime < startTime)
				{
					// Proposed action starts after existing action.
					previousActionEndTime = Mathf.Max(previousActionEndTime, actionEndTime);
					continue;
				}

				if (startTime < actionStartTime)
				{
					// Proposed action overlaps beginning of existing action, try nudging proposed action earlier in timeline.
					startTime = actionStartTime - duration;
					if (samePart)
					{
						startTime -= lockoutDuration;
					}
					if (startTime < previousActionEndTime)
					{
						// Nudge earlier still causes an overlap so can't place proposed action.
						return (false, 0f);
					}
					break;
				}

				if (nudged)
				{
					// Still have an overlap after nudge so can't place proposed action.
					return (false, 0);
				}

				// Proposed action overlaps end of existing action, try nudging proposed action later in timeline.
				previousActionEndTime = Mathf.Max(previousActionEndTime, actionEndTime);
				startTime = previousActionEndTime;
				endTime = startTime + duration;
				nudged = true;
			}

			return CheckPlacementTime(startTime);
		}

		static (bool, float) CheckPlacementTime(float startTime)
		{
			var combat = Contexts.sharedInstance.combat;
			var minPlacementTime = combat.currentTurn.i * combat.turnLength.i;
			var maxPlacementTime = minPlacementTime + DataShortcuts.sim.maxActionTimePlacement;
			if (startTime < minPlacementTime)
			{
				return (false, 0f);
			}
			if (startTime > maxPlacementTime)
			{
				return (false, 0f);
			}
			return (true, startTime);
		}
	}
}
