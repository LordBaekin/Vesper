using UnityEditor;
using UnityEngine;

namespace DevionGames
{
    [InitializeOnLoad]
    public static class WriteInputManager
    {
        // Static ctor fires once on editor load
        static WriteInputManager()
        {
            // Define your axes in one place, using target-typed new()
            var axesToEnsure = new InputAxis[]
            {
                new()
                {
                    name = "Change Speed",
                    positiveButton = "left shift",
                    gravity = 1000f,
                    dead = 0.1f,
                    sensitivity = 1000f,
                    type = AxisType.KeyOrMouseButton,
                    axis = 1
                },
                new()
                {
                    name = "Crouch",
                    positiveButton = "c",
                    gravity = 1000f,
                    dead = 0.1f,
                    sensitivity = 1000f,
                    type = AxisType.KeyOrMouseButton,
                    axis = 1
                },
                new()
                {
                    name = "No Control",
                    positiveButton = "left ctrl",
                    gravity = 1000f,
                    dead = 0.1f,
                    sensitivity = 1000f,
                    type = AxisType.KeyOrMouseButton,
                    axis = 1
                },
                new()
                {
                    name = "Evade",
                    positiveButton = "left alt",
                    gravity = 1000f,
                    dead = 0.1f,
                    sensitivity = 1000f,
                    type = AxisType.KeyOrMouseButton,
                    axis = 1
                }
            };

            foreach (var axis in axesToEnsure)
            {
                if (!AxisDefined(axis.name))
                    AddAxis(axis);
            }
        }

        private static bool AxisDefined(string axisName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("❌ Could not find ProjectSettings/InputManager.asset");
                return false;
            }

            var so = new SerializedObject(assets[0]);
            var axes = so.FindProperty("m_Axes");
            if (axes == null || !axes.isArray)
            {
                Debug.LogError("❌ InputManager.asset has no m_Axes array");
                return false;
            }

            for (int i = 0; i < axes.arraySize; i++)
            {
                var entry = axes.GetArrayElementAtIndex(i);
                var nameProp = entry.FindPropertyRelative("m_Name");
                if (nameProp != null && nameProp.stringValue == axisName)
                    return true;
            }
            return false;
        }

        private static void AddAxis(InputAxis axis)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("❌ Could not find ProjectSettings/InputManager.asset");
                return;
            }

            var so = new SerializedObject(assets[0]);
            var axes = so.FindProperty("m_Axes");
            if (axes == null || !axes.isArray)
            {
                Debug.LogError("❌ InputManager.asset has no m_Axes array");
                return;
            }

            // Expand array
            axes.arraySize++;
            so.ApplyModifiedProperties();

            // Grab the brand-new element
            var entry = axes.GetArrayElementAtIndex(axes.arraySize - 1);

            // Populate each field
            entry.FindPropertyRelative("m_Name").stringValue = axis.name;
            entry.FindPropertyRelative("descriptiveName").stringValue = axis.descriptiveName;
            entry.FindPropertyRelative("descriptiveNegativeName").stringValue = axis.descriptiveNegativeName;
            entry.FindPropertyRelative("negativeButton").stringValue = axis.negativeButton;
            entry.FindPropertyRelative("positiveButton").stringValue = axis.positiveButton;
            entry.FindPropertyRelative("altNegativeButton").stringValue = axis.altNegativeButton;
            entry.FindPropertyRelative("altPositiveButton").stringValue = axis.altPositiveButton;
            entry.FindPropertyRelative("gravity").floatValue = axis.gravity;
            entry.FindPropertyRelative("dead").floatValue = axis.dead;
            entry.FindPropertyRelative("sensitivity").floatValue = axis.sensitivity;
            entry.FindPropertyRelative("snap").boolValue = axis.snap;
            entry.FindPropertyRelative("invert").boolValue = axis.invert;
            entry.FindPropertyRelative("type").intValue = (int)axis.type;
            // Unity’s InputManager stores axes zero-based, so subtract 1
            entry.FindPropertyRelative("axis").intValue = axis.axis - 1;
            entry.FindPropertyRelative("joyNum").intValue = axis.joyNum;

            so.ApplyModifiedProperties();
            Debug.Log($"✅ Added Input Axis: {axis.name}");
        }

        public enum AxisType
        {
            KeyOrMouseButton = 0,
            MouseMovement = 1,
            JoystickAxis = 2
        }

        public class InputAxis
        {
            public string name;
            public string descriptiveName = "";
            public string descriptiveNegativeName = "";
            public string negativeButton = "";
            public string positiveButton = "";
            public string altNegativeButton = "";
            public string altPositiveButton = "";
            public float gravity;
            public float dead;
            public float sensitivity;
            public bool snap = false;
            public bool invert = false;
            public AxisType type;
            public int axis;
            public int joyNum;
        }
    }
}
