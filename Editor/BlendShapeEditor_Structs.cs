using System.Collections.Generic;
using UnityEditor;

public partial class VRCBlendShapeEditor : EditorWindow
{

    private class BlendShapeData
    {
        public string name;
        public int index;
        public float value;
        public bool isSelected;
    }

    [System.Serializable]
    private class BlendShapeClipboard
    {
        public List<BlendShapeEntry> entries = new();
    }

    [System.Serializable]
    private class BlendShapeEntry
    {
        public string meshPath;
        public string blendShapeName;
        public float value;
    }

    private enum ValueFilterMode
    {
        All,    // すべて表示
        NonZero,    // 0以外
        EqualTo,    // 指定値と等しい
        GreaterThanEqual,   // 指定値以上
        GreaterThan,    // 指定値より大きい
        LessThanEqual,  // 指定値以下
        LessThan,   // 指定値未満
    }
}
