using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MouseClickDebugger : MonoBehaviour
{
    [SerializeField] private bool logOnClick = true;
    [SerializeField] private bool showHoverOverlay = true;

    private readonly List<RaycastResult> _results = new List<RaycastResult>();
    private string _hoverText = string.Empty;

    private void Update()
    {
        var mouse = Mouse.current;
        var eventSystem = EventSystem.current;
        if (mouse == null || eventSystem == null) return;

        Vector2 position = mouse.position.ReadValue();
        Raycast(eventSystem, position);

        if (showHoverOverlay)
            _hoverText = _results.Count > 0 ? $"Hover: {GetPath(_results[0].gameObject)}" : "Hover: nothing";

        if (logOnClick && mouse.leftButton.wasPressedThisFrame)
            Debug.Log(BuildReport(position));
    }

    private void Raycast(EventSystem eventSystem, Vector2 position)
    {
        var pointer = new PointerEventData(eventSystem) { position = position };
        _results.Clear();
        eventSystem.RaycastAll(pointer, _results);
    }

    private string BuildReport(Vector2 position)
    {
        var report = new StringBuilder();
        report.AppendLine($"[Click] at {position}");

        if (_results.Count == 0)
        {
            report.AppendLine("  No raycast hits at all.");
        }
        else
        {
            var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(_results[0].gameObject);
            report.AppendLine($"  Click goes to: {(receiver != null ? GetPath(receiver) : "nothing clickable (top hit has no click handler above it)")}");
            report.AppendLine("  Hits, top first:");

            for (int i = 0; i < _results.Count; i++)
            {
                var hit = _results[i];
                var canvas = hit.gameObject.GetComponentInParent<Canvas>();
                report.AppendLine($"    {i}. {GetPath(hit.gameObject)}  [canvas '{(canvas != null ? canvas.name : "none")}' order {hit.sortingOrder}, raycaster {hit.module?.GetType().Name}]");
            }
        }

        AppendMissedSelectables(report, position);
        return report.ToString();
    }

    private void AppendMissedSelectables(StringBuilder report, Vector2 position)
    {
        bool headerWritten = false;

        foreach (var selectable in Selectable.allSelectablesArray)
        {
            if (!IsUnderPointer(selectable, position)) continue;
            if (_results.Count > 0 && IsSameOrChild(_results[0].gameObject, selectable.gameObject)) continue;

            if (!headerWritten)
            {
                report.AppendLine("  Buttons under the mouse that did NOT get the click:");
                headerWritten = true;
            }

            report.AppendLine($"    - {GetPath(selectable.gameObject)}: {DiagnoseMiss(selectable)}");
        }
    }

    private string DiagnoseMiss(Selectable selectable)
    {
        var reasons = new List<string>();
        var graphic = selectable.targetGraphic;

        if (!selectable.IsInteractable())
            reasons.Add("not interactable");

        if (graphic == null)
        {
            reasons.Add("no Target Graphic assigned");
        }
        else
        {
            if (!graphic.raycastTarget)
                reasons.Add("Target Graphic has Raycast Target unticked");

            var ownCanvas = graphic.canvas;
            if (ownCanvas != null && ownCanvas.GetComponent<GraphicRaycaster>() == null)
                reasons.Add($"its Canvas '{ownCanvas.name}' has no Graphic Raycaster");
        }

        foreach (var group in selectable.GetComponentsInParent<CanvasGroup>())
        {
            if (!group.blocksRaycasts)
                reasons.Add($"Canvas Group on '{group.name}' has Blocks Raycasts unticked");
            if (group.ignoreParentGroups)
                break;
        }

        if (reasons.Count == 0 && _results.Count > 0)
            reasons.Add($"covered by '{GetPath(_results[0].gameObject)}'");

        return reasons.Count > 0 ? string.Join(", ", reasons) : "unknown";
    }

    private static bool IsUnderPointer(Selectable selectable, Vector2 position)
    {
        if (!selectable.isActiveAndEnabled || !(selectable.transform is RectTransform rect)) return false;

        var canvas = selectable.GetComponentInParent<Canvas>();
        if (canvas == null) return false;

        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, position, camera);
    }

    private static bool IsSameOrChild(GameObject hit, GameObject target)
    {
        return hit == target || hit.transform.IsChildOf(target.transform);
    }

    private static string GetPath(GameObject go)
    {
        var path = new StringBuilder(go.name);
        var parent = go.transform.parent;
        while (parent != null)
        {
            path.Insert(0, parent.name + "/");
            parent = parent.parent;
        }
        return path.ToString();
    }

    private void OnGUI()
    {
        if (!showHoverOverlay) return;
        GUI.Label(new Rect(10, 10, 1000, 25), _hoverText);
    }
}