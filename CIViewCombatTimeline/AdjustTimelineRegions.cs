// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.Data;
using PBCIViewCombatTimeline = CIViewCombatTimeline;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	public static partial class CIViewCombatTimeline
	{
		public static void AdjustTimelineRegions(
			PBCIViewCombatTimeline inst,
			int actionIDSelected,
			float actionStartTime,
			float offsetSelected)
		{
			var t = Traverse.Create(inst);
			if (!t.Field<bool>("timelineRegionDragged").Value)
			{
				return;
			}
			if (offsetSelected == 0f)
			{
				return;
			}

			var selectedIndex = ClearRegionChanges(actionIDSelected);
			if (selectedIndex == -1)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) AdjustTimelineRegions cannot find matching region for action | action: A-{2} | start time: {3:F3}s | offsetSelected: {4:F3}s",
					ModLink.modIndex,
					ModLink.modID,
					actionIDSelected,
					actionStartTime,
					offsetSelected);
				return;
			}

			if (offsetSelected < 0f)
			{
				AdjustEarlierRegions(
					actionStartTime,
					offsetSelected,
					selectedIndex);
			}
			else
			{
				AdjustLaterRegions(
					actionStartTime,
					offsetSelected,
					selectedIndex);
			}

			RepositionActionButtons(inst, t);
		}

		static int ClearRegionChanges(int actionIDSelected)
		{
			var selectedRegionIndex = -1;
			timelineRegions.Clear();
			if (timelineRegionsModified.Count == 0)
			{
				return selectedRegionIndex;
			}

			timelineRegionsModified.Sort(CompareByStartTime);
			timelineRegions.AddRange(timelineRegionsModified);

			for (var i = 0; i < timelineRegions.Count; i += 1)
			{
				var region = timelineRegions[i];
				region.changed = false;
				region.offset = 0f;
				timelineRegions[i] = region;
				timelineRegionsModified[i] = region;
				if (actionIDSelected == region.actionID)
				{
					selectedRegionIndex = i;
					timelineRegionSelected = region;
				}
			}

			return selectedRegionIndex;
		}

		static void AdjustEarlierRegions(
			float actionStartTime,
			float offsetSelected,
			int selectedIndex)
		{
			var selectedRegion = AdjustSelectedRegion(
				actionStartTime,
				offsetSelected,
				selectedIndex,
				GetMinStartTimes,
				Mathf.Max,
				AdjustEarlierStartTime,
				CorrectEarlierOffset);
			if (!selectedRegion.changed)
			{
				return;
			}
			if (selectedRegion.locked)
			{
				return;
			}

			var primaryTrackIndex = selectedRegion.primary ? selectedIndex : -1;
			var secondaryTrackIndex = selectedRegion.secondary ? selectedIndex : -1;

			for (var i = selectedIndex - 1; i >= 0; i -= 1)
			{
				var region = timelineRegionsModified[i];
				if (region.locked)
				{
					break;
				}

				if (region.doubleTrack)
				{
					var (stop, pidx, sidx) = PushRegionEarlier(
						region,
						i,
						primaryTrackIndex,
						secondaryTrackIndex);
					if (stop)
					{
						break;
					}
					primaryTrackIndex = pidx;
					secondaryTrackIndex = sidx;
				}
				else if (region.primary && primaryTrackIndex != -1)
				{
					primaryTrackIndex = PushRegionEarlier(
						region,
						i,
						primaryTrackIndex);
				}
				else if (region.secondary && secondaryTrackIndex != -1)
				{
					secondaryTrackIndex = PushRegionEarlier(
						region,
						i,
						secondaryTrackIndex);
				}

			}
		}

		static (bool Stop, int PrimaryTrackIndex, int SecondaryTrackIndex)
			PushRegionEarlier(
				TimelineRegion2 region,
				int index,
				int primaryTrackIndex,
				int secondaryTrackIndex)
		{
			var combat = Contexts.sharedInstance.combat;
			var turnStartTime = (float)combat.currentTurn.i * combat.turnLength.i;
			var primaryStartTime = primaryTrackIndex != -1
				? timelineRegionsModified[primaryTrackIndex].startTime
				: float.MaxValue;
			var secondaryStartTime = secondaryTrackIndex != -1
				? timelineRegionsModified[secondaryTrackIndex].startTime
				: float.MaxValue;
			var startTime = Mathf.Min(primaryStartTime, secondaryStartTime);
			var endTime = region.endTime;

			if (region.partID != IDUtility.invalidID)
			{
				var adjustedEndTime = endTime + region.activationLockoutDuration;
				for (var i = lockoutRegions.Count - 1; i >= 0; i -= 1)
				{
					var (lockoutIndex, lockoutPartID) = lockoutRegions[i];
					if (lockoutIndex <= index)
					{
						break;
					}
					if (region.partID != lockoutPartID)
					{
						continue;
					}
					var lockoutRegion = timelineRegionsModified[lockoutIndex];

					if (lockoutRegion.startTime < adjustedEndTime)
					{
						startTime = lockoutRegion.startTime;
						endTime = adjustedEndTime;
					}
				}
			}

			var offset = startTime - endTime;
			if (offset > 0f || offset.RoughlyEqual(0f, timeThreshold))
			{
				return (true, primaryTrackIndex, secondaryTrackIndex);
			}

			startTime = region.startTime + offset;
			if (startTime < turnStartTime)
			{
				offset += turnStartTime - startTime;
				startTime = turnStartTime;
			}
			if (offset > 0f || offset.RoughlyEqual(0f, timeThreshold))
			{
				return (true, primaryTrackIndex, secondaryTrackIndex);
			}

			region.startTime = startTime;
			region.offset = offset;
			region.changed = true;
			timelineRegionsModified[index] = region;

			return (false, index, index);
		}

		static int PushRegionEarlier(
			TimelineRegion2 region,
			int index,
			int trackIndex)
		{
			var adjustedRegion = timelineRegionsModified[trackIndex];
			var startTime = adjustedRegion.startTime;
			var endTime = region.endTime;

			if (region.partID != IDUtility.invalidID)
			{
				var adjustedEndTime = endTime + region.activationLockoutDuration;
				for (var i = lockoutRegions.Count - 1; i >= 0; i -= 1)
				{
					var (lockoutIndex, lockoutPartID) = lockoutRegions[i];
					if (lockoutIndex <= index)
					{
						break;
					}
					if (region.partID != lockoutPartID)
					{
						continue;
					}

					var lockoutRegion = timelineRegionsModified[lockoutIndex];
					if (region.primary && !lockoutRegion.primary)
					{
						continue;
					}
					else if (region.secondary && !lockoutRegion.secondary)
					{
						continue;
					}

					if (lockoutRegion.startTime < adjustedEndTime)
					{
						startTime = lockoutRegion.startTime;
						endTime = adjustedEndTime;
					}
				}
			}

			var offset = startTime - endTime;
			if (offset > 0f || offset.RoughlyEqual(0f, timeThreshold))
			{
				return trackIndex;
			}

			startTime = region.startTime + offset;
			var combat = Contexts.sharedInstance.combat;
			var turnStartTime = (float)combat.currentTurn.i * combat.turnLength.i;
			if (startTime < turnStartTime)
			{
				offset += turnStartTime - startTime;
				startTime = turnStartTime;
			}
			if (offset > 0f || offset.RoughlyEqual(0f, timeThreshold))
			{
				return trackIndex;
			}

			region.startTime = startTime;
			region.offset = offset;
			region.changed = true;
			timelineRegionsModified[index] = region;

			return index;
		}

		static void AdjustLaterRegions(
			float actionStartTime,
			float offsetSelected,
			int selectedIndex)
		{
			var selectedRegion = AdjustSelectedRegion(
				actionStartTime,
				offsetSelected,
				selectedIndex,
				GetMaxStartTimes,
				Mathf.Min,
				AdjustLaterStartTime,
				CorrectLaterOffset);
			if (!selectedRegion.changed)
			{
				return;
			}
			if (selectedRegion.locked)
			{
				return;
			}

			var primaryTrackIndex = selectedRegion.primary ? selectedIndex : -1;
			var secondaryTrackIndex = selectedRegion.secondary ? selectedIndex : -1;

			for (var i = selectedIndex + 1; i < timelineRegionsModified.Count; i += 1)
			{
				var region = timelineRegionsModified[i];
				if (region.locked)
				{
					break;
				}

				if (region.doubleTrack)
				{
					var (stop, pidx, sidx) = PushRegionLater(
						region,
						i,
						primaryTrackIndex,
						secondaryTrackIndex);
					if (stop)
					{
						break;
					}
					primaryTrackIndex = pidx;
					secondaryTrackIndex = sidx;
				}
				else if (region.primary && primaryTrackIndex != -1)
				{
					primaryTrackIndex = PushRegionLater(
						region,
						i,
						primaryTrackIndex);
				}
				else if (region.secondary && secondaryTrackIndex != -1)
				{
					secondaryTrackIndex = PushRegionLater(
						region,
						i,
						secondaryTrackIndex);
				}
			}
		}

		static (bool Stop, int PrimaryTrackIndex, int SecondaryTrackIndex) PushRegionLater(
			TimelineRegion2 region,
			int index,
			int primaryTrackIndex,
			int secondaryTrackIndex)
		{
			var primaryEndTime = primaryTrackIndex != -1
				? timelineRegionsModified[primaryTrackIndex].endTime
				: float.MinValue;
			var secondaryEndTime = secondaryTrackIndex != -1
				? timelineRegionsModified[secondaryTrackIndex].endTime
				: float.MinValue;
			var endTime = Mathf.Max(primaryEndTime, secondaryEndTime);

			for (var i = 0; i < lockoutRegions.Count; i += 1)
			{
				var (lockoutIndex, lockoutPartID) = lockoutRegions[i];
				if (lockoutIndex >= index)
				{
					break;
				}
				if (region.partID != lockoutPartID)
				{
					continue;
				}

				var lockoutRegion = timelineRegionsModified[lockoutIndex];
				if (lockoutRegion.lockoutEndTime > region.startTime)
				{
					endTime = lockoutRegion.lockoutEndTime;

					if (LoggingToggles.ActionDrag)
					{
						Debug.LogFormat(
							"Mod {0} ({1}) PushRegionLater() using prior lockout | action ID: {2} ({3}) | track: {4} | action start time: {5:F3}s | lockout end time: {6:F3}s",
							ModLink.modIndex,
							ModLink.modID,
							lockoutRegion.actionID,
							lockoutRegion.actionKey,
							lockoutRegion.doubleTrack
								? "double"
								: lockoutRegion.primary
									? "primary"
									: "secondary",
							lockoutRegion.startTime,
							lockoutRegion.lockoutEndTime);
					}
				}
			}

			var offset = endTime - region.startTime;
			if (offset < 0f || offset.RoughlyEqual(0f, timeThreshold))
			{
				return (true, primaryTrackIndex, secondaryTrackIndex);
			}

			if (LoggingToggles.ActionDrag)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) PushRegionLater() | action ID: {2} ({3}) | track: {4} | offset: {5:F3}s | action start time: {6:F3}s --> {7:F3}s",
					ModLink.modIndex,
					ModLink.modID,
					region.actionID,
					region.actionKey,
					region.doubleTrack
						? "double"
						: region.primary
							? "primary"
							: "secondary",
					offset,
					region.startTime,
					region.startTime + offset);
			}

			region.startTime += offset;
			region.offset = offset;
			region.changed = true;
			timelineRegionsModified[index] = region;

			return (false, index, index);
		}

		static int PushRegionLater(
			TimelineRegion2 region,
			int index,
			int trackIndex)
		{
			var adjustedRegion = timelineRegionsModified[trackIndex];
			var endTime = adjustedRegion.endTime;
			for (var i = 0; i < lockoutRegions.Count; i += 1)
			{
				var (lockoutIndex, lockoutPartID) = lockoutRegions[i];
				if (lockoutIndex >= index)
				{
					break;
				}

				if (region.partID != lockoutPartID)
				{
					continue;
				}

				var lockoutRegion = timelineRegionsModified[lockoutIndex];
				if (region.primary && !lockoutRegion.primary)
				{
					continue;
				}
				else if (region.secondary && !lockoutRegion.secondary)
				{
					continue;
				}

				if (lockoutRegion.lockoutEndTime > region.startTime)
				{
					endTime = lockoutRegion.lockoutEndTime;
				}
			}
			var offset = endTime - region.startTime;
			if (offset < 0f || offset.RoughlyEqual(0f, timeThreshold))
			{
				return trackIndex;
			}

			region.startTime += offset;
			region.offset = offset;
			region.changed = true;
			timelineRegionsModified[index] = region;

			return index;
		}

		static TimelineRegion2 AdjustSelectedRegion(
			float actionStartTime,
			float offsetSelected,
			int selectedIndex,
			System.Func<int, System.ValueTuple<float, float>> getTimes,
			System.Func<float, float, float> minmax,
			System.Func<int, float, float> adjustStartTime,
			System.Func<float, float, float, float> correctOffset)
		{
			if (timelineRegionSelected.locked)
			{
				return timelineRegionSelected;
			}

			lockoutRegions.Clear();
			lockoutPartLookup.Clear();

			var (primaryTime, secondaryTime) = getTimes(selectedIndex);
			var startTime = actionStartTime;
			var selectedRegion = timelineRegionSelected;

			if (selectedRegion.doubleTrack)
			{
				var minmaxTime = minmax(primaryTime, secondaryTime);
				startTime = minmax(minmaxTime, startTime);
			}
			else if (selectedRegion.primary)
			{
				startTime = minmax(primaryTime, startTime);
			}
			else if (selectedRegion.secondary)
			{
				startTime = minmax(secondaryTime, startTime);
			}
			else
			{
				return selectedRegion;
			}

			startTime = adjustStartTime(selectedIndex, startTime);

			var offset = correctOffset(startTime, actionStartTime, offsetSelected);
			if (offset.RoughlyEqual(0f, timeThreshold))
			{
				return selectedRegion;
			}

			selectedRegion.startTime = startTime;
			selectedRegion.offset = offset;
			selectedRegion.changed = true;

			timelineRegionsModified[selectedIndex] = selectedRegion;
			timelineRegionSelected = selectedRegion;

			if (LoggingToggles.ActionDrag)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) AdjustSelectedRegion() | action ID: {2} ({3}) | track: {4} | action start time: {5:F3}s | duration: {6:F3}s | part ID: {7} | lockout duration: {8:F3}s",
					ModLink.modIndex,
					ModLink.modID,
					timelineRegionSelected.actionID,
					timelineRegionSelected.actionKey,
					timelineRegionSelected.doubleTrack
						? "double"
						: timelineRegionSelected.primary
							? "primary"
							: "secondary",
					timelineRegionSelected.startTime,
					timelineRegionSelected.duration,
					timelineRegionSelected.partID,
					timelineRegionSelected.activationLockoutDuration);
			}

			return selectedRegion;
		}

		static (float, float) GetMinStartTimes(int selectedIndex)
		{
			var combat = Contexts.sharedInstance.combat;
			var minPrimaryTime = (float)combat.currentTurn.i * combat.turnLength.i;
			var minSecondaryTime = minPrimaryTime;
			for (var i = 0; i < selectedIndex; i += 1)
			{
				var region = timelineRegionsModified[i];

				if (region.doubleTrack)
				{
					var minTime = region.locked
						? region.startTime
						: Mathf.Max(minPrimaryTime, minSecondaryTime);
					var duration = region.duration;
					if (region.partID != IDUtility.invalidID)
					{
						if (!region.locked
							&& lockoutPartLookup.TryGetValue(region.partID, out var lockout)
							&& lockout.Time > minTime)
						{
							minTime = lockout.Time;
						}
						lockoutRegions.Add((i, region.partID));
						var endTime = minTime + duration + region.activationLockoutDuration;
						lockoutPartLookup[region.partID] = (i, endTime);
					}

					minTime += duration;
					minPrimaryTime = minTime;
					minSecondaryTime = minTime;

					continue;
				}

				if (region.primary)
				{
					if (region.locked)
					{
						minPrimaryTime = region.startTime;
					}

					var duration = region.duration;
					if (region.partID != IDUtility.invalidID)
					{
						if (!region.locked
							&& lockoutPartLookup.TryGetValue(region.partID, out var lockout)
							&& timelineRegionsModified[lockout.Index].primary
							&& lockout.Time > minPrimaryTime)
						{
							minPrimaryTime = lockout.Time;
						}
						lockoutRegions.Add((i, region.partID));
						var endTime = minPrimaryTime + duration + region.activationLockoutDuration;
						lockoutPartLookup[region.partID] = (i, endTime);
					}
					minPrimaryTime += duration;
				}

				if (region.secondary)
				{
					if (region.locked)
					{
						minSecondaryTime = region.startTime;
					}

					var duration = region.duration;
					if (region.partID != IDUtility.invalidID)
					{
						if (!region.locked
							&&lockoutPartLookup.TryGetValue(region.partID, out var lockout)
							&& timelineRegionsModified[lockout.Index].secondary
							&& lockout.Time > minSecondaryTime)
						{
							minSecondaryTime = lockout.Time;
						}
						lockoutRegions.Add((i, region.partID));
						var endTime = minSecondaryTime + duration + region.activationLockoutDuration;
						lockoutPartLookup[region.partID] = (i, endTime);
					}
					minSecondaryTime += duration;
				}
			}

			return (minPrimaryTime, minSecondaryTime);
		}

		static (float, float) GetMaxStartTimes(int selectedIndex)
		{
			var combat = Contexts.sharedInstance.combat;
			var maxPlacementTime = (float)combat.currentTurn.i * combat.turnLength.i + DataShortcuts.sim.maxActionTimePlacement;
			var maxPrimaryTime = maxPlacementTime;
			var maxSecondaryTime = maxPlacementTime;
			var lastPrimaryDuration = 0f;
			var lastSecondaryDuration = 0f;
			for (var i = timelineRegionsModified.Count - 1; i >= selectedIndex; i -= 1)
			{
				var region = timelineRegionsModified[i];
				if (region.primary && lastPrimaryDuration == 0f)
				{
					lastPrimaryDuration = region.duration;
					if (region.startTime > maxPlacementTime)
					{
						Debug.LogWarningFormat(
							"Mod {0} ({1}) AdjustTimelineRegions last action after max time placement | action ID: {2} ({3}) | action start time: {4:F3}s | track: primary | max placement: {5:F3}s",
							ModLink.modIndex,
							ModLink.modID,
							region.actionID,
							region.actionKey,
							region.startTime,
							maxPlacementTime);
						maxPrimaryTime = region.startTime;
					}
				}
				if (region.secondary && lastSecondaryDuration == 0f)
				{
					lastSecondaryDuration = region.duration;
					if (region.startTime > maxPlacementTime)
					{
						Debug.LogWarningFormat(
							"Mod {0} ({1}) AdjustTimelineRegions last action after max time placement | action ID: {2} ({3}) | action start time: {4:F3}s | track: secondary | max placement: {5:F3}s",
							ModLink.modIndex,
							ModLink.modID,
							region.actionID,
							region.actionKey,
							region.startTime,
							maxPlacementTime);
						maxSecondaryTime = region.startTime;
					}
				}
				if (lastPrimaryDuration != 0f && lastSecondaryDuration != 0f)
				{
					break;
				}
			}
			maxPrimaryTime += lastPrimaryDuration;
			maxSecondaryTime += lastSecondaryDuration;

			var sb = LoggingToggles.ActionDrag
				? new System.Text.StringBuilder()
				: null;
			if (LoggingToggles.ActionDrag)
			{
				sb.AppendFormat("GetMaxStartTimes | regions modified: {0}", timelineRegionsModified.Count);
				sb.AppendFormat("\n  starting times | primary: {0:F3}s | secondary: {1:F3}s", maxPrimaryTime, maxSecondaryTime);
			}

			for (var i = timelineRegionsModified.Count - 1; i > selectedIndex; i -= 1)
			{
				var region = timelineRegionsModified[i];

				if (region.doubleTrack)
				{
					var maxTime = region.startTime;

					if (LoggingToggles.ActionDrag)
					{
						sb.AppendFormat(
							"\n  {0} double | locked: {1} | end time: {2:F3}s | region start time: {3:F3}s",
							i,
							region.locked,
							Mathf.Min(maxPrimaryTime, maxSecondaryTime),
							maxTime);
					}

					if (!region.locked)
					{
						var duration = region.duration;
						maxTime = Mathf.Min(maxPrimaryTime, maxSecondaryTime);

						if (LoggingToggles.ActionDrag)
						{
							sb.AppendFormat(" | duration: {0:F3}s", duration);
						}

						if (region.partID != IDUtility.invalidID)
						{
							if (lockoutPartLookup.TryGetValue(region.partID, out var lockout)
								&& maxTime + duration + region.activationLockoutDuration >= lockout.Time)
							{
								duration += region.activationLockoutDuration;
								if (LoggingToggles.ActionDrag)
								{
									sb.AppendFormat(" | lockout duration: {0:F3}s", region.activationLockoutDuration);
								}
							}
							lockoutPartLookup[region.partID] = (i, maxTime - duration);
							lockoutRegions.Add((i, region.partID));

							if (LoggingToggles.ActionDrag)
							{
								sb.AppendFormat(" | part ID: {0}", region.partID);
							}
						}
						maxTime -= duration;
					}

					if (LoggingToggles.ActionDrag)
					{
						sb.AppendFormat(" | max start time: {0:F3}s", maxTime);
					}

					maxPrimaryTime = maxTime;
					maxSecondaryTime = maxTime;
					continue;
				}

				if (region.primary)
				{
					if (region.locked)
					{
						maxPrimaryTime = region.startTime;
						if (LoggingToggles.ActionDrag)
						{
							sb.AppendFormat(
								"\n  {0} primary locked | region start time: {1:F3}s | max start time: {2:F3}s",
								i,
								region.startTime,
								maxPrimaryTime);
						}
					}
					else
					{
						var duration = region.duration;

						if (LoggingToggles.ActionDrag)
						{
							sb.AppendFormat(
								"\n  {0} primary | region start time: {1:F3}s | end time: {1:F3}s | duration: {2:F3}s",
								i,
								region.startTime,
								maxPrimaryTime,
								duration);
						}

						if (region.partID != IDUtility.invalidID)
						{
							if (lockoutPartLookup.TryGetValue(region.partID, out var lockout)
								&& timelineRegionsModified[lockout.Index].primary
								&& maxPrimaryTime + duration + region.activationLockoutDuration >= lockout.Time)
							{
								duration += region.activationLockoutDuration;
								if (LoggingToggles.ActionDrag)
								{
									sb.AppendFormat(" | lockout duration: {0:F3}s", region.activationLockoutDuration);
								}
							}
							lockoutPartLookup[region.partID] = (i, maxPrimaryTime - duration);
							lockoutRegions.Add((i, region.partID));

							if (LoggingToggles.ActionDrag)
							{
								sb.AppendFormat(" | part ID: {0}", region.partID);
							}
						}
						maxPrimaryTime -= duration;

						if (LoggingToggles.ActionDrag)
						{
							sb.AppendFormat(" | max start time: {0:F3}s", maxPrimaryTime);
						}
					}
				}

				if (region.secondary)
				{
					if (region.locked)
					{
						maxSecondaryTime = region.startTime;
						if (LoggingToggles.ActionDrag)
						{
							sb.AppendFormat(
								"\n  {0} secondary locked | region start time: {1:F3}s | max start time: {2:F3}s",
								i,
								region.startTime,
								maxSecondaryTime);
						}
					}
					else
					{
						var duration = region.duration;

						if (LoggingToggles.ActionDrag)
						{
							sb.AppendFormat(
								"\n  {0} secondary | region start time: {1:F3}s | end time: {1:F3}s | duration: {2:F3}s",
								i,
								region.startTime,
								maxSecondaryTime,
								duration);
						}

						if (region.partID != IDUtility.invalidID)
						{
							if (lockoutPartLookup.TryGetValue(region.partID, out var lockout)
								&& timelineRegionsModified[lockout.Index].secondary
								&& maxSecondaryTime + duration + region.activationLockoutDuration >= lockout.Time)
							{
								duration += region.activationLockoutDuration;
								if (LoggingToggles.ActionDrag)
								{
									sb.AppendFormat(" | lockout duration: {0:F3}s", region.activationLockoutDuration);
								}
							}
							lockoutPartLookup[region.partID] = (i, maxSecondaryTime - duration);
							lockoutRegions.Add((i, region.partID));

							if (LoggingToggles.ActionDrag)
							{
								sb.AppendFormat(" | part ID: {0}", region.partID);
							}
						}
						maxSecondaryTime -= duration;

						if (LoggingToggles.ActionDrag)
						{
							sb.AppendFormat(" | max start time: {0:F3}s", maxSecondaryTime);
						}
					}
				}
			}

			var selectedRegion = timelineRegionsModified[selectedIndex];
			var primaryDuration = selectedRegion.duration;
			var secondaryDuration = selectedRegion.duration;

			if (LoggingToggles.ActionDrag)
			{
				sb.AppendFormat(
					"\n  {0} {1} | selected | region start time: {2:F3}s | duration: {3:F3}s",
					selectedIndex,
					selectedRegion.doubleTrack
						? "double"
						: selectedRegion.secondary
							? "secondary"
							: "primary",
					selectedRegion.startTime,
					selectedRegion.duration);
				sb.AppendFormat(
					"\n  loop done | max primary time: {0:F3}s | max secondary time: {1:F3}s",
					maxPrimaryTime,
					maxSecondaryTime);
			}

			if (selectedRegion.partID != IDUtility.invalidID)
			{
				if (LoggingToggles.ActionDrag)
				{
					sb.AppendFormat(
						" | lockout duration: {0:F3}s | part ID: {1}",
						selectedRegion.activationLockoutDuration,
						selectedRegion.partID);
				}

				if (lockoutPartLookup.TryGetValue(selectedRegion.partID, out var lockout))
				{
					if (LoggingToggles.ActionDrag)
					{
						sb.AppendFormat("\n  found lockout region | index: {0} | max start time: {1:F3}s", lockout.Index, lockout.Time);
					}

					var lockoutRegion = timelineRegionsModified[lockout.Index];
					if (selectedRegion.primary
						&& lockoutRegion.primary
						&& maxPrimaryTime + selectedRegion.activationLockoutDuration > lockout.Time)
					{
						maxPrimaryTime = lockout.Time;
						primaryDuration += selectedRegion.activationLockoutDuration;
						if (LoggingToggles.ActionDrag)
						{
							sb.Append(" | adjusted max primary");
						}
					}
					if (selectedRegion.secondary
						&& lockoutRegion.secondary
						&& maxSecondaryTime + selectedRegion.activationLockoutDuration > lockout.Time)
					{
						maxSecondaryTime = lockout.Time;
						secondaryDuration += selectedRegion.activationLockoutDuration;
						if (LoggingToggles.ActionDrag)
						{
							sb.Append("| adjusted max secondary");
						}
					}
				}
				lockoutRegions.Add((selectedIndex, selectedRegion.partID));
			}

			if (LoggingToggles.ActionDrag)
			{
				sb.AppendFormat(
					"\n  selected region durations | primary: {0:F3}s | secondary: {1:F3}s",
					primaryDuration,
					secondaryDuration);
				sb.AppendFormat(
					"\n  selected region max start times | primary: {0:F3}s | secondary: {1:F3}s",
					maxPrimaryTime - primaryDuration,
					maxSecondaryTime - secondaryDuration);
				Debug.LogFormat(
					"Mod {0} ({1}) {2}",
					ModLink.modIndex,
					ModLink.modID,
					sb);
			}

			return (maxPrimaryTime - primaryDuration, maxSecondaryTime - secondaryDuration);
		}

		static float AdjustEarlierStartTime(int selectedIndex, float startTime)
		{
			var selectedRegion = timelineRegionSelected;
			if (selectedRegion.partID == IDUtility.invalidID)
			{
				return startTime;
			}

			if (!lockoutPartLookup.TryGetValue(selectedRegion.partID, out var pair))
			{
				return startTime;
			}

			lockoutRegions.Add((selectedIndex, selectedRegion.partID));

			var lockoutRegion = timelineRegionsModified[pair.Index];
			if (selectedRegion.primary && lockoutRegion.primary && startTime < pair.Time)
			{
				startTime = pair.Time;
			}
			if (selectedRegion.secondary && lockoutRegion.secondary && startTime < pair.Time)
			{
				startTime = pair.Time;
			}

			return startTime;
		}

		static float AdjustLaterStartTime(int selectedIndex, float startTime)
		{
			if (lockoutRegions.Count > 1)
			{
				lockoutRegions.Reverse();
			}

			if (LoggingToggles.ActionDrag && lockoutRegions.Count != 0)
			{
				var sb = new System.Text.StringBuilder();
				foreach (var (index, _) in lockoutRegions)
				{
					var region = timelineRegionsModified[index];
					sb.AppendFormat(
						"\n  index: {0} | action ID: {1} ({2}) | track: {3} | action start time: {4:F3}s | duration: {5:F3}s | part ID: {6} | lockout duration: {7:F3}s",
						index,
						region.actionID,
						region.actionKey,
						region.doubleTrack
							? "double"
							: region.primary
								? "primary"
								: "secondary",
						region.startTime,
						region.duration,
						region.partID,
						region.activationLockoutDuration);
				}
				Debug.LogFormat(
					"Mod {0} ({1}) lockout regions{2}",
					ModLink.modIndex,
					ModLink.modID,
					sb);
			}

			return startTime;
		}

		static float CorrectEarlierOffset(
			float startTime,
			float actionStartTime,
			float offset)
		{
			if (startTime > actionStartTime)
			{
				offset += startTime - actionStartTime;
			}
			return offset;
		}

		static float CorrectLaterOffset(
			float startTime,
			float actionStartTime,
			float offset)
		{
			if (startTime < actionStartTime)
			{
				offset += startTime - actionStartTime;
			}
			return offset;
		}

		static void RepositionActionButtons(PBCIViewCombatTimeline inst, Traverse t)
		{
			var combat = Contexts.sharedInstance.combat;
			var turnStartTime = (float)combat.currentTurn.i * combat.turnLength.i;
			var helpersActionsPlanned = t.Field<Dictionary<int, CIHelperTimelineAction>>("helpersActionsPlanned").Value;

			foreach (var region in timelineRegionsModified)
			{
				if (!region.changed)
				{
					continue;
				}
				if (!helpersActionsPlanned.TryGetValue(region.actionID, out var helperTimelineAction))
				{
					continue;
				}

				var action = IDUtility.GetActionEntity(region.actionID);
				if (action == null)
				{
					continue;
				}
				if (action.isDisposed)
				{
					continue;
				}

				var x = inst.timelineOffsetLeft + (region.startTime - turnStartTime) * inst.timelineSecondSize;
				helperTimelineAction.transform.SetPositionLocalX(x);
				action.ReplaceStartTime(region.startTime);
			}
		}

		static int CompareByStartTime(TimelineRegion2 x, TimelineRegion2 y) => x.startTime.CompareTo(y.startTime);

		const float timeThreshold = 0.0005f;

		static readonly List<(int Index, int PartID)> lockoutRegions = new List<(int, int)>();
		static readonly Dictionary<int, (int Index, float Time)> lockoutPartLookup = new Dictionary<int, (int, float)>();

		static readonly List<TimelineRegion2> timelineRegions = new List<TimelineRegion2>();
		static readonly List<TimelineRegion2> timelineRegionsModified = new List<TimelineRegion2>();
		static TimelineRegion2 timelineRegionSelected;

		private sealed class TimelineRegion2
		{
			public float startTime;
			public float offset;
			public float duration;
			public int actionID = IDUtility.invalidID;
			public string actionKey;
			public bool wait;
			public bool changed;
			public bool primary;
			public bool secondary;
			public bool locked;
			public int partID = IDUtility.invalidID;
			public float activationLockoutDuration;

			public bool doubleTrack => primary && secondary;
			public float endTime => startTime + duration;
			public float lockoutEndTime => endTime + activationLockoutDuration;
		}
	}
}
