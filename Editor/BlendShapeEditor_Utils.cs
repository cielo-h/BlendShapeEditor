using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class VRCBlendShapeEditor : EditorWindow
{
    private string GetSessionKey(string fieldName) => $"{nameof(VRCBlendShapeEditor)}_{fieldName}";

    private string GetValueFilterModeDisplayName(ValueFilterMode mode) => mode switch
    {
        ValueFilterMode.All => "すべて表示",
        ValueFilterMode.NonZero => "0以外",
        ValueFilterMode.EqualTo => "等しい",
        ValueFilterMode.GreaterThanEqual => "以上",
        ValueFilterMode.GreaterThan => "より大きい",
        ValueFilterMode.LessThanEqual => "以下",
        ValueFilterMode.LessThan => "未満",
        _ => mode.ToString(),
    };

    private static string GetPathFromRoot(Transform transform, string separator = " > ")
    {
        string path = transform.name;
        Transform current = transform;

        while (current.parent != null)
        {
            current = current.parent;
            if (current.parent != null)
            {
                path = current.name + separator + path;
            }
        }

        return path;
    }

    private bool freezeFilter = false;
    private readonly Dictionary<List<BlendShapeData>, List<BlendShapeData>> frozenFilterCache = new();

    private List<BlendShapeData> GetFilteredBlendShapesRaw(List<BlendShapeData> blendShapes)
    {
        return blendShapes.Where(bs =>
        {
            bool matchesSearch = string.IsNullOrEmpty(searchFilter) ||
                               bs.name.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0;

            bool matchesValue = true;
            switch (valueFilterMode)
            {
                case ValueFilterMode.All:
                    matchesValue = true;
                    break;
                case ValueFilterMode.NonZero:
                    matchesValue = Mathf.Abs(bs.value) > 0.01f;
                    break;
                case ValueFilterMode.EqualTo:
                    matchesValue = Mathf.Abs(bs.value - filterValue) < 0.01f;
                    break;
                case ValueFilterMode.GreaterThanEqual:
                    matchesValue = bs.value >= filterValue;
                    break;
                case ValueFilterMode.GreaterThan:
                    matchesValue = bs.value > filterValue;
                    break;
                case ValueFilterMode.LessThanEqual:
                    matchesValue = bs.value <= filterValue;
                    break;
                case ValueFilterMode.LessThan:
                    matchesValue = bs.value < filterValue;
                    break;
            }

            return matchesSearch && matchesValue;
        }).ToList();
    }

    private List<BlendShapeData> GetFilteredBlendShapes(List<BlendShapeData> blendShapes)
    {
        if (!freezeFilter)
            return GetFilteredBlendShapesRaw(blendShapes);

        if (!frozenFilterCache.TryGetValue(blendShapes, out var snapshot))
        {
            snapshot = GetFilteredBlendShapesRaw(blendShapes);
            frozenFilterCache[blendShapes] = snapshot;
        }
        return snapshot;
    }

    private void SelectAllVisible(bool selected)
    {
        if (blendShapeCache == null) return;

        foreach (var kvp in blendShapeCache)
        {
            var blendShapes = kvp.Value;
            var filteredBlendShapes = GetFilteredBlendShapes(blendShapes);

            foreach (var bs in filteredBlendShapes)
            {
                bs.isSelected = selected;
            }
        }
    }

    private int GetSelectedCount()
    {
        if (blendShapeCache == null) return 0;

        int count = 0;
        foreach (var kvp in blendShapeCache)
        {
            count += kvp.Value.Count(bs => bs.isSelected);
        }
        return count;
    }

    private bool? GetMeshSelectionState(List<BlendShapeData> filtered)
    {
        if (filtered.Count == 0) return false;
        int selected = filtered.Count(bs => bs.isSelected);
        if (selected == 0) return false;
        if (selected == filtered.Count) return true;
        return null;
    }

    private void SetMeshSelection(List<BlendShapeData> filtered, bool value)
    {
        foreach (var bs in filtered)
            bs.isSelected = value;
    }
}
