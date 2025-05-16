using DevionGames.CharacterSystem.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using System;
using System.Text;

namespace DevionGames.CharacterSystem
{
    public class CharacterManager : MonoBehaviour
    {
        /// <summary>
        /// Don't destroy this object instance when loading new scenes.
        /// </summary>
        public bool dontDestroyOnLoad = true;

        // Server API endpoints for character persistence
        private const string CHARACTERS_ENDPOINT = "characters";
        private const string CHARACTER_ENDPOINT = "characters/{0}"; // {0} will be replaced with character name

        // Base URL for API endpoints - will be initialized from configuration or default
        private static string serverBaseUrl = "http://localhost:5000/";

        private static CharacterManager m_Current;

        /// <summary>
        /// The CharacterManager singleton object. This object is set inside Awake()
        /// </summary>
        public static CharacterManager current
        {
            get
            {
                Assert.IsNotNull(m_Current, "Requires a Character Manager.Create one from Tools > Devion Games > Character System > Create Character Manager!");
                return m_Current;
            }
        }

        [SerializeField]
        private CharacterDatabase m_Database = null;

        /// <summary>
        /// Gets the item database. Configurate it inside the editor.
        /// </summary>
        /// <value>The database.</value>
        public static CharacterDatabase Database
        {
            get
            {
                if (CharacterManager.current != null)
                {
                    Assert.IsNotNull(CharacterManager.current.m_Database, "Please assign CharacterDatabase to the Character Manager!");
                    return CharacterManager.current.m_Database;
                }
                return null;
            }
        }

        private static Default m_DefaultSettings;
        public static Default DefaultSettings
        {
            get
            {
                if (m_DefaultSettings == null)
                {
                    m_DefaultSettings = GetSetting<Default>();
                }
                return m_DefaultSettings;
            }
        }

        private static UI m_UI;
        public static UI UI
        {
            get
            {
                if (m_UI == null)
                {
                    m_UI = GetSetting<UI>();
                }
                return m_UI;
            }
        }

        private static Notifications m_Notifications;
        public static Notifications Notifications
        {
            get
            {
                if (m_Notifications == null)
                {
                    m_Notifications = GetSetting<Notifications>();
                }
                return m_Notifications;
            }
        }

        private static SavingLoading m_SavingLoading;
        public static SavingLoading SavingLoading
        {
            get
            {
                if (m_SavingLoading == null)
                {
                    m_SavingLoading = GetSetting<SavingLoading>();
                }
                return m_SavingLoading;
            }
        }

        private static T GetSetting<T>() where T : Configuration.Settings
        {
            if (CharacterManager.Database != null)
            {
                return (T)CharacterManager.Database.settings.Where(x => x.GetType() == typeof(T)).FirstOrDefault();
            }
            return default(T);
        }

        private Character m_SelectedCharacter;
        public Character SelectedCharacter
        {
            get { return this.m_SelectedCharacter; }
        }

        // Track if we're using server-based persistence
        private bool useServerPersistence = false;

        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        private void Awake()
        {
            if (CharacterManager.m_Current != null)
            {
                // Debug.Log("Multiple Character Manager in scene...this is not supported. Destroying instance!");
                Destroy(gameObject);
                return;
            }
            else
            {
                CharacterManager.m_Current = this;
                if (dontDestroyOnLoad)
                {
                    if (transform.parent != null)
                    {
                        if (CharacterManager.DefaultSettings.debugMessages)
                            Debug.Log("Character Manager with DontDestroyOnLoad can't be a child transform. Unparent!");
                        transform.parent = null;
                    }
                    DontDestroyOnLoad(gameObject);
                }

                // Try to get server base URL from PlayerPrefs (set by LoginManager)
                string savedServerUrl = PlayerPrefs.GetString("ServerBaseUrl", null);
                if (!string.IsNullOrEmpty(savedServerUrl))
                {
                    serverBaseUrl = savedServerUrl;
                }

                // Check if we have a JWT token - if so, use server persistence
                string token = GetAuthToken();
                useServerPersistence = !string.IsNullOrEmpty(token);

                Debug.Log("Character Manager initialized. Using server persistence: " + useServerPersistence);

                // Listen for auth events
                EventHandler.Register("OnSessionExpired", OnSessionExpired);
            }
        }

        private void OnDestroy()
        {
            EventHandler.Unregister("OnSessionExpired", OnSessionExpired);
        }

        private void OnSessionExpired()
        {
            useServerPersistence = false;
            Debug.Log("Session expired. Switching to local storage for character data.");
        }

        // Helper method to get JWT token from PlayerPrefs
        private static string GetAuthToken()
        {
            return PlayerPrefs.GetString("jwt_token", null);
        }

        // Helper method to set auth header for requests
        private static void SetAuthHeader(UnityWebRequest request)
        {
            string token = GetAuthToken();
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }
        }

        // Helper method to handle HTTP errors with token refresh
        private static IEnumerator HandleAuthError(UnityWebRequest www, Action retryAction)
        {
            if (www.responseCode == 401) // Unauthorized - token expired
            {
                // We can't call LoginManager directly due to assembly references,
                // so we'll fire an event that LoginManager can listen for
                EventHandler.Execute("OnAuthTokenExpired");

                // Wait a bit to see if LoginManager refreshes the token
                float timer = 0f;
                float timeout = 5f;
                bool tokenRefreshed = false;

                while (timer < timeout && !tokenRefreshed)
                {
                    // Check if a new token is available
                    if (PlayerPrefs.GetString("jwt_token_refreshed", "false") == "true")
                    {
                        // Clear the refresh flag
                        PlayerPrefs.SetString("jwt_token_refreshed", "false");
                        tokenRefreshed = true;
                    }

                    timer += Time.deltaTime;
                    yield return null;
                }

                if (tokenRefreshed && retryAction != null)
                {
                    // Token was refreshed, retry the original action
                    retryAction();
                }
                else
                {
                    // Token refresh failed, notify session expired
                    EventHandler.Execute("OnSessionExpired");
                }
            }
        }

        public static void StartPlayScene(Character selected)
        {
            CharacterManager.current.m_SelectedCharacter = selected;
            PlayerPrefs.SetString("Player", selected.CharacterName);
            PlayerPrefs.SetString("Profession", selected.Name);
            string scene = selected.FindProperty("Scene").stringValue;
            if (string.IsNullOrEmpty(scene))
            {
                scene = CharacterManager.DefaultSettings.playScene;
            }
            if (CharacterManager.DefaultSettings.debugMessages)
                Debug.Log("[Character System] Loading scene " + scene + " for " + selected.CharacterName);
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += ChangedActiveScene;
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }

        private static void ChangedActiveScene(UnityEngine.SceneManagement.Scene current, UnityEngine.SceneManagement.Scene next)
        {
            Vector3 position = CharacterManager.current.m_SelectedCharacter.FindProperty("Spawnpoint").vector3Value;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            //Player already in scene
            if (player != null)
            {
                //Is it the player prefab we selected?
                if (player.name == CharacterManager.current.m_SelectedCharacter.Prefab.name)
                {
                    return;
                }
                DestroyImmediate(player);
            }

            player = GameObject.Instantiate(CharacterManager.current.m_SelectedCharacter.Prefab, position, Quaternion.identity);
            player.name = player.name.Replace("(Clone)", "").Trim();
        }

        public static void CreateCharacter(Character character)
        {
            // Check if we should use server persistence
            if (CharacterManager.current.useServerPersistence)
            {
                CharacterManager.current.StartCoroutine(CreateCharacterOnServer(character));
                return;
            }

            // Fallback to local storage if needed
            string key = PlayerPrefs.GetString(CharacterManager.SavingLoading.accountKey);
            string serializedCharacterData = PlayerPrefs.GetString(key);
            List<Character> list = JsonSerializer.Deserialize<Character>(serializedCharacterData);

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].CharacterName == character.CharacterName)
                {
                    EventHandler.Execute("OnFailedToCreateCharacter", character);
                    return;
                }
            }

            list.Add(character);
            string data = JsonSerializer.Serialize(list.ToArray());
            PlayerPrefs.SetString(key, data);
            EventHandler.Execute("OnCharacterCreated", character);
        }

        // Server-based character creation
        private static IEnumerator CreateCharacterOnServer(Character character)
        {
            if (GetAuthToken() == null)
            {
                Debug.LogError("Cannot create character on server: No authentication token available");
                EventHandler.Execute("OnFailedToCreateCharacter", character);
                yield break;
            }

            // Prepare the API endpoint
            string uri = $"{serverBaseUrl.TrimEnd('/')}/{CHARACTERS_ENDPOINT}";

            // Serialize character to JSON
            string jsonData = JsonSerializer.Serialize(character);

            using (var www = new UnityWebRequest(uri, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                SetAuthHeader(www);

                yield return www.SendWebRequest();

                // Check for errors using Unity version-appropriate method
                bool isError = false;
#if UNITY_2020_1_OR_NEWER
                isError = www.result != UnityWebRequest.Result.Success;
#else
                isError = www.isNetworkError || www.isHttpError;
#endif

                if (isError)
                {
                    Debug.LogError($"Failed to create character on server: {www.error}");

                    // Handle authentication errors
                    if (www.responseCode == 401)
                    {
                        yield return CharacterManager.current.StartCoroutine(
                            HandleAuthError(www, () => CreateCharacter(character))
                        );
                        yield break;
                    }

                    // If character name already exists or other error
                    EventHandler.Execute("OnFailedToCreateCharacter", character);
                    yield break;
                }

                // Success!
                EventHandler.Execute("OnCharacterCreated", character);
            }
        }

        public static void LoadCharacters()
        {
            // Check if we should use server persistence
            if (CharacterManager.current.useServerPersistence)
            {
                CharacterManager.current.StartCoroutine(LoadCharactersFromServer());
                return;
            }

            // Fallback to local storage if needed
            string key = PlayerPrefs.GetString(CharacterManager.SavingLoading.accountKey);

            string data = PlayerPrefs.GetString(key);
            if (string.IsNullOrEmpty(data)) return;

            List<object> l = MiniJSON.Deserialize(data) as List<object>;
            for (int i = 0; i < l.Count; i++)
            {
                Dictionary<string, object> characterData = l[i] as Dictionary<string, object>;
                EventHandler.Execute("OnCharacterDataLoaded", characterData);
            }
            List<Character> list = JsonSerializer.Deserialize<Character>(data);
            for (int i = 0; i < list.Count; i++)
            {
                Character character = list[i];
                EventHandler.Execute("OnCharacterLoaded", character);
            }
        }

        // Server-based character loading
        private static IEnumerator LoadCharactersFromServer()
        {
            if (GetAuthToken() == null)
            {
                Debug.LogError("Cannot load characters from server: No authentication token available");
                yield break;
            }

            // Prepare the API endpoint
            string uri = $"{serverBaseUrl.TrimEnd('/')}/{CHARACTERS_ENDPOINT}";

            using (var www = UnityWebRequest.Get(uri))
            {
                SetAuthHeader(www);

                yield return www.SendWebRequest();

                // Check for errors using Unity version-appropriate method
                bool isError = false;
#if UNITY_2020_1_OR_NEWER
                isError = www.result != UnityWebRequest.Result.Success;
#else
                isError = www.isNetworkError || www.isHttpError;
#endif

                if (isError)
                {
                    Debug.LogError($"Failed to load characters from server: {www.error}");

                    // Handle authentication errors
                    if (www.responseCode == 401)
                    {
                        yield return CharacterManager.current.StartCoroutine(
                            HandleAuthError(www, () => LoadCharacters())
                        );
                    }

                    yield break;
                }

                // Parse the response
                string data = www.downloadHandler.text;

                if (string.IsNullOrEmpty(data))
                {
                    Debug.Log("No characters found on server");
                    yield break;
                }

                // Process the character data
                List<object> l = MiniJSON.Deserialize(data) as List<object>;
                for (int i = 0; i < l.Count; i++)
                {
                    Dictionary<string, object> characterData = l[i] as Dictionary<string, object>;
                    EventHandler.Execute("OnCharacterDataLoaded", characterData);
                }

                List<Character> list = JsonSerializer.Deserialize<Character>(data);
                for (int i = 0; i < list.Count; i++)
                {
                    Character character = list[i];
                    EventHandler.Execute("OnCharacterLoaded", character);
                }
            }
        }

        public static void DeleteCharacter(Character character)
        {
            // Check if we should use server persistence
            if (CharacterManager.current.useServerPersistence)
            {
                CharacterManager.current.StartCoroutine(DeleteCharacterOnServer(character));
                return;
            }

            // Fallback to local storage if needed
            string key = PlayerPrefs.GetString(CharacterManager.SavingLoading.accountKey);

            string serializedCharacterData = PlayerPrefs.GetString(key);
            List<Character> list = JsonSerializer.Deserialize<Character>(serializedCharacterData);

            string data = JsonSerializer.Serialize(list.Where(x => x.CharacterName != character.CharacterName).ToArray());
            PlayerPrefs.SetString(key, data);

            DeleteInventorySystemForCharacter(character.CharacterName);
            DeleteStatSystemForCharacter(character.CharacterName);
            EventHandler.Execute("OnCharacterDeleted", character);
        }

        // Server-based character deletion
        private static IEnumerator DeleteCharacterOnServer(Character character)
        {
            if (GetAuthToken() == null)
            {
                Debug.LogError("Cannot delete character on server: No authentication token available");
                yield break;
            }

            // Prepare the API endpoint
            string uri = $"{serverBaseUrl.TrimEnd('/')}/{string.Format(CHARACTER_ENDPOINT, character.CharacterName)}";

            using (var www = UnityWebRequest.Delete(uri))
            {
                SetAuthHeader(www);

                yield return www.SendWebRequest();

                // Check for errors using Unity version-appropriate method
                bool isError = false;
#if UNITY_2020_1_OR_NEWER
                isError = www.result != UnityWebRequest.Result.Success;
#else
                isError = www.isNetworkError || www.isHttpError;
#endif

                if (isError)
                {
                    Debug.LogError($"Failed to delete character from server: {www.error}");

                    // Handle authentication errors
                    if (www.responseCode == 401)
                    {
                        yield return CharacterManager.current.StartCoroutine(
                            HandleAuthError(www, () => DeleteCharacter(character))
                        );
                    }

                    yield break;
                }

                // Clean up local data for the character
                DeleteInventorySystemForCharacter(character.CharacterName);
                DeleteStatSystemForCharacter(character.CharacterName);

                // Notify that character was deleted
                EventHandler.Execute("OnCharacterDeleted", character);
            }
        }

        private static void DeleteInventorySystemForCharacter(string character)
        {
            PlayerPrefs.DeleteKey(character + ".UI");
            List<string> scenes = PlayerPrefs.GetString(character + ".Scenes").Split(';').ToList();
            scenes.RemoveAll(x => string.IsNullOrEmpty(x));
            for (int i = 0; i < scenes.Count; i++)
            {
                PlayerPrefs.DeleteKey(character + "." + scenes[i]);
            }
            PlayerPrefs.DeleteKey(character + ".Scenes");
        }

        private static void DeleteStatSystemForCharacter(string character)
        {
            PlayerPrefs.DeleteKey(character + ".Stats");
            List<string> keys = PlayerPrefs.GetString("StatSystemSavedKeys").Split(';').ToList();
            keys.RemoveAll(x => string.IsNullOrEmpty(x));
            List<string> allKeys = new List<string>(keys);
            allKeys.Remove(character);
            PlayerPrefs.SetString("StatSystemSavedKeys", string.Join(";", allKeys));
        }
    }
}