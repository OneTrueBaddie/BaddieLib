namespace Baddie.Utils
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public enum LogType
    {
        Normal,
        Warning,
        Error
    }

    public enum LogColour
    {
        Red,
        Green,
        Blue,
        Yellow,
        Orange,
        Purple,
        Cyan,
        Magenta,
        Lime,
        Pink
    }

    public class Debugger
    {

        public static bool DebugMode = true;

        public static Dictionary<LogColour, string> Colours = new()
        {
            { LogColour.Red, "#FF0000" },
            { LogColour.Green, "#00FF00" },
            { LogColour.Blue, "#0000FF" },
            { LogColour.Yellow, "#FFFF00" },
            { LogColour.Orange, "#FFA500" },
            { LogColour.Purple, "#800080" },
            { LogColour.Cyan, "#00FFFF" },
            { LogColour.Magenta, "#FF00FF" },
            { LogColour.Lime, "#00FF00" },
            { LogColour.Pink, "#FFC0CB" }
        };

        public static void Log(object log, LogType type = LogType.Normal)
        {
            #if UNITY_EDITOR || DEBUG

            if (type == LogType.Error)
                Debug.LogError(log);
            else if (!DebugMode)
                return;

            if (type == LogType.Normal)
                Debug.Log(log);
            else if (type == LogType.Warning)
                Debug.LogWarning(log);

            #endif
        }

        public static void Log(object log, LogColour colour, LogType type = LogType.Normal)
        {
            #if UNITY_EDITOR || DEBUG

            string hex = Colours[colour];

            log = $"<color={hex}>{log}</color>";

            if (type == LogType.Error)
                Debug.LogError(log);
            else if (!DebugMode)
                return;

            if (type == LogType.Normal)
                Debug.Log(log);
            else if (type == LogType.Warning)
                Debug.LogWarning(log);

            #endif
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(InfoAttribute))]
    public class InfoDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label)
        {
            string valueStr;

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    valueStr = prop.intValue.ToString();
                    break;
                case SerializedPropertyType.Boolean:
                    valueStr = prop.boolValue.ToString();
                    break;
                case SerializedPropertyType.Float:
                    valueStr = prop.floatValue.ToString($"F{InfoAttribute.NumOfDecimals}");
                    break;
                case SerializedPropertyType.String:
                    valueStr = prop.stringValue;
                    break;
                case SerializedPropertyType.Vector2:
                    valueStr = prop.vector2Value.ToString();
                    break;
                case SerializedPropertyType.Vector3:
                    valueStr = prop.vector3Value.ToString();
                    break;
                case SerializedPropertyType.Vector4:
                    valueStr = prop.vector4Value.ToString();
                    break;
                case SerializedPropertyType.Color:
                    valueStr = prop.colorValue.ToString();
                    break;
                case SerializedPropertyType.Rect:
                    valueStr = prop.rectValue.ToString();
                    break;
                case SerializedPropertyType.Enum:
                    valueStr = prop.enumDisplayNames.ToString();
                    break;
                case SerializedPropertyType.Quaternion:
                    valueStr = prop.quaternionValue.ToString();
                    break;
                case SerializedPropertyType.Gradient:
                    valueStr = prop.gradientValue.ToString();
                    break;
                case SerializedPropertyType.ObjectReference:
                    valueStr = prop.objectReferenceValue != null ? prop.objectReferenceValue.name : null;
                    break;
                case SerializedPropertyType.ArraySize:
                    valueStr = prop.arraySize.ToString();
                    break;
                case SerializedPropertyType.Bounds:
                    valueStr = prop.boundsValue.ToString();
                    break;
                case SerializedPropertyType.ExposedReference:
                    valueStr = prop.exposedReferenceValue.ToString();
                    break;
                case SerializedPropertyType.ManagedReference:
                    valueStr = prop.managedReferenceValue.ToString();
                    break;
                case SerializedPropertyType.Hash128:
                    valueStr = prop.hash128Value.ToString();
                    break;
                default:
                    valueStr = null;
                    break;
            }

            EditorGUI.LabelField(position, label.text, valueStr);
        }
    }
#endif

    public class InfoAttribute : PropertyAttribute 
    {
        public static int NumOfDecimals = 2;
    }
}
