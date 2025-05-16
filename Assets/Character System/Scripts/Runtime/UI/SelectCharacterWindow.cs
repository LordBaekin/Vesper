// File: SelectCharacterWindow.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using DevionGames.CharacterSystem;
using DevionGames;

/// <summary>
/// Drives the Character Selection UI: populates with available characters,
/// allows selection, and emits an event when the player confirms.
/// </summary>
public class SelectCharacterWindow : CharacterWindow
{
    public override string[] Callbacks => new List<string>(base.Callbacks) { "OnCharacterLoaded", "OnCharacterDeleted" }.ToArray();

    [Header("Buttons Reference")]
    [SerializeField] private Button m_PlayButton;
    [SerializeField] private Button m_CreateButton;
    [SerializeField] private Button m_DeleteButton;

    [Header("Events")]
    [Tooltip("Invoked with the confirmed character name when 'Play' is clicked")]
    public UnityEvent<string> onCharacterConfirmed;

    void Awake()
    {
        if (m_PlayButton == null || m_CreateButton == null || m_DeleteButton == null)
            Debug.LogError("SelectCharacterWindow: Buttons not assigned in inspector.");
    }

    void Start()
    {
        m_PlayButton.onClick.AddListener(OnPlayClicked);
        m_CreateButton.onClick.AddListener(OnCreateClicked);
        m_DeleteButton.onClick.AddListener(OnDeleteClicked);

        EventHandler.Register<Character>("OnCharacterLoaded", OnCharacterLoaded);
        EventHandler.Register<Character>("OnCharacterDeleted", OnCharacterDeleted);
        CharacterManager.LoadCharacters();
    }

    new protected void OnDestroy()
    {
        m_PlayButton.onClick.RemoveListener(OnPlayClicked);
        m_CreateButton.onClick.RemoveListener(OnCreateClicked);
        m_DeleteButton.onClick.RemoveListener(OnDeleteClicked);
        EventHandler.Unregister<Character>("OnCharacterLoaded", OnCharacterLoaded);
        EventHandler.Unregister<Character>("OnCharacterDeleted", OnCharacterDeleted);
    }

    public override void OnSelectionChange(Character character)
    {
        base.OnSelectionChange(character);
        bool has = character != null;
        m_PlayButton.gameObject.SetActive(has);
        m_DeleteButton.gameObject.SetActive(has);
        if (CharacterManager.DefaultSettings.debugMessages && character != null)
            Debug.Log($"[CharacterSystem] Character selected: {character.CharacterName}");
    }

    private void OnCharacterLoaded(Character character)
    {
        var slot = Add(character);
        slot.name = character.CharacterName;
        var data = new CallbackEventData();
        data.AddData("Slot", slot);
        data.AddData("Character", character);
        data.AddData("CharacterName", character.CharacterName);
        Execute("OnCharacterLoaded", data);
        slot.BroadcastMessage("OnCharacterLoaded", data, SendMessageOptions.DontRequireReceiver);
    }

    private void OnCharacterDeleted(Character character)
    {
        Remove(character);
        var data = new CallbackEventData();
        data.AddData("Character", character);
        data.AddData("CharacterName", character.CharacterName);
        Execute("OnCharacterDeleted", data);
    }

    private void OnPlayClicked()
    {
        if (SelectedCharacter == null) return;
        string name = SelectedCharacter.CharacterName;
        Debug.Log($"[CharacterSystem] Confirming character: {name}");

        onCharacterConfirmed?.Invoke(name);

        Debug.Log("[SelectCharacterWindow] Initiating Main scene load...");
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene("Main Scene", LoadSceneMode.Single);
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SelectCharacterWindow] Scene loaded: {scene.name}, mode: {mode}");
        int camCount = Camera.allCamerasCount;
        Debug.Log($"[SelectCharacterWindow] Cameras present: {camCount}");
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnCreateClicked()
    {
        SceneManager.LoadScene(CharacterManager.DefaultSettings.createCharacterScene, LoadSceneMode.Single);
    }

    private void OnDeleteClicked()
    {
        Close();
        m_CreateButton.gameObject.SetActive(false);
        m_PlayButton.gameObject.SetActive(false);
        m_DeleteButton.gameObject.SetActive(false);
        CharacterManager.Notifications.deleteConfirmation.Show(result =>
        {
            if (result == 0)
            {
                CharacterManager.DeleteCharacter(SelectedCharacter);
                Show();
            }
            m_CreateButton.gameObject.SetActive(true);
            m_PlayButton.gameObject.SetActive(true);
            m_DeleteButton.gameObject.SetActive(true);
        }, "Yes", "Cancel");
    }

    public void ShowPanel() => Show();
}
