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

namespace DevionGames.InventorySystem
{
	public class InventoryManager : MonoBehaviour
	{
        /// <summary>
		/// Don't destroy this object instance when loading new scenes.
		/// </summary>
		public bool dontDestroyOnLoad = true;

        private static InventoryManager m_Current;

        /// <summary>
        /// The InventoryManager singleton object. This object is set inside Awake()
        /// </summary>
        public static InventoryManager Current
        {
			get {
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
		public static ItemDatabase Database {
			get {
				if (InventoryManager.Current != null) {
                    Assert.IsNotNull(InventoryManager.Current.m_Database, "Please assign ItemDatabase to the Inventory Manager!");
                    return InventoryManager.Current.m_Database;
				}
				return null;
			}
		}

        [SerializeField]
        private ItemDatabase[] m_ChildDatabases= null;

        private static Default m_DefaultSettings;
        public static Default DefaultSettings {
            get {
                if (m_DefaultSettings== null)
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
                    m_Notifications= GetSetting<Notifications>();
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

        private static T GetSetting<T>() where T: Configuration.Settings{
            if (InventoryManager.Database != null)
            {
                return (T)InventoryManager.Database.settings.Where(x => x.GetType() == typeof(T)).FirstOrDefault();
            }
            return default(T);
        }


        protected static Dictionary<string, GameObject> m_PrefabCache;

        private PlayerInfo m_PlayerInfo;
        public PlayerInfo GetPlayerInfo()
        {
            m_PlayerInfo ??= new PlayerInfo(DefaultSettings.playerTag);
            return this.m_PlayerInfo;
        }

        [HideInInspector]
        public UnityEvent onDataLoaded;
        [HideInInspector]
        public UnityEvent onDataSaved;

        protected static bool m_IsLoaded = false;
        public static bool IsLoaded { get => m_IsLoaded; }


        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        private void Awake ()
		{
			if (InventoryManager.m_Current != null) {
                //if(InventoryManager.DefaultSettings.debugMessages)
                  //  Debug.Log ("Multiple Inventory Manager in scene...this is not supported. Destroying instance!");
				Destroy (gameObject);
				return;
			} else {
				InventoryManager.m_Current = this;
                if (EventSystem.current == null) {
                    if (InventoryManager.DefaultSettings.debugMessages)
                        Debug.Log("Missing EventSystem in scene. Auto creating!");
                        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                }

                if (Camera.main != null && Camera.main.GetComponent<PhysicsRaycaster>() == null) {
                    if (InventoryManager.DefaultSettings.debugMessages)
                        Debug.Log("Missing PhysicsRaycaster on Main Camera. Auto adding!");
                    PhysicsRaycaster physicsRaycaster = Camera.main.gameObject.AddComponent<PhysicsRaycaster>();
                    physicsRaycaster.eventMask = Physics.DefaultRaycastLayers;
                }

                this.m_Database = ScriptableObject.Instantiate(this.m_Database);
                for (int i = 0; i < this.m_ChildDatabases.Length; i++) {
                    ItemDatabase child = this.m_ChildDatabases[i];
                    this.m_Database.Merge(child);
                }

                m_PrefabCache = new Dictionary<string, GameObject>();
                UnityEngine.SceneManagement.SceneManager.activeSceneChanged += ChangedActiveScene;

                if (dontDestroyOnLoad) {
                    if (transform.parent != null)
                    {
                        if (InventoryManager.DefaultSettings.debugMessages)
                            Debug.Log("Inventory Manager with DontDestroyOnLoad can't be a child transform. Unparent!");
                        transform.parent = null;
                    }
					DontDestroyOnLoad (gameObject);
				}
                if (InventoryManager.SavingLoading.autoSave) {
                    StartCoroutine(RepeatSaving(InventoryManager.SavingLoading.savingRate));
                }

                Physics.queriesHitTriggers = InventoryManager.DefaultSettings.queriesHitTriggers;

                m_IsLoaded = !HasSavedData();
                this.onDataLoaded.AddListener(() => { m_IsLoaded = true; });
                if (InventoryManager.DefaultSettings.debugMessages)
                    Debug.Log("Inventory Manager initialized.");
            }
		}

        private void Start()
        {
            /*if (InventoryManager.SavingLoading.autoSave){
                StartCoroutine(DelayedLoading(1f));
            }*/
        }

        private static void ChangedActiveScene(UnityEngine.SceneManagement.Scene Current, UnityEngine.SceneManagement.Scene next)
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
            Bounds bounds = new();
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
                    if (renderer.enabled){
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }
            return bounds;
        }


        private IEnumerator DelayedLoading(float seconds) {
            yield return new WaitForSecondsRealtime(seconds);
            Load();
        }

        private IEnumerator RepeatSaving(float seconds) {
            while (true) {
                yield return new WaitForSeconds(seconds);
                Save();
            }
        }

        public static void Save() {
            string key = PlayerPrefs.GetString(InventoryManager.SavingLoading.savingKey, InventoryManager.SavingLoading.savingKey);
            Save(key);
        }

        public static void Save(string key) {

            /* List<MonoBehaviour> results = new List<MonoBehaviour>();
             UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToList().ForEach(g => results.AddRange(g.GetComponentsInChildren<MonoBehaviour>(true)));
             //DontDestroyOnLoad GameObjects
             SingleInstance.GetInstanceObjects().ForEach(g => results.AddRange(g.GetComponentsInChildren<MonoBehaviour>(true)));

             ItemCollection[] serializables  = results.OfType<ItemCollection>().Where(x=>x.saveable).ToArray();

             IJsonSerializable[] ui = serializables.Where(x=>x.GetComponent<ItemContainer>() != null).ToArray();
             IJsonSerializable[] world = serializables.Except(ui).ToArray();

             string uiData = JsonSerializer.Serialize(ui);
             string worldData = JsonSerializer.Serialize(world);*/

            string uiData = string.Empty;
            string worldData = string.Empty;
            Serialize(ref uiData, ref worldData);

            string CurrentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            PlayerPrefs.SetString(key+".UI",uiData);
            PlayerPrefs.SetString(key + "." + CurrentScene, worldData);
            List<string> scenes = PlayerPrefs.GetString(key + ".Scenes").Split(';').ToList();
            scenes.RemoveAll(x => string.IsNullOrEmpty(x));
            if (!scenes.Contains(CurrentScene)) {
                scenes.Add(CurrentScene);
            }
            PlayerPrefs.SetString(key + ".Scenes", string.Join(";", scenes));
            List<string> keys = PlayerPrefs.GetString("InventorySystemSavedKeys").Split(';').ToList();
            keys.RemoveAll(x => string.IsNullOrEmpty(x));
            if (!keys.Contains(key)) {
                keys.Add(key);
            }
            PlayerPrefs.SetString("InventorySystemSavedKeys",string.Join(";",keys));


            if (InventoryManager.Current != null && InventoryManager.Current.onDataSaved != null){
                InventoryManager.Current.onDataSaved.Invoke();
            }
         
            if (InventoryManager.DefaultSettings.debugMessages){
                Debug.Log("[Inventory System] UI Saved: "+uiData);
                Debug.Log("[Inventory System] Scene Saved: " + worldData);
            }
        }

        public static void Serialize(ref string uiData, ref string sceneData) {
            List<MonoBehaviour> monoBehaviours = new();
            List<MonoBehaviour> results = monoBehaviours;
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToList().ForEach(g => results.AddRange(g.GetComponentsInChildren<MonoBehaviour>(true)));
            //DontDestroyOnLoad GameObjects
            SingleInstance.GetInstanceObjects().ForEach(g => results.AddRange(g.GetComponentsInChildren<MonoBehaviour>(true)));

            ItemCollection[] serializables = results.OfType<ItemCollection>().Where(x => x.saveable).ToArray();

            IJsonSerializable[] ui = serializables.Where(x => x.GetComponent<ItemContainer>() != null).ToArray();
            IJsonSerializable[] world = serializables.Except(ui).ToArray();

            uiData = JsonSerializer.Serialize(ui);
            sceneData = JsonSerializer.Serialize(world);
        }

        public static void Load() {
            string key = PlayerPrefs.GetString(InventoryManager.SavingLoading.savingKey, InventoryManager.SavingLoading.savingKey);
            Load(key);
        }

        public static void Load(string key) {
            string CurrentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string uiData = PlayerPrefs.GetString(key + ".UI");
            string sceneData = PlayerPrefs.GetString(key + "." + CurrentScene);

            Load(uiData, sceneData);
        }

        public static void Load(string uiData, string sceneData) {
            //Load UI
            LoadUI(uiData);
            //Load Scene
            LoadScene(sceneData);

            if (InventoryManager.Current != null && InventoryManager.Current.onDataLoaded != null)
            {
                InventoryManager.Current.onDataLoaded.Invoke();
            }
        }

        public static bool HasSavedData() {
            string key = PlayerPrefs.GetString(InventoryManager.SavingLoading.savingKey, InventoryManager.SavingLoading.savingKey);
            return InventoryManager.HasSavedData(key);
        }

        public static bool HasSavedData(string key) {
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
                _ = new Vector3(System.Convert.ToSingle(positionData[0]), System.Convert.ToSingle(positionData[1]), System.Convert.ToSingle(positionData[2]));
                Quaternion quaternion = Quaternion.Euler(new Vector3(System.Convert.ToSingle(rotationData[0]), System.Convert.ToSingle(rotationData[1]), System.Convert.ToSingle(rotationData[2])));
                Quaternion rotation = quaternion;
                if (type == "UI")
                {
                    UIWidget container = WidgetUtility.Find<UIWidget>(prefab);
                    if (container != null){
                        _ = container.GetComponent<ItemCollection>();
                    }
                }
                if ((ItemCollection)null != null)
                {
                    ((ItemCollection)null).SetObjectData(mData);
                }
            }
            if (InventoryManager.DefaultSettings.debugMessages)
            {
                  Debug.Log("[Inventory System] UI Loaded: "+json);
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
                    collection.transform.parent = InventoryManager.Current.transform;
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

                Vector3 position = new(
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



        public static GameObject GetPrefab(string prefabName) {
            //Return from cache
            if (InventoryManager.m_PrefabCache.TryGetValue(prefabName, out GameObject prefab))
            {
                return prefab;
            }
            //Get from database
            prefab = InventoryManager.Database.GetItemPrefab(prefabName);

            //Load from Resources
            if (prefab == null){
                prefab = Resources.Load<GameObject>(prefabName);
            }
            // Add to cache
            if (prefab != null) {
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
                go.name = go.name.Replace("(Clone)","");
                go.SetActive(true);
                return go;

            }
            return null;
        }

        public static GameObject Instantiate(GameObject original,Vector3 position, Quaternion rotation) {
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
            if (group == null) {
                return CreateInstances(Database.items.ToArray(), Enumerable.Repeat(1, Database.items.Count).ToArray(), Enumerable.Repeat(new ItemModifierList(), Database.items.Count).ToArray());
            }
            return CreateInstances(group.Items, group.Amounts, group.Modifiers.ToArray());
        }


        public static Item CreateInstance(Item item)
        {
            return CreateInstance( item, item.Stack, new ItemModifierList());
        }

        public static Item CreateInstance(Item item, int amount, ItemModifierList modiferList)
        {
            return CreateInstances(new Item[] { item },new int[] { amount }, new ItemModifierList[] { modiferList })[0];
        }

        public static Item[] CreateInstances(Item[] items)
        {
            return CreateInstances(items, Enumerable.Repeat(1, items.Length).ToArray(), new ItemModifierList[items.Length]);
        }

        public static Item[] CreateInstances(Item[] items, int[] amounts, ItemModifierList[] modifierLists) {
            Item[] instances = new Item[items.Length];

            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                item = Instantiate(item);
                item.Stack = amounts[i];
                if(i < modifierLists.Length)
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