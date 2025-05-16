using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DevionGames.InventorySystem;
using DevionGames.AI;
using DevionGames.QuestSystem;
using DevionGames.CharacterSystem;
using DevionGames.StatSystem;

namespace DevionGames.RPGKit
{
    public class RPGKitEditor : EditorWindow
    {
        public int m_SystemIndex = 0;
        public GUIContent[] m_SystemContent;
        private GUISkin m_Skin;

        private InventorySystemInspector m_InventorySystemInspector;
        private UtilityAIInspector m_UtilityAIInspector;
        private QuestSystemInspector m_QuestSystemInspector;
        private CharacterSystemInspector m_CharacterSystemInspector;
        private StatSystemInspector m_StatSystemInspector;

        public static void ShowWindow()
        {
            RPGKitEditor[] objArray = Resources.FindObjectsOfTypeAll<RPGKitEditor>();
            RPGKitEditor editor = objArray.Length <= 0
                ? ScriptableObject.CreateInstance<RPGKitEditor>()
                : objArray[0];

            editor.hideFlags = HideFlags.HideAndDontSave;
            editor.minSize = new Vector2(690, 300);
            editor.titleContent = new GUIContent("RPG Kit");
            editor.Show();
        }

        private void OnEnable()
        {
            m_Skin = Resources.Load<GUISkin>("EditorSkin");
            m_SystemContent = new GUIContent[5];
            m_SystemContent[0] = new GUIContent(Resources.Load<Texture>("Item"), "Inventory System");
            m_SystemContent[1] = new GUIContent((Texture)EditorGUIUtility.LoadRequired("d_Animator Icon"), "Utility AI");
            m_SystemContent[2] = new GUIContent(Resources.Load<Texture>("Quest"), "Quest System");
            m_SystemContent[3] = new GUIContent((Texture)EditorGUIUtility.LoadRequired("d_NavMeshAgent Icon"), "Character System");
            m_SystemContent[4] = new GUIContent(Resources.Load<Texture>("Stats"), "Stat System");

            m_InventorySystemInspector = new InventorySystemInspector();
            m_InventorySystemInspector.OnEnable();
            m_UtilityAIInspector = new UtilityAIInspector();
            m_UtilityAIInspector.OnEnable();
            m_QuestSystemInspector = new QuestSystemInspector();
            m_QuestSystemInspector.OnEnable();
            m_CharacterSystemInspector = new CharacterSystemInspector();
            m_CharacterSystemInspector.OnEnable();
            m_StatSystemInspector = new StatSystemInspector();
            m_StatSystemInspector.OnEnable();
        }

        private void OnDisable()
        {
            m_InventorySystemInspector.OnDisable();
            m_UtilityAIInspector.OnDisable();
            m_QuestSystemInspector.OnDisable();
            m_CharacterSystemInspector.OnDisable();
            m_StatSystemInspector.OnDisable();
        }

        private void OnDestroy()
        {
            m_InventorySystemInspector.OnDestroy();
            m_UtilityAIInspector.OnDestroy();
            m_QuestSystemInspector.OnDestroy();
            m_CharacterSystemInspector.OnDestroy();
            m_StatSystemInspector.OnDestroy();
        }

        private void Update()
        {
            if (EditorWindow.mouseOverWindow == this)
                Repaint();
        }

        private void OnGUI()
        {
            // Horizontal split: left toolbar, right inspector area
            EditorGUILayout.BeginHorizontal();

            // Left toolbar
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.17647f, 0.17647f, 0.17647f, 1f);
            EditorGUILayout.BeginVertical(GUILayout.Width(50f));
            for (int i = 0; i < m_SystemContent.Length; i++)
            {
                GUIStyle style = (m_SystemIndex == i)
                    ? m_Skin.FindStyle("roundbuttonactive")
                    : m_Skin.FindStyle("roundbutton");
                if (GUILayout.Button(m_SystemContent[i], style, GUILayout.Width(40f), GUILayout.Height(40f)))
                {
                    m_SystemIndex = i;
                }
            }
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = previousColor;

            // Right inspector area
            EditorGUILayout.BeginVertical();
            Rect inspectorRect = new Rect(0f, 0f, position.width - 50f, position.height);
            switch (m_SystemIndex)
            {
                case 0:
                    m_InventorySystemInspector.OnGUI(inspectorRect);
                    break;
                case 1:
                    m_UtilityAIInspector.OnGUI(inspectorRect);
                    break;
                case 2:
                    m_QuestSystemInspector.OnGUI(inspectorRect);
                    break;
                case 3:
                    m_CharacterSystemInspector.OnGUI(inspectorRect);
                    break;
                case 4:
                    m_StatSystemInspector.OnGUI(inspectorRect);
                    break;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }
    }
}