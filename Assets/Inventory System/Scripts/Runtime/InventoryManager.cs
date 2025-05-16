using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using DevionGames.UIWidgets;
using DevionGames.InventorySystem.Configuration;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using System.Text;

namespace DevionGames.InventorySystem
{
    public class InventoryManager : MonoBehaviour
    {
        /// <summary>
		/// Don't destroy this object instance when loading new scenes.
		/// </summary>
		public bool dontDestroyOnLoad = true;

        // Server API endpoints for inventory persistence
        private const string INVENTORY_ENDPOINT = "inventory";
        private const string UI_INVENTORY_ENDPOINT = "inventory/ui";
        private const string SCENE_INVENTORY_ENDPOINT = "inventory/scene";

        // Base URL for API endpoints - will be initialized from PlayerPrefs
        private static string serverBaseUrl = "http://localhost:5000/";

        private static InventoryManager m_Current;

        /// <summary>
        /// The InventoryManager singleton object. This object is set inside Awake()
        /// </summary>
        public static InventoryManager current
        {
            get
            {
                Assert.IsNotNull(m_Current, "Requires an Inventory Manager.Create one from Tools > Devion Games > Inventory System > Create Inventory Manager!");
                return m_Current;
            }
        }

        [SerializeField]
        private ItemDatabase m_Database = null;

        /// <summary>
        /// Gets the item database. Configurate it inside the editor.
        /// </summary>
        /// <value>The database.</value>
        public static ItemDatabase Database
        {
            get
            {
                if (InventoryManager.current != null)
                {
                    Assert.IsNotNull(InventoryManager.current.m_Database, "Please assign ItemDatabase to the Inventory Manager!");
                    return InventoryManager.current.m_Database;
                }
                return null;
            }
        }

        [SerializeField]
        private ItemDatabase[] m_ChildDatabases = null;

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

        private static Configuration.Input m_Input;
        public static Configuration.Input Input
        {
            get
            {
                if (m_Input == null)
                {
                    m_Input = GetSetting<Configuration.Input>();
                }
                return m_Input;
            }
        }

        private static T GetSetting<T>() where T : Configuration.Settings
        {
            if (InventoryManager.Database != null)
            {
                return (T)InventoryManager.Database.settings.Where(x => x.GetType() == typeof(T)).FirstOrDefault();
            }
            return default;
        }

        protected static Dictionary<string, GameObject> m_PrefabCache;

        private PlayerInfo m_PlayerInfo;
        public PlayerInfo PlayerInfo
        {
            get
            {
                if (this.m_PlayerInfo == null) { this.m_PlayerInfo = new PlayerInfo(InventoryManager.DefaultSettings.playerTag); }
                return this.m_PlayerInfo;
            }
        }

        [HideInInspector]
        public UnityEvent onDataLoaded;
        [HideInInspector]
        public UnityEvent onDataSaved;

        protected static bool m_IsLoaded = false;
        public static bool IsLoaded { get => m_IsLoaded; }

        // Track if we're using server-based persistence
        private bool useServerPersistence = false;

        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        private void Awake()
        {
            if (InventoryManager.m_Current != null)
            {
                //if(InventoryManager.DefaultSettings.debugMessages)
                //  Debug.Log ("Multiple Inventory Manager in scene...this is not supported. Destroying instance!");
                Destroy(gameObject);
                return;
            }
            else
            {
                InventoryManager.m_Current = this;
                if (EventSystem.current == null)
                {
                    if (InventoryManager.DefaultSettings.debugMessages)
                        Debug.Log("Missing EventSystem in scene. Auto creating!");
                    new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                }

                if (Camera.main != null && Camera.main.GetComponent<PhysicsRaycaster>() == null)
                {
                    if (InventoryManager.DefaultSettings.debugMessages)
                        Debug.Log("Missing PhysicsRaycaster on Main Camera. Auto adding!");
                    PhysicsRaycaster physicsRaycaster = Camera.main.gameObject.AddComponent<PhysicsRaycaster>();
                    physicsRaycaster.eventMask = Physics.DefaultRaycastLayers;
                }

                this.m_Database = ScriptableObject.Instantiate(this.m_Database);
                for (int i = 0; i < this.m_ChildDatabases.Length; i++)
                {
                    ItemDatabase child = this.m_ChildDatabases[i];
                    this.m_Database.Merge(child);
                }

                m_PrefabCache = new Dictionary<string, GameObject>();
                UnityEngine.SceneManagement.SceneManager.activeSceneChanged += ChangedActiveScene;

                if (dontDestroyOnLoad)
                {
                    if (transform.parent != null)
                    {
                        if (InventoryManager.DefaultSettings.debugMessages)
                            Debug.Log("Inventory Manager with DontDestroyOnLoad can't be a child transform. Unparent!");
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

                if (InventoryManager.DefaultSettings.debugMessages)
                    Debug.Log("Inventory Manager initialized. Using server persistence: " + useServerPersistence);

                // Listen for auth events
                EventHandler.Register("OnSessionExpired", OnSessionExpired);

                if (InventoryManager.SavingLoading.autoSave)
                {
                    StartCoroutine(RepeatSaving(InventoryManager.SavingLoading.savingRate));
                }

                Physics.queriesHitTriggers = InventoryManager.DefaultSettings.queriesHitTriggers;

                m_IsLoaded = !HasSavedData();
                this.onDataLoaded.AddListener(() => { m_IsLoaded = true; });
            }
        }

        private void OnDestroy()
        {
            EventHandler.Unregister("OnSessionExpired", OnSessionExpired);
        }

        private void OnSessionExpired()
        {
            useServerPersistence = false;
            Debug.Log("Session expired. Switching to local storage for inventory data.");
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
            /*if (InventoryManager.SavingLoading.autoSave){
                StartCoroutine(DelayedLoading(1f));
            }*/
        }

        private static void ChangedActiveScene(UnityEngine.SceneManagement.Scene current, UnityEngine.SceneManagement.Scene next)
        {
            if (InventoryManager.SavingLoading.autoSave)
            {
                InventoryManager.m_IsLoaded = false;
                InventoryManager.Load();
            }
        }

        //TODO move to utility
        [Obsolete("InventoryManager.GetBounds is obsolete Use UnityUtility.GetBounds")]
        public Bounds GetBounds(GameObject obj)
        {
            Bounds bounds = new Bounds();
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.enabled)
                    {
                        bounds = renderer.bounds;
                        break;
                    }
                }
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.enabled)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }
            return bounds;
        }

        private IEnumerator DelayedLoading(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Load();
        }

        private IEnumerator RepeatSaving(float seconds)
        {
            while (true)
            {
                yield return new WaitForSeconds(seconds);
                Save();
            }
        }

        public static void Save()
        {
            string key = PlayerPrefs.GetString(InventoryManager.SavingLoading.savingKey, InventoryManager.SavingLoading.savingKey);
            Save(key);
        }

        public static void Save(string key)
        {
            // Check if we should use server persistence
            if (InventoryManager.current != null && InventoryManager.current.useServerPersistence)
            {
                InventoryManager.current.StartCoroutine(SaveToServer(key));
                return;
            }

            // Fallback to local storage if needed
            string uiData = string.Empty;
            string worldData = string.Empty;
            Serialize(ref uiData, ref worldData);

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            PlayerPrefs.SetString(key + ".UI", uiData);
            PlayerPrefs.SetString(key + "." + currentScene, worldData);
            List<string> scenes = PlayerPrefs.GetString(key + ".Scenes").Split(';').ToList();
            scenes.RemoveAll(x => string.IsNullOrEmpty(x));
            if (!scenes.Contains(currentScene))
            {
                scenes.Add(currentScene);
            }
            PlayerPrefs.SetString(key + ".Scenes", string.Join(";", scenes));
            List<string> keys = PlayerPrefs.GetString("InventorySystemSavedKeys").Split(';').ToList();
            keys.RemoveAll(x => string.IsNullOrEmpty(x));
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
            PlayerPrefs.SetString("InventorySystemSavedKeys", string.Join(";", keys));


            if (InventoryManager.current != null && InventoryManager.current.onDataSaved != null)
            {
                InventoryManager.current.onDataSaved.Invoke();
            }

            if (InventoryManager.DefaultSettings.debugMessages)
            {
                Debug.Log("[Inventory System] UI Saved: " + uiData);
                Debug.Log("[Inventory System] Scene Saved: " + worldData);
            }
        }

        // Server-based inventory saving
        private static IEnumerator SaveToServer(string key)
        {
            if (GetAuthToken() == null)
            {
                Debug.LogError("Cannot save inventory to server: No authentication token available");
                yield break;
            }

            string uiData = string.Empty;
            string worldData = string.Empty;
            Serialize(ref uiData, ref worldData);

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            // Create the JSON data to send
            var payload = JsonUtility.ToJson(new
            {
                key = key,
                scene = currentScene,
                ui_data = uiData,
                scene_data = worldData
            });

            // Prepare the API endpoint
            string uri = $"{serverBaseUrl.TrimEnd('/')}/{INVENTORY_ENDPOINT}";

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
                    Debug.LogError($"Failed to save inventory to server: {www.error}");

                    // Handle authentication errors
                    if (www.responseCode == 401)
                    {
                        yield return InventoryManager.current.StartCoroutine(
                            HandleAuthError(www, () => Save(key))
                        );
                    }

                    // Fall back to local storage
                    Debug.Log("Falling back to local storage for inventory...");
                    PlayerPrefs.SetString(key + ".UI", uiData);
                    PlayerPrefs.SetString(key + "." + currentScene, worldData);

                    yield break;
                }

                // Success - trigger saved event
                if (InventoryManager.current != null && InventoryManager.current.onDataSaved != null)
                {
                    InventoryManager.current.onDataSaved.Invoke();
                }

                if (InventoryManager.DefaultSettings.debugMessages)
                {
                    Debug.Log("[Inventory System] Data saved to server successfully");
                }
            }
        }

        public static void Serialize(ref string uiData, ref string sceneData)
        {
            List<MonoBehaviour> results = new List<MonoBehaviour>();
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToList().ForEach(g => results.AddRange(g.GetComponentsInChildren<MonoBehaviour>(true)));
            //DontDestroyOnLoad GameObjects
            SingleInstance.GetInstanceObjects().ForEach(g => results.AddRange(g.GetComponentsInChildren<MonoBehaviour>(true)));

            ItemCollection[] serializables = results.OfType<ItemCollection>().Where(x => x.saveable).ToArray();

            IJsonSerializable[] ui = serializables.Where(x => x.GetComponent<ItemContainer>() != null).ToArray();
            IJsonSerializable[] world = serializables.Except(ui).ToArray();

            uiData = JsonSerializer.Serialize(ui);
            sceneData = JsonSerializer.Serialize(world);
        }

        public static void Load()
        {
            string key = PlayerPrefs.GetString(InventoryManager.SavingLoading.savingKey, InventoryManager.SavingLoading.savingKey);
            Load(key);
        }

        public static void Load(string key)
        {
            // Check if we should use server persistence
            if (InventoryManager.current != null && InventoryManager.current.useServerPersistence)
            {
                InventoryManager.current.StartCoroutine(LoadFromServer(key));
                return;
            }

            // Fallback to local storage if needed
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string uiData = PlayerPrefs.GetString(key + ".UI");
            string sceneData = PlayerPrefs.GetString(key + "." + currentScene);

            Load(uiData, sceneData);
        }

        // Server-based inventory loading
        private static IEnumerator LoadFromServer(string key)
        {
            if (GetAuthToken() == null)
            {
                Debug.LogError("Cannot load inventory from server: No authentication token available");
                yield break;
            }

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            // Prepare the API endpoint
            string uri = $"{serverBaseUrl.TrimEnd('/')}/{INVENTORY_ENDPOINT}/{key}/{currentScene}";

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
                        Debug.LogError($"Failed to load inventory from server: {www.error}");

                        // Handle authentication errors
                        if (www.responseCode == 401)
                        {
                            yield return InventoryManager.current.StartCoroutine(
                                HandleAuthError(www, () => Load(key))
                            );
                        }
                    }

                    // Fall back to local storage
                    Debug.Log("Falling back to local storage for inventory...");
                    string uiData = PlayerPrefs.GetString(key + ".UI");
                    string sceneData = PlayerPrefs.GetString(key + "." + currentScene);
                    Load(uiData, sceneData);

                    yield break;
                }

                // Parse the response
                try
                {
                    // Expected format: {"ui_data": "...", "scene_data": "..."}
                    var response = JsonUtility.FromJson<InventoryData>(www.downloadHandler.text);

                    // Load the inventory data
                    Load(response.ui_data, response.scene_data);

                    if (InventoryManager.DefaultSettings.debugMessages)
                    {
                        Debug.Log("[Inventory System] Data loaded from server successfully");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse inventory data from server: {ex.Message}");

                    // Fall back to local storage
                    string uiData = PlayerPrefs.GetString(key + ".UI");
                    string sceneData = PlayerPrefs.GetString(key + "." + currentScene);
                    Load(uiData, sceneData);
                }
            }
        }

        [Serializable]
        private class InventoryData
        {
            public string ui_data;
            public string scene_data;
        }

        public static void Load(string uiData, string sceneData)
        {
            //Load UI
            LoadUI(uiData);
            //Load Scene
            LoadScene(sceneData);

            if (InventoryManager.current != null && InventoryManager.current.onDataLoaded != null)
            {
                InventoryManager.current.onDataLoaded.Invoke();
            }
        }

        public static bool HasSavedData()
        {
            string key = PlayerPrefs.GetString(InventoryManager.SavingLoading.savingKey, InventoryManager.SavingLoading.savingKey);
            return InventoryManager.HasSavedData(key);
        }

        public static bool HasSavedData(string key)
        {
            // Check if we have server-based data
            if (InventoryManager.current != null && InventoryManager.current.useServerPersistence)
            {
                // Since this is a synchronous method, we can't do an async check
                // We'll assume we have data if we're using server persistence
                return true;
            }

            // Check local storage
            return !string.IsNullOrEmpty(PlayerPrefs.GetString(key + ".UI"));
        }

        private static void LoadUI(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            List<object> list = MiniJSON.Deserialize(json) as List<object>;
            for (int i = 0; i < list.Count; i++)
            {
                Dictionary<string, object> mData = list[i] as Dictionary<string, object>;
                string prefab = (string)mData["Prefab"];
                List<object> positionData = mData["Position"] as List<object>;
                List<object> rotationData = mData["Rotation"] as List<object>;
                string type = (string)mData["Type"];

                Vector3 position = new Vector3(System.Convert.ToSingle(positionData[0]), System.Convert.ToSingle(positionData[1]), System.Convert.ToSingle(positionData[2]));
                Quaternion rotation = Quaternion.Euler(new Vector3(System.Convert.ToSingle(rotationData[0]), System.Convert.ToSingle(rotationData[1]), System.Convert.ToSingle(rotationData[2])));
                ItemCollection itemCollection = null;
                if (type == "UI")
                {
                    UIWidget container = WidgetUtility.Find<UIWidget>(prefab);
                    if (container != null)
                    {
                        itemCollection = container.GetComponent<ItemCollection>();
                    }
                }
                if (itemCollection != null)
                {
                    itemCollection.SetObjectData(mData);
                }
            }
            if (InventoryManager.DefaultSettings.debugMessages)
            {
                Debug.Log("[Inventory System] UI Loaded: " + json);
            }
        }

        private static void LoadScene(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

#if UNITY_2023_1_OR_NEWER
            ItemCollection[] itemCollections = UnityEngine.Object
                .FindObjectsByType<ItemCollection>(FindObjectsSortMode.None)
                .Where(x => x.saveable)
                .ToArray();
#else
            ItemCollection[] itemCollections = UnityEngine.Object
                .FindObjectsOfType<ItemCollection>()
                .Where(x => x.saveable)
                .ToArray();
#endif

            for (int i = 0; i < itemCollections.Length; i++)
            {
                ItemCollection collection = itemCollections[i];

                // Skip UI-based containers
                if (collection.GetComponent<ItemContainer>() != null)
                    continue;

                GameObject prefabForCollection = InventoryManager.GetPrefab(collection.name);

                // Store non-UI prefab reference
                if (prefabForCollection == null)
                {
                    collection.transform.parent = InventoryManager.current.transform;
                    InventoryManager.m_PrefabCache.Add(collection.name, collection.gameObject);
                    collection.gameObject.SetActive(false);
                    continue;
                }

                UnityEngine.Object.Destroy(collection.gameObject);
            }

            if (MiniJSON.Deserialize(json) is not List<object> list)
            {
                Debug.LogWarning("[Inventory System] Failed to parse scene JSON.");
                return;
            }

            foreach (object entry in list)
            {
                if (entry is not Dictionary<string, object> mData)
                    continue;

                string prefab = mData["Prefab"] as string;
                List<object> positionData = mData["Position"] as List<object>;
                List<object> rotationData = mData["Rotation"] as List<object>;

                if (positionData?.Count != 3 || rotationData?.Count != 3)
                    continue;

                Vector3 position = new Vector3(
                    System.Convert.ToSingle(positionData[0]),
                    System.Convert.ToSingle(positionData[1]),
                    System.Convert.ToSingle(positionData[2])
                );

                Quaternion rotation = Quaternion.Euler(new Vector3(
                    System.Convert.ToSingle(rotationData[0]),
                    System.Convert.ToSingle(rotationData[1]),
                    System.Convert.ToSingle(rotationData[2])
                ));

                GameObject collectionGameObject = CreateCollection(prefab, position, rotation);
                if (collectionGameObject != null)
                {
                    IGenerator[] generators = collectionGameObject.GetComponents<IGenerator>();
                    foreach (var generator in generators)
                    {
                        generator.enabled = false;
                    }

                    ItemCollection itemCollection = collectionGameObject.GetComponent<ItemCollection>();
                    itemCollection.SetObjectData(mData);
                }
            }

            if (InventoryManager.DefaultSettings.debugMessages)
            {
                Debug.Log("[Inventory System] Scene Loaded: " + json);
            }
        }


        public static GameObject GetPrefab(string prefabName)
        {
            GameObject prefab = null;
            //Return from cache
            if (InventoryManager.m_PrefabCache.TryGetValue(prefabName, out prefab))
            {
                return prefab;
            }
            //Get from database
            prefab = InventoryManager.Database.GetItemPrefab(prefabName);

            //Load from Resources
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>(prefabName);
            }
            // Add to cache
            if (prefab != null)
            {
                InventoryManager.m_PrefabCache.Add(prefabName, prefab);
            }
            return prefab;

        }

        private static GameObject CreateCollection(string prefabName, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = InventoryManager.GetPrefab(prefabName);

            if (prefab != null)
            {
                GameObject go = InventoryManager.Instantiate(prefab, position, rotation);
                go.name = go.name.Replace("(Clone)", "");
                go.SetActive(true);
                return go;

            }
            return null;
        }

        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation)
        {
#if Proxy
            return Proxy.Instantiate(original, position, rotation);
#else
            return GameObject.Instantiate(original, position, rotation);
#endif
        }

        public static void Destroy(GameObject gameObject)
        {
#if Proxy
            Proxy.Destroy(gameObject);
#else
            GameObject.Destroy(gameObject);
#endif
        }

        public static Item[] CreateInstances(ItemGroup group)
        {
            if (group == null)
            {
                return CreateInstances(Database.items.ToArray(), Enumerable.Repeat(1, Database.items.Count).ToArray(), Enumerable.Repeat(new ItemModifierList(), Database.items.Count).ToArray());
            }
            return CreateInstances(group.Items, group.Amounts, group.Modifiers.ToArray());
        }


        public static Item CreateInstance(Item item)
        {
            return CreateInstance(item, item.Stack, new ItemModifierList());
        }

        public static Item CreateInstance(Item item, int amount, ItemModifierList modiferList)
        {
            return CreateInstances(new Item[] { item }, new int[] { amount }, new ItemModifierList[] { modiferList })[0];
        }

        public static Item[] CreateInstances(Item[] items)
        {
            return CreateInstances(items, Enumerable.Repeat(1, items.Length).ToArray(), new ItemModifierList[items.Length]);
        }

        public static Item[] CreateInstances(Item[] items, int[] amounts, ItemModifierList[] modifierLists)
        {
            Item[] instances = new Item[items.Length];

            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                item = Instantiate(item);
                item.Stack = amounts[i];
                if (i < modifierLists.Length)
                    modifierLists[i].Modify(item);

                if (item.IsCraftable)
                {
                    for (int j = 0; j < item.ingredients.Count; j++)
                    {
                        item.ingredients[j].item = Instantiate(item.ingredients[j].item);
                        item.ingredients[j].item.Stack = item.ingredients[j].amount;
                    }
                }
                instances[i] = item;
            }
            return instances;
        }
    }
}