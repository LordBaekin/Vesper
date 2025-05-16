using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DevionGames.UIWidgets;

public class DialogBoxTrigger : MonoBehaviour
{
    public string title;

    [TextArea]
    public string text;

    public Sprite icon;
    public string[] options;

    private DialogBox m_DialogBox;

    private void Start()
    {
#if UNITY_2023_1_OR_NEWER
        this.m_DialogBox = UnityEngine.Object.FindFirstObjectByType<DialogBox>();
#else
        this.m_DialogBox = UnityEngine.Object.FindObjectOfType<DialogBox>();
#endif

        if (m_DialogBox == null)
        {
            Debug.LogWarning("⚠️ No DialogBox found in the scene. Please add one to use DialogBoxTrigger.");
        }
    }

    public void Show()
    {
        if (m_DialogBox != null)
        {
            m_DialogBox.Show(title, text, icon, null, options);
        }
    }

    public void ShowWithCallback()
    {
        if (m_DialogBox != null)
        {
            m_DialogBox.Show(title, text, icon, OnDialogResult, options);
        }
    }

    private void OnDialogResult(int index)
    {
        if (m_DialogBox != null)
        {
            m_DialogBox.Show("Result", "Callback Result: " + options[index], icon, null, "OK");
        }
    }
}
