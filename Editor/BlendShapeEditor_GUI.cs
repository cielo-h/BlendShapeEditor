using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class VRCBlendShapeEditor : EditorWindow
{

    [MenuItem("Tools/BlendShapeEditor")]
    public static void ShowWindow()
    {
        var window = GetWindow<VRCBlendShapeEditor>("BlendShape Editor");
        window.minSize = new Vector2(500, 600);
    }

    private void OnGUI()
    {
        if (targetAvatar == null)
        {
            var avatarDescriptors = FindObjectsOfType<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (avatarDescriptors.Length == 1)
            {
                targetAvatar = avatarDescriptors[0].gameObject;
            }
        }

        EditorGUI.BeginChangeCheck();
        GameObject newAvatar = (GameObject)EditorGUILayout.ObjectField(
            "Target Avatar",
            targetAvatar,
            typeof(GameObject),
            true
        );

        if (EditorGUI.EndChangeCheck())
        {
            targetAvatar = newAvatar;
            RefreshBlendShapes();
        }
        else if (blendShapeCache == null)
        {
            RefreshBlendShapes();
        }

        if (targetAvatar == null)
        {
            EditorGUILayout.HelpBox(
                "VRChatアバターのGameObjectを選択してください。\n" +
                "Hierarchy内のアバタールートオブジェクトをドラッグ&ドロップしてください。",
                MessageType.Info
            );
            return;
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        string newSearchFilter = EditorGUILayout.TextField("検索:", searchFilter);
        if (EditorGUI.EndChangeCheck())
        {
            searchFilter = newSearchFilter;
        }

        if (GUILayout.Button("×", GUILayout.Width(25)))
        {
            if (!string.IsNullOrEmpty(searchFilter))
            {
                searchFilter = "";
            }
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        DrawSettings();

        EditorGUILayout.Space(5);

        DrawFoldAndExpandButton();

        DrawCopyPasteButton();

        EditorGUILayout.Space(5);

        if (blendShapeCache == null || blendShapeCache.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "BlendShapeが見つかりませんでした。\n" +
                "SkinnedMeshRendererコンポーネントを持つオブジェクトが必要です。",
                MessageType.Warning
            );
            return;
        }

        DrawBlendShapeList();
    }

    private void DrawSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();

        var allModes = System.Enum.GetValues(typeof(ValueFilterMode));
        string[] displayNames = new string[allModes.Length];
        for (int i = 0; i < allModes.Length; i++)
        {
            displayNames[i] = GetValueFilterModeDisplayName((ValueFilterMode)allModes.GetValue(i));
        }

        int currentIndex = (int)valueFilterMode;
        int newIndex = EditorGUILayout.Popup("値フィルター:", currentIndex, displayNames, GUILayout.Width(250));

        if (EditorGUI.EndChangeCheck())
        {
            valueFilterMode = (ValueFilterMode)newIndex;
        }

        if (valueFilterMode != ValueFilterMode.All)
        {
            EditorGUI.BeginChangeCheck();
            float newFilterValue = EditorGUILayout.FloatField(filterValue, GUILayout.Width(60));
            if (EditorGUI.EndChangeCheck())
            {
                filterValue = newFilterValue;
            }
        }

        EditorGUILayout.EndHorizontal();

        DrawCheckboxSettings();

        EditorGUILayout.EndVertical();
    }

    private void DrawCheckboxSettings()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        bool newGroupByMesh = EditorGUILayout.ToggleLeft("メッシュ別にグループ化", groupByMesh, GUILayout.Width(150));
        if (EditorGUI.EndChangeCheck())
        {
            groupByMesh = newGroupByMesh;
        }

        EditorGUI.BeginChangeCheck();
        bool newSkipIfEditorOnly = EditorGUILayout.ToggleLeft("EditorOnlyを含まない", skipIfEditorOnly, GUILayout.Width(150));
        if (EditorGUI.EndChangeCheck())
        {
            skipIfEditorOnly = newSkipIfEditorOnly;
            RefreshBlendShapes();
        }

        EditorGUI.BeginChangeCheck();
        bool newHideVRCShapes = EditorGUILayout.ToggleLeft("vrc.で始まるものを非表示", hideVRCShapes, GUILayout.Width(150));
        if (EditorGUI.EndChangeCheck())
        {
            hideVRCShapes = newHideVRCShapes;
            RefreshBlendShapes();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        bool newCopyOnClickLabel = EditorGUILayout.ToggleLeft("BlendShape名をクリックしてコピーする", copyOnClickLabel, GUILayout.Width(200));
        if (EditorGUI.EndChangeCheck())
        {
            copyOnClickLabel = newCopyOnClickLabel;
        }

        EditorGUI.BeginChangeCheck();
        bool newLinkVisibleShapes = EditorGUILayout.ToggleLeft("表示されているものを一括変更する", linkVisibleShapes, GUILayout.Width(200));
        if (EditorGUI.EndChangeCheck())
        {
            linkVisibleShapes = newLinkVisibleShapes;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        bool newShowSelectionCheckboxes = EditorGUILayout.ToggleLeft("コピーするBlendShapeを選択", showSelectionCheckboxes, GUILayout.Width(250));
        if (EditorGUI.EndChangeCheck())
        {
            showSelectionCheckboxes = newShowSelectionCheckboxes;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawFoldAndExpandButton()
    {
        GUILayout.BeginHorizontal();
        float buttonWidth = (EditorGUIUtility.currentViewWidth - 20) / 2;

        if (GUILayout.Button("全て展開", GUILayout.Width(buttonWidth)))
        {
            foreach (var key in foldoutStates.Keys.ToList())
            {
                foldoutStates[key] = true;
            }
        }

        if (GUILayout.Button("全て折りたたむ", GUILayout.Width(buttonWidth)))
        {
            foreach (var key in foldoutStates.Keys.ToList())
            {
                foldoutStates[key] = false;
            }
        }

        GUILayout.EndHorizontal();
    }

    private void DrawCopyPasteButton()
    {
        GUILayout.BeginHorizontal();
        float buttonWidth = (EditorGUIUtility.currentViewWidth - 20) / 2;

        if (showSelectionCheckboxes)
        {
            int selectedCount = GetSelectedCount();

            if (GUILayout.Button($"選択をコピー ({selectedCount}個)", GUILayout.Width(buttonWidth)))
            {
                CopySelectedBlendShapes();
            }
        }
        else
        {
            if (GUILayout.Button("クリップボードにコピー", GUILayout.Width(buttonWidth)))
            {
                CopyVisibleBlendShapes();
            }
        }

        if (GUILayout.Button("クリップボードからペースト", GUILayout.Width(buttonWidth)))
        {
            if (EditorUtility.DisplayDialog(
                "確認",
                "クリップボードのデータをペーストしますか？\nこの操作は元に戻せます（Ctrl+Z）。",
                "OK",
                "キャンセル"
                ))
            {
                PasteBlendShapes();
            }
        }

        GUILayout.EndHorizontal();

        if (showSelectionCheckboxes)
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("全て選択", GUILayout.Width(buttonWidth)))
            {
                SelectAllVisible(true);
            }

            if (GUILayout.Button("全て解除", GUILayout.Width(buttonWidth)))
            {
                SelectAllVisible(false);
            }

            GUILayout.EndHorizontal();
        }
    }

    private void DrawBlendShapeList()
    {
        int totalCount = 0;
        int displayedCount = 0;

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var kvp in blendShapeCache)
        {
            var smr = kvp.Key;
            string rootPath = GetPathFromRoot(smr.transform);
            var blendShapes = kvp.Value;

            var filteredBlendShapes = GetFilteredBlendShapes(blendShapes);

            totalCount += blendShapes.Count;
            displayedCount += filteredBlendShapes.Count;

            if (filteredBlendShapes.Count == 0) continue;

            if (groupByMesh)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                GUIContent foldoutContent = new(
                    $"{smr.gameObject.name} ({filteredBlendShapes.Count}個)",
                    GetPathFromRoot(smr.transform)
                    );

                EditorGUI.BeginChangeCheck();
                bool newFoldoutState = EditorGUILayout.Foldout(
                    foldoutStates[smr],
                    foldoutContent,
                    true,
                    EditorStyles.foldoutHeader
                );

                if (EditorGUI.EndChangeCheck())
                {
                    foldoutStates[smr] = newFoldoutState;
                }

                if (foldoutStates[smr])
                {
                    EditorGUILayout.Space(3);

                    foreach (var bs in filteredBlendShapes)
                    {
                        DrawBlendShapeRow(smr, bs);
                    }
                }

                EditorGUILayout.EndVertical();
            }
            else
            {
                foreach (var bs in filteredBlendShapes)
                {
                    DrawBlendShapeRow(smr, bs);
                }
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"表示中: {displayedCount} / 全体: {totalCount}個のBlendShape",
            EditorStyles.miniLabel
        );
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBlendShapeRow(SkinnedMeshRenderer smr, BlendShapeData bs)
    {
        EditorGUILayout.BeginHorizontal();

        if (showSelectionCheckboxes)
        {
            EditorGUI.BeginChangeCheck();
            bool newSelected = EditorGUILayout.Toggle(bs.isSelected, GUILayout.Width(20));
            if (EditorGUI.EndChangeCheck())
            {
                bs.isSelected = newSelected;
            }
        }

        if (copyOnClickLabel && GUILayout.Button(bs.name, EditorStyles.label, GUILayout.Width(200)))
        {
            EditorGUIUtility.systemCopyBuffer = bs.name;
            Debug.Log($"BlendShape({bs.name})をコピーしました。");
        }

        EditorGUILayout.LabelField($"{bs.value:F1}", GUILayout.Width(50));

        EditorGUI.BeginChangeCheck();
        float newValue = EditorGUILayout.Slider(bs.value, 0f, 100f);
        if (EditorGUI.EndChangeCheck())
        {
            UpdateBlendShapeValue(smr, bs, newValue);
        }

        EditorGUILayout.EndHorizontal();
    }
}
