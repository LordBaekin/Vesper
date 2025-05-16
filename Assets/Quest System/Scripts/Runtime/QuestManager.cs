using DevionGames.QuestSystem.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using System.Text;

namespace DevionGames.QuestSystem
{
    public class QuestManager : MonoBehaviour
    {
        /// <summary>
        /// Don't destroy this object instance when loading new scenes.
        /// </summary>
        public bool dontDestroyOnLoad = true;

        // Server API endpoints for quest persistence
        private const string QUEST_ENDPOINT = "quests";
        private const string ACTIVE_QUESTS_ENDPOINT = "quests/active";
        private const string COMPLETED_QUESTS_ENDPOINT = "quests/completed";
        private const string FAILED_QUESTS_ENDPOINT = "quests/failed";

        // Base URL for API endpoints - will be initialized from PlayerPrefs
        private static string serverBaseUrl = "http://localhost:5000/";

        public event Quest.StatusChanged OnQuestStatusChanged;
        public event Quest.TaskStatusChanged OnTaskStatusChanged;
        public event Quest.TaskProgressChanged OnTaskProgressChanged;
        public event Quest.TaskTimerTick OnTaskTimerTick;

        private static QuestManager m_Current;

        /// <summary>
        /// The QuestManager singleton object. This object is set inside Awake()
        /// </summary>
        public static QuestManager current
        {
            get
            {
                Assert.IsNotNull(m_Current, "Requires a Quest Manager. Create one from Tools > Devion Games > Quest System > Create Quest Manager!");
                return m_Current;
            }
        }

        [SerializeField]
        private QuestDatabase m_Database = null;

        /// <summary>
        /// Gets the quest database. Configurate it inside the editor.
        /// </summary>
        /// <value>The database.</value>
        public static QuestDatabase Database
        {
            get
            {
                if (QuestManager.current != null)
                {
                    Assert.IsNotNull(QuestManager.current.m_Database, "Please assign QuestDatabase to the Quest Manager!");
                    return QuestManager.current.m_Database;
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
            if (QuestManager.Database != null)
            {
                return (T)QuestManager.Database.settings.Where(x => x.GetType() == typeof(T)).FirstOrDefault();
            }
            return default;
        }

        private PlayerInfo m_PlayerInfo;
        public PlayerInfo PlayerInfo
        {
            get
            {
                if (this.m_PlayerInfo == null) { this.m_PlayerInfo = new PlayerInfo(QuestManager.DefaultSettings.playerTag); }
                return this.m_PlayerInfo;
            }
        }

        public List<Quest> AllQuests
        {
            get
            {
                List<Quest> quests = new List<Quest>();
                quests.AddRange(this.ActiveQuests);
                quests.AddRange(this.CompletedQuests);
                quests.AddRange(this.FailedQuests);
                return quests.Distinct().ToList();
            }
        }

        [HideInInspector]
        public List<Quest> ActiveQuests = new List<Quest>();
        private List<Quest> CompletedQuests = new List<Quest>();
        private List<Quest> FailedQuests = new List<Quest>();

        // Track if we're using server-based persistence
        private bool useServerPersistence = false;

        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        private void Awake()
        {
            if (QuestManager.m_Current != null)
            {
                // Debug.Log("Multiple Character Manager in scene...this is not supported. Destroying instance!");
                Destroy(gameObject);
                return;
            }
            else
            {
                QuestManager.m_Current = this;
                if (dontDestroyOnLoad)
                {
                    if (transform.parent != null)
                    {
                        if (QuestManager.DefaultSettings.debugMessages)
                            Debug.Log("Quest Manager with DontDestroyOnLoad can't be a child transform. Unparent!");
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

                if (QuestManager.DefaultSettings.debugMessages)
                    Debug.Log("Quest Manager initialized. Using server persistence: " + useServerPersistence);

                // Listen for auth events
                EventHandler.Register("OnSessionExpired", OnSessionExpired);

                if (QuestManager.SavingLoading.autoSave)
                    Load();
            }
        }

        private void OnDestroy()
        {
            EventHandler.Unregister("OnSessionExpired", OnSessionExpired);
        }

        private void OnSessionExpired()
        {
            useServerPersistence = false;
            Debug.Log("Session expired. Switching to local storage for quest data.");
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

        public static void Save()
        {
            string key = PlayerPrefs.GetString(QuestManager.SavingLoading.savingKey, QuestManager.SavingLoading.savingKey);
            Save(key);
        }

        public static void Save(string key)
        {
            // Check if we should use server persistence
            if (QuestManager.current != null && QuestManager.current.useServerPersistence)
            {
                QuestManager.current.StartCoroutine(SaveToServer(key));
                return;
            }

            // Fallback to local storage
            string activeQuestData = JsonSerializer.Serialize(QuestManager.current.ActiveQuests.ToArray());
            string completedQuestData = JsonSerializer.Serialize(QuestManager.current.CompletedQuests.ToArray());
            string failedQuestData = JsonSerializer.Serialize(QuestManager.current.FailedQuests.ToArray());

            PlayerPrefs.SetString(key + ".ActiveQuests", activeQuestData);
            PlayerPrefs.SetString(key + ".CompletedQuests", completedQuestData);
            PlayerPrefs.SetString(key + ".FailedQuests", failedQuestData);

            List<string> keys = PlayerPrefs.GetString("QuestSystemSavedKeys").Split(';').ToList();
            keys.RemoveAll(x => string.IsNullOrEmpty(x));
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
            PlayerPrefs.SetString("QuestSystemSavedKeys", string.Join(";", keys));

            if (QuestManager.DefaultSettings.debugMessages)
            {
                Debug.Log("[Quest System] Quests Saved");
            }
        }

        // Server-based quest saving
        private static IEnumerator SaveToServer(string key)
        {
            if (GetAuthToken() == null)
            {
                Debug.LogError("Cannot save quests to server: No authentication token available");
                yield break;
            }

            string activeQuestData = JsonSerializer.Serialize(QuestManager.current.ActiveQuests.ToArray());
            string completedQuestData = JsonSerializer.Serialize(QuestManager.current.CompletedQuests.ToArray());
            string failedQuestData = JsonSerializer.Serialize(QuestManager.current.FailedQuests.ToArray());

            // Create the JSON data to send
            var payload = JsonUtility.ToJson(new QuestData
            {
                key = key,
                active_quests = activeQuestData,
                completed_quests = completedQuestData,
                failed_quests = failedQuestData
            });

            // Prepare the API endpoint
            string uri = $"{serverBaseUrl.TrimEnd('/')}/{QUEST_ENDPOINT}";

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
                    Debug.LogError($"Failed to save quests to server: {www.error}");

                    // Handle authentication errors
                    if (www.responseCode == 401)
                    {
                        yield return QuestManager.current.StartCoroutine(
                            HandleAuthError(www, () => Save(key))
                        );
                    }

                    // Fall back to local storage
                    Debug.Log("Falling back to local storage for quests...");
                    PlayerPrefs.SetString(key + ".ActiveQuests", activeQuestData);
                    PlayerPrefs.SetString(key + ".CompletedQuests", completedQuestData);
                    PlayerPrefs.SetString(key + ".FailedQuests", failedQuestData);

                    yield break;
                }

                if (QuestManager.DefaultSettings.debugMessages)
                {
                    Debug.Log("[Quest System] Data saved to server successfully");
                }
            }
        }

        [Serializable]
        private class QuestData
        {
            public string key;
            public string active_quests;
            public string completed_quests;
            public string failed_quests;
        }

        public static void Load()
        {
            string key = PlayerPrefs.GetString(QuestManager.SavingLoading.savingKey, QuestManager.SavingLoading.savingKey);
            Load(key);
        }

        public static void Load(string key)
        {
            // Check if we should use server persistence
            if (QuestManager.current != null && QuestManager.current.useServerPersistence)
            {
                QuestManager.current.StartCoroutine(LoadFromServer(key));
                return;
            }

            // Fallback to local storage
            string activeQuestData = PlayerPrefs.GetString(key + ".ActiveQuests");
            string completedQuestData = PlayerPrefs.GetString(key + ".CompletedQuests");
            string failedQuestData = PlayerPrefs.GetString(key + ".FailedQuests");

            LoadQuests(activeQuestData, ref QuestManager.current.ActiveQuests, true);
            LoadQuests(completedQuestData, ref QuestManager.current.CompletedQuests);
            LoadQuests(failedQuestData, ref QuestManager.current.FailedQuests);

            if (QuestManager.DefaultSettings.debugMessages)
            {
                Debug.Log("[Quest System] Quests Loaded");
            }
        }

        // Server-based quest loading
        private static IEnumerator LoadFromServer(string key)
        {
            if (GetAuthToken() == null)
            {
                Debug.LogError("Cannot load quests from server: No authentication token available");
                yield break;
            }

            // Prepare the API endpoint
            string uri = $"{serverBaseUrl.TrimEnd('/')}/{QUEST_ENDPOINT}/{key}";

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
                        Debug.LogError($"Failed to load quests from server: {www.error}");

                        // Handle authentication errors
                        if (www.responseCode == 401)
                        {
                            yield return QuestManager.current.StartCoroutine(
                                HandleAuthError(www, () => Load(key))
                            );
                        }
                    }

                    // Fall back to local storage
                    Debug.Log("Falling back to local storage for quests...");
                    string activeQuestData = PlayerPrefs.GetString(key + ".ActiveQuests");
                    string completedQuestData = PlayerPrefs.GetString(key + ".CompletedQuests");
                    string failedQuestData = PlayerPrefs.GetString(key + ".FailedQuests");

                    LoadQuests(activeQuestData, ref QuestManager.current.ActiveQuests, true);
                    LoadQuests(completedQuestData, ref QuestManager.current.CompletedQuests);
                    LoadQuests(failedQuestData, ref QuestManager.current.FailedQuests);

                    yield break;
                }

                // Parse the response
                try
                {
                    var response = JsonUtility.FromJson<QuestData>(www.downloadHandler.text);

                    // Load the quest data
                    LoadQuests(response.active_quests, ref QuestManager.current.ActiveQuests, true);
                    LoadQuests(response.completed_quests, ref QuestManager.current.CompletedQuests);
                    LoadQuests(response.failed_quests, ref QuestManager.current.FailedQuests);

                    if (QuestManager.DefaultSettings.debugMessages)
                    {
                        Debug.Log("[Quest System] Data loaded from server successfully");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse quest data from server: {ex.Message}");

                    // Fall back to local storage
                    string activeQuestData = PlayerPrefs.GetString(key + ".ActiveQuests");
                    string completedQuestData = PlayerPrefs.GetString(key + ".CompletedQuests");
                    string failedQuestData = PlayerPrefs.GetString(key + ".FailedQuests");

                    LoadQuests(activeQuestData, ref QuestManager.current.ActiveQuests, true);
                    LoadQuests(completedQuestData, ref QuestManager.current.CompletedQuests);
                    LoadQuests(failedQuestData, ref QuestManager.current.FailedQuests);
                }
            }
        }

        private static void LoadQuests(string json, ref List<Quest> quests, bool registerCallbacks = false)
        {
            if (string.IsNullOrEmpty(json)) return;

            List<object> list = MiniJSON.Deserialize(json) as List<object>;
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                Dictionary<string, object> mData = list[i] as Dictionary<string, object>;
                if (mData == null) continue;

                string name = (string)mData["Name"];
                Quest quest = QuestManager.Database.items.FirstOrDefault(x => x.Name == name);
                if (quest != null)
                {
                    Quest instance = Instantiate(quest);
                    for (int j = 0; j < instance.tasks.Count; j++)
                    {
                        instance.tasks[j].owner = instance;
                    }
                    instance.SetObjectData(mData);
                    if (registerCallbacks)
                    {
                        instance.OnStatusChanged += QuestManager.current.NotifyQuestStatusChanged;
                        instance.OnTaskStatusChanged += QuestManager.current.NotifyTaskStatusChanged;
                        instance.OnTaskProgressChanged += QuestManager.current.NotifyTaskProgressChanged;
                        instance.OnTaskTimerTick += QuestManager.current.NotifyTaskTimerTick;
                    }
                    quests.Add(instance);
                }
                else
                {
                    Debug.LogWarning("Failed to load quest " + name + ". Quest is not present in Database.");
                }
            }

            if (QuestManager.DefaultSettings.debugMessages)
            {
                Debug.Log("[Quest System] Quests Loaded");
            }
        }

        public void AddQuest(Quest quest)
        {
            if (this.ActiveQuests.Contains(quest)) return;
            quest.OnStatusChanged += NotifyQuestStatusChanged;
            quest.OnTaskStatusChanged += NotifyTaskStatusChanged;
            quest.OnTaskProgressChanged += NotifyTaskProgressChanged;
            quest.OnTaskTimerTick += NotifyTaskTimerTick;
            this.ActiveQuests.Add(quest);

        }

        public void RemoveQuest(Quest quest)
        {
            quest.OnStatusChanged -= NotifyQuestStatusChanged;
            quest.OnTaskStatusChanged -= NotifyTaskStatusChanged;
            quest.OnTaskProgressChanged -= NotifyTaskProgressChanged;
            quest.OnTaskTimerTick -= NotifyTaskTimerTick;
            this.ActiveQuests.Remove(quest);
        }

        private void NotifyQuestStatusChanged(Quest quest)
        {
            OnQuestStatusChanged?.Invoke(quest);
            if (quest.Status == Status.Completed && !this.CompletedQuests.Contains(quest))
            {
                this.CompletedQuests.Add(quest);
                RemoveQuest(quest);

            }

            if (quest.Status == Status.Failed && !this.FailedQuests.Contains(quest))
            {
                if (quest.RestartFailed)
                {
                    quest.Reset();
                }
                else
                {
                    this.FailedQuests.Add(quest);
                    RemoveQuest(quest);
                }
            }

            if (quest.Status == Status.Canceled)
            {
                if (quest.RestartCanceled)
                {
                    RemoveQuest(quest);
                    quest.Reset();

                }
            }

            //GameState.MarkDirty();
            if (QuestManager.SavingLoading.autoSave)
                QuestManager.Save();
        }

        private void NotifyTaskStatusChanged(Quest quest, QuestTask task)
        {
            OnTaskStatusChanged?.Invoke(quest, task);
            if (QuestManager.SavingLoading.autoSave)
                QuestManager.Save();
        }

        private void NotifyTaskProgressChanged(Quest quest, QuestTask task)
        {
            OnTaskProgressChanged?.Invoke(quest, task);
            if (QuestManager.SavingLoading.autoSave)
                QuestManager.Save();
        }

        private void NotifyTaskTimerTick(Quest quest, QuestTask task)
        {
            OnTaskTimerTick?.Invoke(quest, task);
            if (QuestManager.SavingLoading.autoSave)
                QuestManager.Save();
        }

        public Quest GetQuest(string name)
        {
            return this.ActiveQuests.FirstOrDefault(x => x.Name == name);
        }

        public bool HasQuest(Quest quest, Status status)
        {
            return AllQuests.FirstOrDefault(x => x.Status == status && x.Name == quest.Name) != null;
        }

        public bool HasQuest(Quest quest, out Quest instance)
        {
            instance = AllQuests.FirstOrDefault(x => x.Name == quest.Name);
            return instance != null;
        }
    }
}