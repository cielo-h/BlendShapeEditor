using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
public partial class VRCBlendShapeEditor : EditorWindow
{
    private GameObject targetAvatar;
    private Vector2 scrollPosition;
    private Dictionary<SkinnedMeshRenderer, List<BlendShapeData>> blendShapeCache;
    private Dictionary<SkinnedMeshRenderer, bool> foldoutStates;
    private string searchFilter = "";
    private ValueFilterMode valueFilterMode = ValueFilterMode.All;
    private float filterValue = 0f;
    private bool groupByMesh = true;
    private bool skipIfEditorOnly = true;
    private bool hideVRCShapes = true;
    private bool linkVisibleShapes = false;
    private bool copyOnClickLabel = true;
    private bool showSelectionCheckboxes = false;

    private const string AvatarStoreKey = "AvatarInstanceID";

    private void OnEnable()
    {
        valueFilterMode = (ValueFilterMode)SessionState.GetInt(GetSessionKey(nameof(valueFilterMode)), 0);
        filterValue = SessionState.GetFloat(GetSessionKey(nameof(filterValue)), 0f);
        groupByMesh = SessionState.GetBool(GetSessionKey(nameof(groupByMesh)), true);
        skipIfEditorOnly = SessionState.GetBool(GetSessionKey(nameof(skipIfEditorOnly)), true);
        hideVRCShapes = SessionState.GetBool(GetSessionKey(nameof(hideVRCShapes)), true);
        linkVisibleShapes = SessionState.GetBool(GetSessionKey(nameof(linkVisibleShapes)), false);
        copyOnClickLabel = SessionState.GetBool(GetSessionKey(nameof(copyOnClickLabel)), true);
        showSelectionCheckboxes = SessionState.GetBool(GetSessionKey(nameof(showSelectionCheckboxes)), false);
        searchFilter = SessionState.GetString(GetSessionKey(nameof(searchFilter)), "");
        float scrollX = SessionState.GetFloat(GetSessionKey("ScrollX"), 0f);
        float scrollY = SessionState.GetFloat(GetSessionKey("ScrollY"), 0f);
        scrollPosition = new Vector2(scrollX, scrollY);
        int avatarInstanceID = SessionState.GetInt(GetSessionKey(AvatarStoreKey), 0);
        if (avatarInstanceID != 0)
        {
            targetAvatar = EditorUtility.InstanceIDToObject(avatarInstanceID) as GameObject;
        }

        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        SessionState.SetInt(GetSessionKey(nameof(valueFilterMode)), (int)valueFilterMode);
        SessionState.SetFloat(GetSessionKey(nameof(filterValue)), filterValue);
        SessionState.SetBool(GetSessionKey(nameof(groupByMesh)), groupByMesh);
        SessionState.SetBool(GetSessionKey(nameof(skipIfEditorOnly)), skipIfEditorOnly);
        SessionState.SetBool(GetSessionKey(nameof(hideVRCShapes)), hideVRCShapes);
        SessionState.SetBool(GetSessionKey(nameof(linkVisibleShapes)), linkVisibleShapes);
        SessionState.SetBool(GetSessionKey(nameof(copyOnClickLabel)), copyOnClickLabel);
        SessionState.SetBool(GetSessionKey(nameof(showSelectionCheckboxes)), showSelectionCheckboxes);
        SessionState.SetString(GetSessionKey(nameof(searchFilter)), searchFilter);
        SessionState.SetFloat(GetSessionKey("ScrollX"), scrollPosition.x);
        SessionState.SetFloat(GetSessionKey("ScrollY"), scrollPosition.y);

        if (targetAvatar != null)
        {
            SessionState.SetInt(GetSessionKey(AvatarStoreKey), targetAvatar.GetInstanceID());
        }
        else
        {
            SessionState.SetInt(GetSessionKey(AvatarStoreKey), 0);
        }

        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void RefreshBlendShapes()
    {
        blendShapeCache = new Dictionary<SkinnedMeshRenderer, List<BlendShapeData>>();
        foldoutStates ??= new Dictionary<SkinnedMeshRenderer, bool>();

        if (targetAvatar == null) return;

        var skinnedMeshRenderers = targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        foreach (var smr in skinnedMeshRenderers)
        {
            if (skipIfEditorOnly && smr.gameObject.CompareTag("EditorOnly")) continue;

            if (smr.sharedMesh == null) continue;

            var blendShapes = new List<BlendShapeData>();
            int blendShapeCount = smr.sharedMesh.blendShapeCount;

            for (int i = 0; i < blendShapeCount; i++)
            {
                string name = smr.sharedMesh.GetBlendShapeName(i);

                if (hideVRCShapes && name.StartsWith("vrc.")) continue;

                float value = smr.GetBlendShapeWeight(i);

                blendShapes.Add(new BlendShapeData
                {
                    name = name,
                    index = i,
                    value = value,
                    isSelected = false,
                });
            }

            if (blendShapes.Count > 0)
            {
                blendShapeCache[smr] = blendShapes;

                if (!foldoutStates.ContainsKey(smr))
                {
                    foldoutStates[smr] = true;
                }
            }
        }

        var keysToRemove = foldoutStates.Keys.Where(k => !blendShapeCache.ContainsKey(k)).ToList();
        foreach (var key in keysToRemove)
        {
            foldoutStates.Remove(key);
        }
    }

    private void UpdateBlendShapeValue(SkinnedMeshRenderer smr, BlendShapeData bs, float newValue)
    {
        if (linkVisibleShapes)
        {
            ApplyLinkedSliderValue(newValue);
        }
        else
        {
            Undo.RecordObject(smr, "Change BlendShape Weight");
            smr.SetBlendShapeWeight(bs.index, newValue);
            bs.value = newValue;
            EditorUtility.SetDirty(smr);
        }
    }

    private void ApplyLinkedSliderValue(float value)
    {
        if (blendShapeCache == null) return;

        foreach (var kvp in blendShapeCache)
        {
            var smr = kvp.Key;
            var blendShapes = kvp.Value;

            var filteredBlendShapes = GetFilteredBlendShapes(blendShapes);

            if (filteredBlendShapes.Count > 0)
            {
                Undo.RecordObject(smr, "Change Linked BlendShape Weights");

                foreach (var bs in filteredBlendShapes)
                {
                    smr.SetBlendShapeWeight(bs.index, value);
                    bs.value = value;
                }

                EditorUtility.SetDirty(smr);
            }
        }
    }

    private void OnUndoRedo()
    {
        RefreshBlendShapes();
        Repaint();
    }

    private void OnFocus() => RefreshBlendShapeValues();

    private void RefreshBlendShapeValues()
    {
        if (targetAvatar != null && blendShapeCache != null)
        {
            foreach (var kvp in blendShapeCache)
            {
                var smr = kvp.Key;
                if (smr == null) continue; // nullチェック

                var blendShapes = kvp.Value;

                for (int i = 0; i < blendShapes.Count; i++)
                {
                    blendShapes[i].value = smr.GetBlendShapeWeight(blendShapes[i].index);
                }
            }
        }
    }

    private void CopyVisibleBlendShapes()
    {
        if (blendShapeCache == null) return;

        var clipboard = new BlendShapeClipboard();

        foreach (var kvp in blendShapeCache)
        {
            var smr = kvp.Key;
            var blendShapes = kvp.Value;
            var filteredBlendShapes = GetFilteredBlendShapes(blendShapes);

            foreach (var bs in filteredBlendShapes)
            {
                clipboard.entries.Add(new BlendShapeEntry
                {
                    meshPath = GetPathFromRoot(smr.transform, "/"),
                    blendShapeName = bs.name,
                    value = bs.value
                });
            }
        }

        EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(clipboard, true);

        Debug.Log($"BlendShape {clipboard.entries.Count}個をクリップボードにコピーしました");
    }

    private void CopySelectedBlendShapes()
    {
        if (blendShapeCache == null) return;

        var clipboard = new BlendShapeClipboard();
        int selectedCount = 0;

        foreach (var kvp in blendShapeCache)
        {
            var smr = kvp.Key;
            var blendShapes = kvp.Value;

            foreach (var bs in blendShapes)
            {
                if (bs.isSelected)
                {
                    clipboard.entries.Add(new BlendShapeEntry
                    {
                        meshPath = GetPathFromRoot(smr.transform, "/"),
                        blendShapeName = bs.name,
                        value = bs.value
                    });
                    selectedCount++;
                }
            }
        }

        if (selectedCount == 0)
        {
            Debug.LogWarning("BlendShapeが選択されていません");
            return;
        }

        EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(clipboard, true);

        Debug.Log($"選択されたBlendShape {selectedCount}個をクリップボードにコピーしました");
    }

    private void PasteBlendShapes()
    {
        string clipboardText = EditorGUIUtility.systemCopyBuffer;

        if (string.IsNullOrEmpty(clipboardText))
        {
            Debug.LogWarning("クリップボードが空です");
            return;
        }

        BlendShapeClipboard clipboard;
        try
        {
            clipboard = JsonUtility.FromJson<BlendShapeClipboard>(clipboardText);
            if (clipboard == null || clipboard.entries == null || clipboard.entries.Count == 0)
            {
                Debug.LogWarning("クリップボードに有効なBlendShapeデータがありません");
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"クリップボードのデータ解析に失敗しました: {e.Message}");
            return;
        }

        if (blendShapeCache == null) return;

        var clipboardDict = new Dictionary<string, float>();
        foreach (var entry in clipboard.entries)
        {
            string key = $"{entry.meshPath}:{entry.blendShapeName}";
            clipboardDict[key] = entry.value;
        }

        int appliedCount = 0;
        int skippedCount = 0;

        foreach (var kvp in blendShapeCache)
        {
            var smr = kvp.Key;
            var blendShapes = kvp.Value;
            bool modified = false;

            Undo.RecordObject(smr, "Paste BlendShape Weights");

            foreach (var bs in blendShapes)
            {
                string key = $"{GetPathFromRoot(smr.transform, "/")}:{bs.name}";

                if (clipboardDict.TryGetValue(key, out float copiedValue))
                {
                    smr.SetBlendShapeWeight(bs.index, copiedValue);
                    bs.value = copiedValue;
                    modified = true;
                    appliedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(smr);
            }
        }

        Debug.Log($"BlendShapeをペーストしました - 適用: {appliedCount}個, スキップ: {skippedCount}個");
    }
}
#endif