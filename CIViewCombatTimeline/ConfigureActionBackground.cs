using System.Collections.Generic;

using HarmonyLib;

using PhantomBrigade.Data;
using PBCIViewCombatTimeline = CIViewCombatTimeline;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	using HelperDepthInfo = System.ValueTuple<int, int, CIHelperTimelineAction>;

	public static partial class CIViewCombatTimeline
	{
		public static int ConfigureActionPaintedBackground(
			Dictionary<int, CIHelperTimelineAction> helpersActionsPlanned,
			int unitID,
			DataContainerAction actionData,
			CIHelperTimelineAction helper,
			bool showOverlap,
			float overlapDelta,
			Color color,
			int w)
		{
			var (okPart, part) = DataHelperAction.TryGetEquipmentPart(unitID, actionData);
			if (!okPart)
			{
				ResetBackgroundSprites(helper, color);
				return w;
			}

			var (ok, duration) = DataHelperStats.TryGetActivationLockoutDuration(part, actionData.dataCore);
			if (!ok)
			{
				ResetBackgroundSprites(helper, color);
				return w;
			}

			var fade = showOverlap && overlapDelta < 0f;
			if (!fade)
			{
				var startPosition = helper.transform.localPosition.x;
				var trackLine = helper.transform.localPosition.y;
				var endPosition = startPosition + w + Mathf.RoundToInt(duration * PBCIViewCombatTimeline.ins.timelineSecondSize);
				orderedHelpers.Clear();
				orderedHelpers.AddRange(helpersActionsPlanned.Values);
				orderedHelpers.Sort(CompareByStartPosition);
				for (var i = 0; i < orderedHelpers.Count; i += 1)
				{
					var existingHelper = orderedHelpers[i];
					var localPosition = existingHelper.transform.localPosition;
					if (localPosition.x < startPosition)
					{
						continue;
					}
					if (localPosition.x > endPosition)
					{
						break;
					}

					fade = localPosition.y == trackLine
						|| localPosition.y == doubleTrackLine
						|| trackLine == doubleTrackLine;
					break;
				}
				orderedHelpers.Clear();
			}
			ConfigureSpritePainted(helper.spriteBackgroundPainted, 5, fade ? paintedAlpha : 1f);

			w = EnableLockoutBackground(helper, color, duration, w);
			ConfigureSpritePainted(helper.spriteBackgroundWarning, 6, fade ? lockoutAlpha : 1f);

			return w;
		}

		static void ConfigureSpritePainted(UISprite sprite, int depth, float alpha)
		{
			sprite.depth = depth;
			var c = sprite.color;
			c.a = alpha;
			sprite.color = c;
		}

		public static int ConfigureActionBackground(
			ActionEntity action,
			CIHelperTimelineAction helper,
			Color color,
			int w)
		{
			var sprite = helper.spriteBackground;
			sprite.depth = 7;

			if (action == null || action.isLocked)
			{
				ResetBackgroundSprites(helper, color);
				return w;
			}

			if (!action.hasHitPredictions)
			{
				ResetBackgroundSprites(helper, color);
				return w;
			}

			var hitPredictions = action.hitPredictions.hitPredictions;
			if (hitPredictions.Count == 0)
			{
				ResetBackgroundSprites(helper, color);
				return w;
			}

			var duration = hitPredictions[0].time;
			color.a = lockoutAlpha;
			return EnableLockoutBackground(helper, color, duration, w);
		}

		static void ResetBackgroundSprites(CIHelperTimelineAction helper, Color color)
		{
			var sprite = helper.spriteBackground;
			if (sprite.rightAnchor.target == null)
			{
				sprite.SetAnchor(helper.widget.gameObject, 0, 0, 0, 0, 1, 0, 1, 0);
			}

			sprite = helper.spriteBackgroundPainted;
			if (sprite.gameObject.activeSelf)
			{
				sprite.depth = 22;
			}

			sprite = helper.spriteBackgroundWarning;
			if (sprite.gameObject.activeSelf)
			{
				sprite.color = color;
				sprite.depth = 4;
				sprite.SetAnchor(0, 0, 0, -24, 1, 0, 0, 16);
				sprite.gameObject.SetActive(false);
			}
		}

		static int EnableLockoutBackground(
			CIHelperTimelineAction helper,
			Color color,
			float duration,
			int w)
		{
			var sprite = helper.spriteBackground;
			sprite.rightAnchor.target = null;
			sprite.ResetAndUpdateAnchors();
			sprite.width = w;

			sprite = helper.spriteBackgroundWarning;
			sprite.gameObject.SetActive(true);
			sprite.color = color;
			sprite.SetAnchor(0, 6, 0, 0, 1, 0, 1, 0);

			return w + Mathf.RoundToInt(duration * PBCIViewCombatTimeline.ins.timelineSecondSize);
		}

		public static void HidePaintedOverlap()
		{
			var t = new Traverse(PBCIViewCombatTimeline.ins);
			var paintedHelper = t.Field<CIHelperTimelineAction>("helperActionPainted").Value;

			if (paintedHelper.gameObject.activeSelf)
			{
				paintedHelper.spriteBackgroundOverlap.gameObject.SetActive(false);
			}

			if (PBCIViewCombatTimeline.ins.hideableWarningOverlap.gameObject.activeSelf)
			{
				t.Field<bool>("warningTimeoutLock").Value = false;
			}
		}

		public static void FixActionWidgetDepths(CIHelperTimelineAction helper, Dictionary<int, CIHelperTimelineAction> helpersActionsPlanned)
		{
			if (LoggingToggles.ActionWidgetDepths || LoggingToggles.ActionWidgetDepthsVerbose)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) FixActionWidgetDepths enter | planned actions count: {2}",
					ModLink.modIndex,
					ModLink.modID,
					helpersActionsPlanned.Count);
			}

			if (helpersActionsPlanned.Count == 0)
			{
				return;
			}
			else if (LoggingToggles.ActionWidgetDepths || LoggingToggles.ActionWidgetDepthsVerbose)
			{
				var sb = new System.Text.StringBuilder();
				DumpHelper("placed helper", helper, sb);
				Debug.LogFormat(
					"Mod {0} ({1}) FixActionWidgetDepths {2}",
					ModLink.modIndex,
					ModLink.modID,
					sb);

				if (LoggingToggles.ActionWidgetDepthsVerbose)
				{
					sb.Clear();
					foreach (var kvp in helpersActionsPlanned)
					{
						DumpHelper("\n  ID: " + kvp.Key, kvp.Value, sb);
					}
					Debug.LogFormat(
						"Mod {0} ({1}) FixActionWidgetDepths planned actions ({2}){3}",
						ModLink.modIndex,
						ModLink.modID,
						helpersActionsPlanned.Count,
						sb);
				}
			}

			InitializeTrackLines();

			orderedHelpers.Clear();
			collectedHelpers.Clear();
			primaryTrackStack.Clear();
			secondaryTrackStack.Clear();

			var placedLeftEdge = LeftEdge(helper.widget);
			var hasLockout = HasLockout(helper);
			var depthInfo = new DepthInfo();
			var seenPlaced = false;

			orderedHelpers.AddRange(helpersActionsPlanned.Values);
			orderedHelpers.Sort(CompareByStartPosition);

			if (LoggingToggles.ActionWidgetDepths || LoggingToggles.ActionWidgetDepthsVerbose)
			{
				DumpHelpers("before");
			}

			for (var i = 0; i < orderedHelpers.Count; i += 1)
			{
				var existing = orderedHelpers[i];

				var existingLeftEdge = LeftEdge(existing.widget);
				var leftEdge = !seenPlaced
					? Mathf.Min(placedLeftEdge, existingLeftEdge)
					: existingLeftEdge;

				FlattenStacks(depthInfo, leftEdge);

				if (seenPlaced && !depthInfo.hasOverlap)
				{
					break;
				}

				if (seenPlaced)
				{
					PushHelper(depthInfo, existing);
					continue;
				}

				seenPlaced = helper == existing;

				if (placedLeftEdge == leftEdge && seenPlaced)
				{
					if (!hasLockout && !depthInfo.hasOverlap)
					{
						break;
					}
					PushHelper(depthInfo, helper);
					FlattenStacks(depthInfo, existingLeftEdge);
					if (!depthInfo.hasOverlap && !HasLockout(existing))
					{
						break;
					}
					PushHelper(depthInfo, existing);
					continue;
				}

				if (depthInfo.hasOverlap || HasLockout(existing))
				{
					PushHelper(depthInfo, existing);
				}

				if (seenPlaced)
				{
					FlattenStacks(depthInfo, placedLeftEdge);
					if (!depthInfo.hasOverlap && !hasLockout)
					{
						break;
					}
					PushHelper(depthInfo, helper);
				}
			}

			FlattenStacks(depthInfo, int.MaxValue);
			ProcessCollectedHelpers();

			if (LoggingToggles.ActionWidgetDepths || LoggingToggles.ActionWidgetDepthsVerbose)
			{
				DumpHelpers("after");
			}

			orderedHelpers.Clear();
			collectedHelpers.Clear();
			primaryTrackStack.Clear();
			secondaryTrackStack.Clear();

			if (LoggingToggles.ActionWidgetDepths || LoggingToggles.ActionWidgetDepthsVerbose)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) FixActionWidgetDepths exit",
					ModLink.modIndex,
					ModLink.modID);
			}
		}

		static void FlattenStacks(DepthInfo depthInfo, int leftEdge)
		{
			depthInfo.primaryTrack = FlattenStack(
				leftEdge,
				depthInfo.primaryTrack,
				primaryTrackStack);
			depthInfo.hasPrimaryOverlap = primaryTrackStack.Count != 0;

			depthInfo.secondaryTrack = FlattenStack(
				leftEdge,
				depthInfo.secondaryTrack,
				secondaryTrackStack);
			depthInfo.hasSecondaryOverlap = secondaryTrackStack.Count != 0;

			depthInfo.hasOverlap = depthInfo.hasPrimaryOverlap || depthInfo.hasSecondaryOverlap;
		}

		static int FlattenStack(
			int leftEdge,
			int trackDepth,
			Stack<HelperDepthInfo> trackStack)
		{
			if (LoggingToggles.ActionWidgetDepthsVerbose)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) FlattenStack enter | count: {2} | track: {3} | depth: {4} | left edge: {5}",
					ModLink.modIndex,
					ModLink.modID,
					trackStack.Count,
					trackStack == primaryTrackStack
						? "primary"
						: "secondary",
					trackDepth,
					leftEdge);
			}

			var warningDepth = 0;
			while (trackStack.Count != 0)
			{
				var (_, rightTop) = CornersToUI(trackStack.Peek().Item3.widget);
				var rightEdge = rightTop.x;
				if (rightEdge > leftEdge)
				{
					break;
				}
				var info = trackStack.Pop();
				warningDepth = info.Item2 != 0
					? Mathf.Min(info.Item2, warningDepth)
					: warningDepth;
				info.Item2 = warningDepth;
				collectedHelpers.Add(info);
				trackDepth -= 1;
				warningDepth -= 1;
			}

			warningDepth = UpdateWarningDepth(trackStack, warningDepth);

			if (LoggingToggles.ActionWidgetDepthsVerbose)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) FlattenStack exit | count: {2} | depth: {3} | warning depth: {4}",
					ModLink.modIndex,
					ModLink.modID,
					trackStack.Count,
					trackDepth,
					warningDepth);
			}

			return trackDepth;
		}

		static int UpdateWarningDepth(Stack<HelperDepthInfo> trackStack, int warningDepth)
		{
			tempHelpers.Clear();
			while (trackStack.Count != 0)
			{
				var info = trackStack.Pop();
				warningDepth = Mathf.Min(info.Item2, warningDepth);
				info.Item2 = warningDepth;
				tempHelpers.Add(info);
				warningDepth -= 1;
			}
			tempHelpers.Reverse();
			foreach (var helper in tempHelpers)
			{
				trackStack.Push(helper);
			}
			tempHelpers.Clear();

			return warningDepth;
		}

		static void PushHelper(DepthInfo info, CIHelperTimelineAction helper)
		{
			var hasLockout = HasLockout(helper);
			var localPosition = helper.transform.localPosition;
			var isSecondary = localPosition.y == secondaryTrackLine;
			var isPrimary = localPosition.y == primaryTrackLine;
			if (localPosition.y == doubleTrackLine)
			{
				isSecondary = isPrimary = true;
			}

			var sb = LoggingToggles.ActionWidgetDepthsVerbose
				? new System.Text.StringBuilder()
				: null;

			if (isSecondary && (hasLockout || info.hasSecondaryOverlap))
			{
				secondaryTrackStack.Push((info.secondaryTrack, 0, helper));
				info.secondaryTrack += 1;
				info.hasSecondaryOverlap = true;
				if (LoggingToggles.ActionWidgetDepthsVerbose)
				{
					sb.Append(" | secondary stack");
				}
			}

			if (isPrimary && (hasLockout || info.hasPrimaryOverlap))
			{
				primaryTrackStack.Push((info.primaryTrack, 0, helper));
				info.primaryTrack += 1;
				info.hasPrimaryOverlap = true;
				if (LoggingToggles.ActionWidgetDepthsVerbose)
				{
					sb.Append(" | primary stack");
				}
			}

			if (LoggingToggles.ActionWidgetDepthsVerbose)
			{
				DumpHelper("", helper, sb);
				Debug.LogFormat(
					"Mod {0} ({1}) FixActionWidgetDepths push helper{2}",
					ModLink.modIndex,
					ModLink.modID,
					sb);
			}

			info.hasOverlap = info.hasPrimaryOverlap || info.hasSecondaryOverlap;
		}

		static void ProcessCollectedHelpers()
		{
			if (collectedHelpers.Count != 0)
			{
				var baseWidgetDepth = 11;
				var baseWarningDepth = 4;
				for (var i = 0; i < collectedHelpers.Count; i += 1)
				{
					var (widgetOffset, warningOffset, existing) = collectedHelpers[i];
					existing.widget.depth = baseWidgetDepth + widgetOffset;
					existing.spriteBackgroundWarning.depth = baseWarningDepth + warningOffset;
				}
			}
		}

		static void DumpHelpers(string label)
		{
			var sb = new System.Text.StringBuilder();
			for (var i = 0; i < orderedHelpers.Count; i += 1)
			{
				var helper = orderedHelpers[i];
				DumpHelper("\n  #" + i, helper, sb);
			}
			Debug.LogFormat(
				"Mod {0} ({1}) FixActionWidgetDepths helpers ordered by X position ({2}){3}",
				ModLink.modIndex,
				ModLink.modID,
				label,
				sb);
		}

		static void DumpHelper(string label, CIHelperTimelineAction helper, System.Text.StringBuilder sb)
		{
			var widget = helper.widget;
			var localPosition = widget.transform.localPosition;
			var secondaryTrack = localPosition.y == secondaryTrackLine;
			var doubleTrack = localPosition.y == doubleTrackLine;
			var root = widget.root;
			var corners = widget.worldCorners;
			var lowerLeft = root.transform.InverseTransformPoint(corners[0]);
			var upperRight = root.transform.InverseTransformPoint(corners[2]);
			var center = root.transform.InverseTransformPoint(widget.transform.position);
			sb.AppendFormat(
				"{0} | uipos: {1}x{2}+{3} | track: {4} | widget depth: {5} | warning depth: {6}",
				label,
				Vector2Int.RoundToInt(lowerLeft),
				Vector2Int.RoundToInt(upperRight),
				Vector2Int.RoundToInt(center),
				doubleTrack
					? "double"
					: secondaryTrack
						? "secondary"
						: "primary",
				widget.depth,
				helper.spriteBackgroundWarning.depth);
		}

		static (Vector2Int LeftBottom, Vector2Int RightTop) CornersToUI(UIWidget widget)
		{
			var root = widget.root;
			var corners = widget.worldCorners;
			var leftBottom = root.transform.InverseTransformPoint(corners[0]);
			var rightTop = root.transform.InverseTransformPoint(corners[2]);
			return (Vector2Int.RoundToInt(leftBottom), Vector2Int.RoundToInt(rightTop));
		}

		static int LeftEdge(UIWidget widget) => CornersToUI(widget).LeftBottom.x;

		static bool HasLockout(CIHelperTimelineAction helper)
		{
			var warning = helper.spriteBackgroundWarning;
			return warning.gameObject.activeSelf && helper.widget.height == warning.height;
		}

		static int CompareByStartPosition(CIHelperTimelineAction a, CIHelperTimelineAction b) =>
			a.transform.localPosition.x.CompareTo(b.transform.localPosition.x);

		static void InitializeTrackLines()
		{
			if (initializedTrackLines)
			{
				return;
			}

			primaryTrackLine = PBCIViewCombatTimeline.ins.timelineOffsetBottom + PBCIViewCombatTimeline.ins.timelineLineHeight;
			secondaryTrackLine = primaryTrackLine + PBCIViewCombatTimeline.ins.timelineLineHeight;
			doubleTrackLine = primaryTrackLine + doubleTrackOffset;

			initializedTrackLines = true;
		}

		class DepthInfo
		{
			public bool hasPrimaryOverlap;
			public bool hasSecondaryOverlap;
			public bool hasOverlap;

			public int primaryTrack;
			public int secondaryTrack;
		}

		static readonly List<CIHelperTimelineAction> orderedHelpers = new List<CIHelperTimelineAction>();
		static readonly List<HelperDepthInfo> collectedHelpers = new List<HelperDepthInfo>();
		static readonly Stack<HelperDepthInfo> primaryTrackStack = new Stack<HelperDepthInfo>();
		static readonly Stack<HelperDepthInfo> secondaryTrackStack = new Stack<HelperDepthInfo>();
		static readonly List<HelperDepthInfo> tempHelpers = new List<HelperDepthInfo>();

		static bool initializedTrackLines;
		static int primaryTrackLine;
		static int secondaryTrackLine;
		static int doubleTrackLine;

		const float lockoutAlpha = 0.75f;
		const float paintedAlpha = 0.5f;
		const int doubleTrackOffset = 16;
	}
}
