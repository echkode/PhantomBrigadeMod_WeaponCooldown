// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using PhantomBrigade;
using PhantomBrigade.Data;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	public static partial class CIViewCombatTimeline
	{
		public static void RefreshTimelineRegions(int actionIDSelected)
		{
			timelineRegions.Clear();
			timelineRegionsModified.Clear();
			CombatEntity selectedCombatEntity = IDUtility.GetSelectedCombatEntity();
			if (selectedCombatEntity == null)
			{
				return;
			}

			var combat = Contexts.sharedInstance.combat;
			var turnStartTime = combat.turnLength.i * combat.currentTurn.i;
			foreach (var actionEntity in Contexts.sharedInstance.action.GetEntitiesWithActionOwner(selectedCombatEntity.id.id))
			{
				if (actionEntity.isDisposed)
				{
					continue;
				}
				if (actionEntity.isDestroyed)
				{
					continue;
				}

				var actionStartTime = actionEntity.startTime.f;
				var actionDuration = actionEntity.duration.f;
				var actionEndTime = actionStartTime + actionDuration;
				var partID = IDUtility.invalidID;
				var lockoutDuration = 0f;
				if (actionEntity.hasHitPredictions && actionEntity.hitPredictions.hitPredictions.Count != 0)
				{
					var hp = actionEntity.hitPredictions.hitPredictions[0];
					partID = hp.combatID;
					lockoutDuration = hp.time;
					actionEndTime += lockoutDuration;
				}
				if (actionDuration > 0f && actionEndTime >= turnStartTime)
				{
					var timelineRegion = new TimelineRegion2()
					{
						startTime = actionStartTime,
						duration = actionDuration,
						offset = 0f,
						actionID = actionEntity.id.id,
						actionKey = actionEntity.dataKeyAction.s,
						wait = actionEntity.dataLinkAction.data.dataCore.paintingType == PaintingType.Wait,
						changed = false,
						primary = actionEntity.isOnPrimaryTrack,
						secondary = actionEntity.isOnSecondaryTrack,
						locked = actionEntity.isLocked
							|| (actionEntity.isStarted && actionStartTime < turnStartTime),
						partID = partID,
						activationLockoutDuration = lockoutDuration,
					};
					timelineRegions.Add(timelineRegion);
					if (actionIDSelected == actionEntity.id.id)
					{
						timelineRegionSelected = timelineRegion;
					}
				}
			}
			timelineRegions.Sort(CompareByStartTime);
			timelineRegionsModified.AddRange(timelineRegions);
			ReportSameTrackOverlaps();
		}

		static void ReportSameTrackOverlaps()
		{
			// Actions on the same track should not overlap. If they do, that's going to cause a lot of problems.

			for (var i = 1; i < timelineRegionsModified.Count; i += 1)
			{
				var priorRegion = timelineRegionsModified[i - 1];
				var region = timelineRegionsModified[i];

				var primaryTrack = priorRegion.primary && region.primary;
				var secondaryTrack = priorRegion.secondary && region.secondary;
				var sameTrack = primaryTrack || secondaryTrack;
				var overlap = priorRegion.startTime.RoughlyEqual(region.startTime, timeThreshold);
				overlap = overlap || region.startTime < priorRegion.endTime - timeThreshold;
				if (sameTrack && overlap)
				{
					Debug.LogWarningFormat(
						"Mod {0} ({1}) RefreshTimelineRegions overlapping regions on same track | track: {2} | action: A-{3} ({4}) / {5:F3}s - {6:F3}s | action: A-{7} ({8}) / {9:F3}s - {10:F3}s",
						ModLink.modIndex,
						ModLink.modID,
						primaryTrack ? "primary" : "secondary",
						priorRegion.actionID,
						priorRegion.actionKey,
						priorRegion.startTime,
						priorRegion.endTime,
						region.actionID,
						region.actionKey,
						region.startTime,
						region.endTime);
				}
			}
		}
	}
}
