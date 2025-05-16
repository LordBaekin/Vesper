using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;
using DevionGames.StatSystem.Configuration;
using UnityEngine.Networking;
using System.Text;
using System;

namespace DevionGames.StatSystem
{
    public class StatsManager : MonoBehaviour
    {
        private static StatsManager m_Current;

        /// <summary>
        /// The StatManager singleton object. This object is set inside Awake()
        /// </summary>
        public static StatsManager current
        {
            get
            {
                Assert.IsNotNull(m_Current, "Requires a Stats Manager. Create one from Tools > Devion Games > Stat System > Create Stats Manager!");
                return m_Current;
            }
        }

        // Server API endpoints for stats persistence
        private const string STATS_ENDPOINT = "stats";

        // Base URL for API endpoints - will be initialized from PlayerPrefs
        private static string serverBaseUrl = "http://localhost:5000/";

        // Track if we're using server-based persistence
        private bool useServerPersistence = false;

        [SerializeField]
        private StatDatabase m_Database = null;

        /// <summary>
        /// Gets the item database. Configurate it inside the editor.
        /// </summary>
        /// <value>The database.</value>
        public static StatDatabase Database
        {
            get
            {
                if (StatsManager.current != null)
                {
                    Assert.IsNotNull(StatsManager.current.m_Database, "Please assign StatDatabase to the Stats Manager!");
                    return StatsManager.current.m_Database;
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
            if (StatsManager.Database != null)
            {
                return (T)StatsManager.Database.settings.Where(x => x.GetType() == typeof(T)).FirstOrDefault();
            }
            return default;
        }

        /// <summary>
        /// Don't destroy this object instance when loading new scenes.
        /// </summary>
        public bool dontDestroyOnLoad = true;

        private List<StatsHandler> m_StatsHandler;

        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        private void Awake()
        {
            if (StatsManager.m_Current != null)
            {
                // Debug.Log("Multiple Stat Manager in scene...this is not supported. Destroying instance!");
                Destroy(gameObject);
                return;
            }
            else
            {
                StatsManager.m_Current = this;
                if (dontDestroyOnLoad)
                {
                    if (transform.parent != null)
                    {
                        if (StatsManager.DefaultSettings.debugMessages)
                            Debug.Log("Stats Manager with DontDestroyOnLoad can't be a child transform. Unparent!");
                        transform.parent = null;
                    }
                    DontDestroyOnLoad(gameObject);
                }

                this.m_StatsHandler = new List<StatsHandler>();

                // Try to get server base URL from PlayerPrefs (set by LoginManager)
                string savedServerUrl = PlayerPrefs.GetString("ServerBaseUrl", null);
                if (!string.IsNullOrEmpty(savedServerUrl))
                {
                    serverBaseUrl = savedServerUrl;
                }

                // Check if we have a JWT token - if so, use server persistence
                string token = GetAuthToken();
                useServerPersistence = !string.IsNullOrEmpty(token);

                if (StatsManager.DefaultSettings.debugMessages)
                    Debug.Log("Stats Manager initialized. Using server persistence: " + useServerPersistence);

                // Listen for auth events
                EventHandler.Register("OnSessionExpired", OnSessionExpired);

                if (StatsManager.SavingLoading.autoSave)
                {
                    StartCoroutine(RepeatSaving(StatsManager.SavingLoading.savingRate));
                }
            }
        }

        private void OnDestroy()
        {
            EventHandler.Unregister("OnSessionExpired", OnSessionExpired);
        }

        private void OnSessionExpired()
        {
            useServerPersistence = false;
            Debug.Log("Session expired. Switching to local storage for stats data.");
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
        private static IEnumerator HandleAuthError(UnityWebRequest www, System.Action retryAction)
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

        private void Start()
        {
            if (StatsManager.SavingLoading.autoSave)
            {
                StartCoroutine(DelayedLoading(1f));
            }
        }

        public static void Save()
        {
            string key = PlayerPrefs.GetString(StatsManager.SavingLoading.savingKey, StatsManager.SavingLoading.savingKey);
            Save(key);
        }

        public static void Save(string key)
        {
            // Check if we should use server persistence
            if (StatsManager.current != null && StatsManager.current.useServerPersistence)
            {
                StatsManager.current.StartCoroutine(SaveToServer(key));
                return;
            }

            // Top-level save call
            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log("[StatsManager] Save() called");

            // Find all saveable handlers
            // Unity 2023.2+ replacement for FindObjectsOfType
            StatsHandler[] results = FindStatsHandlers();

            if (results.Length == 0)
            {
                if (StatsManager.DefaultSettings.debugMessages)
                    Debug.Log("[StatsManager] No saveable handlers found. Aborting Save().");
                return;
            }

            // JSON-serialize entire handlers list
            string data = JsonSerializer.Serialize(results);
            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log($"[StatsManager] Serialized JSON: {data}");

            // Write out each handler and each stat
            foreach (StatsHandler handler in results)
            {
                if (StatsManager.DefaultSettings.debugMessages)
                    Debug.Log($"[StatsManager]   → Saving handler '{handler.HandlerName}'");

                foreach (Stat stat in handler.m_Stats)
                {
                    // Always save the final Value
                    float statValue = stat.Value;
                    PlayerPrefs.SetFloat($"{key}.Stats.{handler.HandlerName}.{stat.Name}.Value", statValue);
                    if (StatsManager.DefaultSettings.debugMessages)
                        Debug.Log($"[StatsManager]      • {stat.Name}.Value = {statValue}");

                    // If it's an Attribute, also save CurrentValue
                    if (stat is Attribute attribute)
                    {
                        float cv = attribute.CurrentValue;
                        PlayerPrefs.SetFloat($"{key}.Stats.{handler.HandlerName}.{stat.Name}.CurrentValue", cv);
                        if (StatsManager.DefaultSettings.debugMessages)
                            Debug.Log($"[StatsManager]      • {stat.Name}.CurrentValue = {cv}");
                    }
                }
            }

            // Finally, save the JSON blob and update key list
            PlayerPrefs.SetString($"{key}.Stats", data);
            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log($"[StatsManager] JSON stored under key '{key}.Stats'");

            // Maintain the saved-keys registry
            List<string> keys = PlayerPrefs.GetString("StatSystemSavedKeys")
                                           .Split(';')
                                           .Where(x => !string.IsNullOrEmpty(x))
                                           .ToList();
            if (!keys.Contains(key)) keys.Add(key);
            PlayerPrefs.SetString("StatSystemSavedKeys", string.Join(";", keys));

            PlayerPrefs.Save();
            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log("[StatsManager] Save() complete");
        }

        // Helper method to find all saveable StatsHandlers
        private static StatsHandler[] FindStatsHandlers()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<StatsHandler>(
                    FindObjectsInactive.Include,      // include disabled handlers too
                    FindObjectsSortMode.None          // no sorting overhead
                )
                .Where(x => x.saveable)
                .ToArray();
#else
            return UnityEngine.Object.FindObjectsOfType<StatsHandler>(true)
                .Where(x => x.saveable)
                .ToArray();
#endif
        }

        // Server-based stats saving
        private static IEnumerator SaveToServer(string key)
        {
            if (GetAuthToken() == null)
            {
                Debug.LogError("Cannot save stats to server: No authentication token available");
                yield break;
            }

            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log("[StatsManager] SaveToServer() called");

            // Find all saveable handlers
            StatsHandler[] results = FindStatsHandlers();

            if (results.Length == 0)
            {
                if (StatsManager.DefaultSettings.debugMessages)
                    Debug.Log("[StatsManager] No saveable handlers found. Aborting SaveToServer().");
                yield break;
            }

            // JSON-serialize entire handlers list
            string data = JsonSerializer.Serialize(results);

            // Create individual stat values 
            Dictionary<string, float> statValues = new Dictionary<string, float>();
            Dictionary<string, float> attributeCurrentValues = new Dictionary<string, float>();

            foreach (StatsHandler handler in results)
            {
                foreach (Stat stat in handler.m_Stats)
                {
                    // Always save the final Value
                    string statKey = $"{handler.HandlerName}.{stat.Name}.Value";
                    statValues[statKey] = stat.Value;

                    // If it's an Attribute, also save CurrentValue
                    if (stat is Attribute attribute)
                    {
                        string attributeKey = $"{handler.HandlerName}.{stat.Name}.CurrentValue";
                        attributeCurrentValues[attributeKey] = attribute.CurrentValue;
                    }
                }
            }

            // Create the JSON data to send
            var payload = JsonUtility.ToJson(new StatsData
            {
                key = key,
                stats_json = data,
                stat_values = statValues,
                attribute_values = attributeCurrentValues
            });

            // Prepare the API endpoint
            string uri = $"{serverBaseUrl.TrimEnd('/')}/{STATS_ENDPOINT}";

            using (var www = new UnityWebRequest(uri, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
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
                    Debug.LogError($"Failed to save stats to server: {www.error}");

                    // Handle authentication errors
                    if (www.responseCode == 401)
                    {
                        yield return StatsManager.current.StartCoroutine(
                            HandleAuthError(www, () => Save(key))
                        );
                    }

                    // Fall back to local storage
                    Debug.Log("Falling back to local storage for stats...");

                    // Save to PlayerPrefs as in the original code
                    PlayerPrefs.SetString($"{key}.Stats", data);

                    foreach (var kvp in statValues)
                    {
                        PlayerPrefs.SetFloat($"{key}.Stats.{kvp.Key}", kvp.Value);
                    }

                    foreach (var kvp in attributeCurrentValues)
                    {
                        PlayerPrefs.SetFloat($"{key}.Stats.{kvp.Key}", kvp.Value);
                    }

                    PlayerPrefs.Save();
                    yield break;
                }

                if (StatsManager.DefaultSettings.debugMessages)
                {
                    Debug.Log("[StatsManager] Data saved to server successfully");
                }
            }
        }

        [Serializable]
        private class StatsData
        {
            public string key;
            public string stats_json;

            // These need to be serialized as individual fields in the class
            // since Unity's JsonUtility doesn't handle dictionaries directly
            [NonSerialized]
            public Dictionary<string, float> stat_values;

            [NonSerialized]
            public Dictionary<string, float> attribute_values;

            // We'll convert these to JSON strings before sending
            public string stat_values_json;
            public string attribute_values_json;

            public StatsData()
            {
                // Convert dictionaries to JSON using MiniJSON
                if (stat_values != null)
                    stat_values_json = MiniJSON.Serialize(stat_values);

                if (attribute_values != null)
                    attribute_values_json = MiniJSON.Serialize(attribute_values);
            }
        }

        public static void Load()
        {
            string key = PlayerPrefs.GetString(StatsManager.SavingLoading.savingKey, StatsManager.SavingLoading.savingKey);
            Load(key);
        }

        public static void Load(string key)
        {
            // Check if we should use server persistence
            if (StatsManager.current != null && StatsManager.current.useServerPersistence)
            {
                StatsManager.current.StartCoroutine(LoadFromServer(key));
                return;
            }

            LoadFromLocalStorage(key);
        }

        private static void LoadFromLocalStorage(string key)
        {
            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log("[StatsManager] Load() called");

            // Read the JSON blob
            string data = PlayerPrefs.GetString($"{key}.Stats");
            if (string.IsNullOrEmpty(data))
            {
                if (StatsManager.DefaultSettings.debugMessages)
                    Debug.Log($"[StatsManager] No JSON found under key '{key}.Stats', aborting Load().");
                return;
            }
            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log($"[StatsManager] JSON loaded: {data}");

            // Find all active saveable handlers
            List<StatsHandler> results = FindStatsHandlers().ToList();

            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log($"[StatsManager] → Found {results.Count} saveable handlers in scene");

            // Deserialize and apply
            List<object> list = MiniJSON.Deserialize(data) as List<object>;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var handlerData = list[i] as Dictionary<string, object>;
                    if (handlerData == null) continue;

                    string handlerName = handlerData.ContainsKey("Name") ? handlerData["Name"] as string : null;
                    if (string.IsNullOrEmpty(handlerName))
                    {
                        if (StatsManager.DefaultSettings.debugMessages)
                            Debug.LogWarning($"[StatsManager] Handler data missing Name field: {handlerData}");
                        continue;
                    }

                    if (StatsManager.DefaultSettings.debugMessages)
                        Debug.Log($"[StatsManager]   → Restoring handler '{handlerName}'");

                    StatsHandler handler = results.Find(x => x.HandlerName == handlerName);
                    if (handler != null)
                    {
                        handler.SetObjectData(handlerData);
                    }
                    else if (StatsManager.DefaultSettings.debugMessages)
                    {
                        Debug.LogWarning($"[StatsManager]   ✗ No handler with Name '{handlerName}' found in scene");
                    }
                }
            }
            else if (StatsManager.DefaultSettings.debugMessages)
            {
                Debug.LogWarning($"[StatsManager] Failed to deserialize JSON: {data}");
            }

            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log("[StatsManager] Load() complete");
        }

        // Server-based stats loading
        private static IEnumerator LoadFromServer(string key)
        {
            if (GetAuthToken() == null)
            {
                Debug.LogError("Cannot load stats from server: No authentication token available");
                yield break;
            }

            // Prepare the API endpoint
            string uri = $"{serverBaseUrl.TrimEnd('/')}/{STATS_ENDPOINT}/{key}";

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
                    // 404 is expected if we don't have saved data yet
                    if (www.responseCode != 404)
                    {
                        Debug.LogError($"Failed to load stats from server: {www.error}");

                        // Handle authentication errors
                        if (www.responseCode == 401)
                        {
                            yield return StatsManager.current.StartCoroutine(
                                HandleAuthError(www, () => Load(key))
                            );
                        }
                    }

                    // Fall back to local storage
                    Debug.Log("Falling back to local storage for stats...");
                    LoadFromLocalStorage(key);
                    yield break;
                }

                // Parse the response
                try
                {
                    var responseText = www.downloadHandler.text;
                    if (string.IsNullOrEmpty(responseText))
                    {
                        Debug.LogWarning("[StatsManager] Empty response from server");
                        LoadFromLocalStorage(key);
                        yield break;
                    }

                    var response = JsonUtility.FromJson<StatsData>(responseText);
                    if (response == null || string.IsNullOrEmpty(response.stats_json))
                    {
                        Debug.LogWarning("[StatsManager] Invalid response format from server");
                        LoadFromLocalStorage(key);
                        yield break;
                    }

                    // Find all active saveable handlers
                    List<StatsHandler> results = FindStatsHandlers().ToList();

                    if (StatsManager.DefaultSettings.debugMessages)
                        Debug.Log($"[StatsManager] → Found {results.Count} saveable handlers in scene");

                    // Deserialize and apply
                    List<object> list = MiniJSON.Deserialize(response.stats_json) as List<object>;
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var handlerData = list[i] as Dictionary<string, object>;
                            if (handlerData == null) continue;

                            string handlerName = handlerData.ContainsKey("Name") ? handlerData["Name"] as string : null;
                            if (string.IsNullOrEmpty(handlerName))
                            {
                                if (StatsManager.DefaultSettings.debugMessages)
                                    Debug.LogWarning($"[StatsManager] Handler data missing Name field: {handlerData}");
                                continue;
                            }

                            if (StatsManager.DefaultSettings.debugMessages)
                                Debug.Log($"[StatsManager]   → Restoring handler '{handlerName}'");

                            StatsHandler handler = results.Find(x => x.HandlerName == handlerName);
                            if (handler != null)
                            {
                                handler.SetObjectData(handlerData);
                            }
                            else if (StatsManager.DefaultSettings.debugMessages)
                            {
                                Debug.LogWarning($"[StatsManager]   ✗ No handler with Name '{handlerName}' found in scene");
                            }
                        }
                    }

                    if (StatsManager.DefaultSettings.debugMessages)
                    {
                        Debug.Log("[StatsManager] Data loaded from server successfully");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse stats data from server: {ex.Message}");

                    // Fall back to local storage
                    LoadFromLocalStorage(key);
                }
            }
        }

        private IEnumerator DelayedLoading(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            try
            {
                Load();
                if (DefaultSettings.debugMessages)
                    Debug.Log("[StatsManager] Initial Load() completed without errors");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[StatsManager] suppressed exception during initial Load(): {e.Message}");
            }
        }

        private IEnumerator RepeatSaving(float seconds)
        {
            while (true)
            {
                yield return new WaitForSeconds(seconds);
                Save();
            }
        }

        public static void RegisterStatsHandler(StatsHandler handler)
        {
            if (!StatsManager.current.m_StatsHandler.Contains(handler))
            {
                StatsManager.current.m_StatsHandler.Add(handler);
            }
        }

        public static StatsHandler GetStatsHandler(string name)
        {
            return StatsManager.current.m_StatsHandler.Find(x => x.HandlerName == name);
        }
    }
}