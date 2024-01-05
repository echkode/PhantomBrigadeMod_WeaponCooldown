using System.Collections.Generic;
using System.Text;

using PBCIViewCombatTimeline = CIViewCombatTimeline;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class ConsoleCommands
	{
		[ConsoleCommand("ui", "dump-timeline-actions", "Dump info about timeline action UI objects")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Console command registered through reflection")]
		static void DumpNGUI()
		{
			var scene = SceneManager.GetActiveScene();
			foreach (var rgo in scene.GetRootGameObjects())
			{
				var root = rgo.GetComponent<UIRoot>();
				if (root == null)
				{
					continue;
				}
				var timeline = root.GetComponentInChildren<PBCIViewCombatTimeline>();
				if (timeline == null)
				{
					break;
				}
				foreach (var uip in timeline.GetComponentsInChildren<UIPanel>())
				{
					if (uip.name == "Panel_ActionsPlanned")
					{
						var sb = new StringBuilder("NGUI dump: combat timeline actions");
						var res = DumpRootInfo(sb, root);
						DumpNGUIPanel(sb, res, uip);
						Debug.LogFormat(
							"Mod {0} ({1}) {2}",
							ModLink.modIndex,
							ModLink.modID,
							sb);
						break;
					}
				}
				break;
			}
		}

		static Vector2Int DumpRootInfo(StringBuilder sb, UIRoot root)
		{
			var cam = UILink.GetCamera();
			DumpCameraInfo(cam, "camera", 1, sb);
			var uipos = UIHelper.GetScreenToUISpace(Vector3.zero, root.transform, cam);
			sb.AppendFormat("\n  screenToUI | lower left: {0} -> {1}", Vector3.zero, uipos);
			var res = UIHelper.GetUIResolution(root);
			var center = res / 2;
			uipos = UIHelper.GetScreenToUISpace(new Vector3(center.x, center.y, 0f), root.transform, cam);
			sb.AppendFormat(" | center: {0} -> {1}", center, uipos);
			uipos = UIHelper.GetScreenToUISpace(new Vector3(res.x, res.y, 0f), root.transform, cam);
			sb.AppendFormat(" | upper right: {0} -> {1}", res, uipos);
			sb.AppendFormat(
				"\n  UIRoot | scaling: {0} | constraint: {1} | content width: {2} | content height {3} ({4}/{5}) | active height: {6} | pixel size adjustment: {7}",
				root.activeScaling,
				root.constraint,
				root.manualWidth,
				root.manualHeight,
				root.minimumHeight,
				root.maximumHeight,
				root.activeHeight,
				root.pixelSizeAdjustment);
			sb.AppendFormat("\n  {0} | scale: {1:F5} |", root.name, root.transform.localScale);

			return res;
		}

		static void DumpCameraInfo(Camera cam, string label, int indentLevel, StringBuilder sb)
		{
			var indent = new string(' ', indentLevel * 2);
			sb.AppendFormat(
				"\n{0}{1} | name: {2} | depth: {3} | aspect: {4}",
				indent,
				label,
				cam.name,
				cam.depth,
				cam.aspect);
			if (cam.allowDynamicResolution)
			{
				sb.Append(" | dynamic resolution");
			}
			if (cam.orthographic)
			{
				sb.AppendFormat(" | mode: ortho | height: {0}", cam.orthographicSize * 2);
			}
			else
			{
				sb.AppendFormat(
					" | mode: perspective | fov: {0} | near: {1} | far: {2} | position: {3} | facing: {4}",
					cam.fieldOfView,
					cam.nearClipPlane,
					cam.farClipPlane,
					cam.transform.position,
					cam.transform.forward);
			}
			var sr = cam.pixelRect;
			sb.AppendFormat(
				" | screen: {0}x{1}+({2},{3})",
				sr.width,
				sr.height,
				sr.x,
				sr.y);
		}

		static void DumpNGUIPanel(StringBuilder sb, Vector2Int res, UIPanel panel)
		{
			sb.AppendFormat("\n  {0}", UtilityTransform.GetTransformPath(panel.transform));
			if (!panel.gameObject.activeSelf)
			{
				sb.Append(" <inactive>");
				return;
			}

			if (panel.usedForUI)
			{
				sb.Append(" | UI");
			}
			if (!panel.alpha.RoughlyEqual(1f))
			{
				sb.AppendFormat(" | alpha: {0}", panel.alpha);
			}
			if (panel.transform.localScale != Vector3.one)
			{
				sb.AppendFormat(" | scale: {0:F5}", panel.transform.localScale);
			}
			var viewSize = panel.GetViewSize();
			if (viewSize.x != res.x || viewSize.y != res.y)
			{
				FormatUIPosition(sb, panel);
				sb.AppendFormat(" | size: {0}", viewSize);
				if (panel.transform.localPosition != Vector3.zero)
				{
					sb.AppendFormat(" | offset: {0}", panel.transform.localPosition);
				}
			}
			if (panel.isAnchored)
			{
				FormatAnchors(sb, panel);
			}
			sb.AppendFormat(" | depth: {0}", panel.depth);
			if (panel.useSortingOrder)
			{
				sb.AppendFormat(" | sort order: {0}", panel.sortingOrder);
			}
			if (panel.clipping != UIDrawCall.Clipping.None)
			{
				sb.AppendFormat(" | clip: {0}", panel.clipping);
			}
			if (panel.cullWhileDragging)
			{
				sb.Append(" | cull");
			}
			if (panel.widgetsAreStatic)
			{
				sb.Append(" | static");
			}
			if (panel.alwaysOnScreen)
			{
				sb.Append(" | always visible");
			}
			sb.Append(" |");
			FormatComponents(sb, panel.gameObject);
			var nk = panel.transform.childCount;
			if (nk != 0)
			{
				sb.AppendFormat("\n    subobjects: {0}", nk);
				for (var j = 0; j < nk; j += 1)
				{
					var child = panel.transform.GetChild(j);
					if (!child.gameObject.activeInHierarchy)
					{
						continue;
					}
					sb.AppendFormat("\n    #{0} {1} ({2})", j, child.name, child.childCount);
					if (child.localPosition != Vector3.zero)
					{
						sb.AppendFormat(" | offset: {0}", child.localPosition);
					}
					if (child.localScale != Vector3.one)
					{
						sb.AppendFormat(" | scale: {0}", child.localScale);
					}
					sb.Append(" |");
					FormatComponents(sb, child);
				}
			}
			if (panel.mChildren.size != 0)
			{
				sb.AppendFormat("\n    children: {0}", panel.mChildren.size);
				var j = 0;
				foreach (var child in panel.mChildren)
				{
					var ok = child.gameObject.activeInHierarchy;
					if (ok && child is UIWidget widget)
					{
						ok = widget.isVisible;
					}
					if (ok)
					{
						sb.AppendFormat(
							"\n    #{0} {1} | name: {2}",
							j,
							child.GetType().Name,
							GetPathToParent(panel, child));
					}
					j += 1;
				}
			}
			if (panel.widgets.Count != 0)
			{
				sb.AppendFormat("\n    widgets: {0}", panel.widgets.Count);
				var j = 0;
				foreach (var widget in panel.widgets)
				{
					if (widget.gameObject.activeInHierarchy && widget.isVisible)
					{
						sb.AppendFormat("\n    #{0}", j);
						DumpNGUIWidget(sb, panel, widget);
					}
					j += 1;
				}
			}
		}

		static void DumpNGUIWidget(StringBuilder sb, UIPanel panel, UIWidget widget)
		{
			sb.AppendFormat(" {0} | name: {1}", widget.GetType().Name, GetPathToPanel(panel, widget));
			sb.AppendFormat(" | size: {0}", widget.localSize);
			FormatUIPosition(sb, widget);
			if (widget.transform.localPosition != Vector3.zero && !widget.transform.localPosition.sqrMagnitude.RoughlyEqual(0f))
			{
				sb.AppendFormat(" | offset: {0} | pivot: {1}", widget.transform.localPosition, widget.pivot);
			}
			if (widget.isAnchored)
			{
				FormatAnchors(sb, widget);
			}
			sb.AppendFormat(" | depth: {0}", widget.depth);
			if (widget.hideIfOffScreen)
			{
				sb.Append(" | hide");
			}
			if (widget.transform.localScale != Vector3.one && !(widget.transform.localScale - Vector3.one).sqrMagnitude.RoughlyEqual(0f))
			{
				sb.AppendFormat(" | scale: {0}", widget.transform.localScale);
			}
			if (widget.mChildren.size != 0)
			{
				sb.AppendFormat(" | ui children: {0}", widget.mChildren.size);
			}
		}

		static void FormatUIPosition(StringBuilder sb, UIPanel panel)
		{
			var root = panel.root;
			var corners = panel.worldCorners;
			var lowerLeft = root.transform.InverseTransformPoint(corners[0]);
			var upperRight = root.transform.InverseTransformPoint(corners[2]);
			var center = root.transform.InverseTransformPoint(panel.transform.position);
			sb.AppendFormat(
				" | uipos: {0}x{1}+{2}",
				Vector2Int.RoundToInt(lowerLeft),
				Vector2Int.RoundToInt(upperRight),
				Vector2Int.RoundToInt(center));
		}

		static void FormatUIPosition(StringBuilder sb, UIWidget widget)
		{
			var root = widget.root;
			var corners = widget.worldCorners;
			var lowerLeft = root.transform.InverseTransformPoint(corners[0]);
			var upperRight = root.transform.InverseTransformPoint(corners[2]);
			var center = root.transform.InverseTransformPoint(widget.transform.position);
			sb.AppendFormat(
				" | uipos: {0}x{1}+{2}",
				Vector2Int.RoundToInt(lowerLeft),
				Vector2Int.RoundToInt(upperRight),
				Vector2Int.RoundToInt(center));
		}

		static void FormatAnchors(StringBuilder sb, UIRect rect)
		{
			sb.Append(" | anchors: ");
			if (rect.isFullyAnchored)
			{
				sb.Append("all");
			}
			else
			{
				if (rect.leftAnchor.target != null)
				{
					sb.Append("l,");
				}
				if (rect.rightAnchor.target != null)
				{
					sb.Append("r,");
				}
				if (rect.topAnchor.target != null)
				{
					sb.Append("t,");
				}
				if (rect.bottomAnchor.target != null)
				{
					sb.Append("b,");
				}
				sb.Remove(sb.Length - 1, 1);
			}
		}

		static string GetPathToPanel(UIPanel panel, UIWidget widget) => GetPathToParent(panel, widget);
		static string GetPathToParent(UIRect parent, UIRect descendant)
		{
			var names = new List<string>();
			var p = parent.transform;
			var t = descendant.transform;
			while (t != null && t != p)
			{
				names.Add(t.name);
				t = t.parent;
			}
			names.Reverse();
			return string.Join("/", names);
		}

		static void FormatComponents(StringBuilder sb, Transform t) => FormatComponents(sb, t.gameObject);
		static void FormatComponents(StringBuilder sb, GameObject go)
		{
			sb.Append(" [");
			foreach (var c in go.GetComponents<Component>())
			{
				var name = c.GetType().Name;
				if (name == "Transform")
				{
					continue;
				}
				sb.AppendFormat("{0} ", name);
			}
			if (sb[sb.Length - 1] == ' ')
			{
				sb[sb.Length - 1] = ']';
			}
			else
			{
				sb.Append(']');
			}
		}
	}
}
