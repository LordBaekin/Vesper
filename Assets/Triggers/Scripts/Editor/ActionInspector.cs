using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace DevionGames
{
    /// <summary>
    /// Base inspector for action lists, with safe layout and context menus.
    /// </summary>
    public abstract class ActionInspector : Editor
    {
        protected SerializedProperty m_Script;
        protected SerializedProperty m_Actions;

        protected virtual void OnEnable()
        {
            if (target == null) return;
            m_Script = serializedObject.FindProperty("m_Script");
            m_Actions = serializedObject.FindProperty("m_Actions");
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(m_Script);
            EditorGUI.EndDisabledGroup();

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, m_Script.propertyPath, m_Actions.propertyPath);
            GUILayout.Space(5f);
            ActionGUI();
            serializedObject.ApplyModifiedProperties();
        }

        protected void ActionGUI()
        {
            EditorGUIUtility.wideMode = true;
            for (int i = 0; i < m_Actions.arraySize; i++)
            {
                var actionProp = m_Actions.GetArrayElementAtIndex(i);
                object value = actionProp.GetValue();

                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(target, "Action");

                // Titlebar with context menu
                if (EditorTools.Titlebar(value, ElementContextMenu(m_Actions.GetValue() as IList, i)))
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(true);
                    MonoScript script = (value != null)
                        ? EditorTools.FindMonoScript(value.GetType())
                        : null;
                    EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), true);
                    EditorGUI.EndDisabledGroup();

                    if (value == null)
                    {
                        EditorGUILayout.HelpBox(
                            "Managed reference values can’t be removed or replaced…",
                            MessageType.Error);
                    }
                    else if (EditorTools.HasCustomPropertyDrawer(value.GetType()))
                    {
                        EditorGUILayout.PropertyField(actionProp, true);
                    }
                    else
                    {
                        foreach (var child in actionProp.EnumerateChildProperties())
                        {
                            EditorGUILayout.PropertyField(child, includeChildren: true);
                        }
                    }
                    EditorGUI.indentLevel--;
                }

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(target);
                }
            }

            GUILayout.FlexibleSpace();
            DoActionAddButton();
            GUILayout.Space(10f);
        }

        protected void AddAction(Type type)
        {
            object value = Activator.CreateInstance(type);
            m_Actions.serializedObject.Update();
            m_Actions.arraySize++;
            m_Actions.GetArrayElementAtIndex(m_Actions.arraySize - 1).managedReferenceValue = value;
            m_Actions.serializedObject.ApplyModifiedProperties();
        }

        protected void CreateActionScript(string scriptName)
        {
            Debug.LogWarning("Not implemented yet.");
        }

        /// <summary>
        /// Draws the "Add Action" button centered in the inspector.
        /// </summary>
        protected void DoActionAddButton()
        {
            GUIStyle buttonStyle = new GUIStyle("AC Button");
            GUIContent buttonContent = new GUIContent("Add Action");
            Rect buttonRect = GUILayoutUtility.GetRect(buttonContent, buttonStyle, GUILayout.ExpandWidth(true));

            // Center the button using the current inspector width
            float inspectorWidth = EditorGUIUtility.currentViewWidth;
            buttonRect.x = (inspectorWidth - buttonRect.width) * 0.5f;
            buttonRect.width = buttonStyle.fixedWidth;

            if (GUI.Button(buttonRect, buttonContent, buttonStyle))
            {
                AddObjectWindow.ShowWindow(buttonRect, typeof(Action), AddAction, CreateActionScript);
            }
        }

        /// <summary>
        /// Builds a context menu for an action element, with safe null and bounds checks.
        /// </summary>
        protected GenericMenu ElementContextMenu(IList list, int index)
        {
            var menu = new GenericMenu();
            if (list == null || index < 0 || index >= list.Count || list[index] == null)
                return menu;

            Type elementType = list[index].GetType();
            menu.AddItem(new GUIContent("Reset"), false, () =>
            {
                object newValue = Activator.CreateInstance(elementType);
                list[index] = newValue;
                EditorUtility.SetDirty(target);
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Remove"), false, () =>
            {
                list.RemoveAt(index);
                EditorUtility.SetDirty(target);
            });

            if (index > 0)
            {
                menu.AddItem(new GUIContent("Move Up"), false, () =>
                {
                    var tmp = list[index]; list.RemoveAt(index); list.Insert(index - 1, tmp);
                    EditorUtility.SetDirty(target);
                });
            }
            else menu.AddDisabledItem(new GUIContent("Move Up"));

            if (index < list.Count - 1)
            {
                menu.AddItem(new GUIContent("Move Down"), false, () =>
                {
                    var tmp = list[index]; list.RemoveAt(index); list.Insert(index + 1, tmp);
                    EditorUtility.SetDirty(target);
                });
            }
            else menu.AddDisabledItem(new GUIContent("Move Down"));

            // Edit Script option if script asset exists
            var scriptAsset = EditorTools.FindMonoScript(elementType);
            if (scriptAsset != null)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Edit Script"), false, () => AssetDatabase.OpenAsset(scriptAsset));
            }

            return menu;
        }
    }
}
