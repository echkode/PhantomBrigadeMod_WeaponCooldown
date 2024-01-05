using System.Collections.Generic;

using PhantomBrigade;
using PhantomBrigade.Data;
using PBDataHelperAction = PhantomBrigade.Data.DataHelperAction;
using PBDataHelperStats = PhantomBrigade.Data.DataHelperStats;

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
				return false;
			}

			var dataCore = actionData.dataCore;
			if (dataCore.durationType == DurationType.Variable)
			{
				// !!! Assumes that this function is called for fixed duration actions.
				return false;
			}

			var dependentData = actionData.dataDependency;
			var hasDependentAction = dependentData != null && !string.IsNullOrEmpty(dependentData.key);
			var (partOK, part) = TryGetEquipmentPart(unit, actionData);
			if (!partOK && !hasDependentAction)
			{
				// !!! Assumes that this function is called for equipment use actions.
				return false;
			}
			else if (hasDependentAction)
			{
				var dependentActionData = DataMultiLinkerAction.GetEntry(dependentData.key, false);
				if (dependentActionData == null)
				{
					return false;
				}
				if (!PBDataHelperAction.IsAvailable(dependentActionData, unit))
				{
					return false;
				}
			}

			var useEquipmentDuration = partOK
				&& dataCore.durationType == DurationType.Equipment
				&& actionData.dataEquipment != null
				&& actionData.dataEquipment.partUsed;
			var duration = useEquipmentDuration
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

				available = false;
				break;
			}

			orderedActions.Clear();
			return available;
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
