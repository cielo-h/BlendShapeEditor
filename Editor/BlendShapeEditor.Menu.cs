using nadena.dev.modular_avatar.core;
using net.narazaka.avatarmenucreator.components;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class VRCBlendShapeEditor : EditorWindow
{
    private string toggleMenuName = "Shape_Fixed";
    private bool toggleAddMAMenuInstaller = false;
    private bool toggleSaved = true;
    private bool toggleSynced = true;
    private bool toggleDefaultOn = true;

    private void CreateToggleMenuFromSelection()
    {
        if (targetAvatar == null)
        {
            EditorUtility.DisplayDialog("エラー", "対象アバターを指定してください", "OK");
            return;
        }

        // 選択されたBlendShapeを収集(meshパスはアバターからの相対パス)
        var entries = new List<(string meshPath, string shapeName, float value)>();
        foreach (var kvp in blendShapeCache)
        {
            var smr = kvp.Key;
            if (smr == null) continue;
            string path = GetPathFromRoot(smr.transform, "/");
            foreach (var bs in kvp.Value.Where(b => b.isSelected))
                entries.Add((path, bs.name, bs.value));
        }

        if (entries.Count == 0)
        {
            EditorUtility.DisplayDialog("エラー", "BlendShapeが選択されていません", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Create AvatarToggleMenuCreator");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            var menuObject = new GameObject(toggleMenuName);
            Undo.RegisterCreatedObjectUndo(menuObject, "Create Toggle Menu Object");
            menuObject.transform.SetParent(targetAvatar.transform);
            menuObject.transform.SetAsLastSibling();
            menuObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            menuObject.transform.localScale = Vector3.one;

            if (toggleAddMAMenuInstaller)
                VRC.Core.ExtensionMethods.GetOrAddComponent<ModularAvatarMenuInstaller>(menuObject);

            var creator = Undo.AddComponent<AvatarToggleMenuCreator>(menuObject);
            var so = new SerializedObject(creator);

            var menuProp = so.FindProperty("AvatarToggleMenu")
                ?? throw new System.Exception("AvatarToggleMenuプロパティが見つかりません。Debugモードでフィールド名を確認してください。");

            SetBool(menuProp, "Saved", toggleSaved);
            SetBool(menuProp, "Synced", toggleSynced);
            SetBool(menuProp, "ToggleDefaultValue", toggleDefaultOn);

            var bsProp = menuProp.FindPropertyRelative("ToggleBlendShapes")
                ?? throw new System.Exception("ToggleBlendShapesプロパティが見つかりません。");
            var k1 = bsProp.FindPropertyRelative("keys1");
            var k2 = bsProp.FindPropertyRelative("keys2");
            var vals = bsProp.FindPropertyRelative("values");
            if (k1 == null || k2 == null || vals == null)
                throw new System.Exception("ToggleBlendShapesの辞書プロパティが見つかりません。");

            k1.ClearArray(); k2.ClearArray(); vals.ClearArray();

            for (int i = 0; i < entries.Count; i++)
            {
                var (meshPath, shapeName, value) = entries[i];
                k1.InsertArrayElementAtIndex(i);
                k2.InsertArrayElementAtIndex(i);
                vals.InsertArrayElementAtIndex(i);

                k1.GetArrayElementAtIndex(i).stringValue = meshPath;
                k2.GetArrayElementAtIndex(i).stringValue = shapeName;

                var v = vals.GetArrayElementAtIndex(i);
                SetFloat(v, "Inactive", 0f);
                SetFloat(v, "Active", value);
                SetFloat(v, "TransitionOffsetPercent", 0f);
                SetFloat(v, "TransitionDurationPercent", 100f);
                SetBool(v, "OmitInactive", false);
                SetBool(v, "OmitActive", false);
                SetBool(v, "OmitTransitionToInactive", false);
                SetBool(v, "OmitTransitionToActive", false);
            }

            so.ApplyModifiedProperties();

            Selection.activeGameObject = menuObject;
            EditorGUIUtility.PingObject(menuObject);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"{toggleMenuName} を作成しました ({entries.Count}個のBlendShape)");
        }
        catch (System.Exception e)
        {
            Undo.RevertAllInCurrentGroup();
            EditorUtility.DisplayDialog("エラー", $"作成中にエラーが発生しました:\n\n{e.Message}", "OK");
            Debug.LogError($"Error creating ToggleMenu: {e}");
        }
    }

    private static void SetBool(SerializedProperty parent, string name, bool value)
    {
        var p = parent.FindPropertyRelative(name)
            ?? throw new System.Exception($"プロパティ '{name}' が見つかりません。");
        p.boolValue = value;
    }

    private static void SetFloat(SerializedProperty parent, string name, float value)
    {
        var p = parent.FindPropertyRelative(name)
            ?? throw new System.Exception($"プロパティ '{name}' が見つかりません。");
        p.floatValue = value;
    }
}
