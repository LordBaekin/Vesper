using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using DevionGames.StatSystem.Configuration;

namespace DevionGames.StatSystem
{
    public class StatsManager : MonoBehaviour
    {
        private static StatsManager m_Current;

        /// <summary>
        /// The StatManager singleton object. This object is set inside Awake()
        /// </summary>
        public static StatsManager Current
        {
            get
            {
                Assert.IsNotNull(m_Current, "Requires a Stats Manager.Create one from Tools > Devion Games > Stat System > Create Stats Manager!");
                return m_Current;
            }
        }

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
                if (StatsManager.Current != null)
                {
                    Assert.IsNotNull(StatsManager.Current.m_Database, "Please assign StatDatabase to the Stats Manager!");
                    return StatsManager.Current.m_Database;
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
            return default(T);
        }

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
                if (StatsManager.SavingLoading.autoSave)
                {
                    StartCoroutine(RepeatSaving(StatsManager.SavingLoading.savingRate));
                }
                if (StatsManager.DefaultSettings.debugMessages)
                    Debug.Log("Stats Manager initialized.");
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
        #if UNITY_2023_1_OR_NEWER
            StatsHandler[] results = UnityEngine.Object
                .FindObjectsByType<StatsHandler>(FindObjectsSortMode.None)
                .Where(x => x.saveable)
                .ToArray();
        #else
            StatsHandler[] results = UnityEngine.Object
                .FindObjectsOfType<StatsHandler>()
                .Where(x => x.saveable)
                .ToArray();
        #endif

            if (results.Length == 0)
            {
                return;
            }

            string data = JsonSerializer.Serialize(results);

            // Required for Select Character Scene in RPG Kit
            foreach (StatsHandler handler in results)
            {
                foreach (Stat stat in handler.m_Stats)
                {
                    string basePath = $"{key}.Stats.{handler.HandlerName}.{stat.Name}";
                    PlayerPrefs.SetFloat($"{basePath}.Value", stat.Value);

                    if (stat is Attribute attribute)
                    {
                        PlayerPrefs.SetFloat($"{basePath}.CurrentValue", attribute.CurrentValue);
                    }
                }
            }

            PlayerPrefs.SetString($"{key}.Stats", data);

            // Maintain list of saved keys
            string savedKeysRaw = PlayerPrefs.GetString("StatSystemSavedKeys", "");
            List<string> keys = new(savedKeysRaw.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));

            if (!keys.Contains(key))
            {
                keys.Add(key);
            }

            PlayerPrefs.SetString("StatSystemSavedKeys", string.Join(";", keys));

            if (StatsManager.DefaultSettings.debugMessages)
            {
                Debug.Log("[Stat System] Stats saved: " + data);
            }
        }


        public static void Load()
        {
            string key = PlayerPrefs.GetString(StatsManager.SavingLoading.savingKey, StatsManager.SavingLoading.savingKey);
            Load(key);
        }

        public static void Load(string key)
        {
            string data = PlayerPrefs.GetString(key + ".Stats");
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

        #if UNITY_2023_1_OR_NEWER
            List<StatsHandler> results = UnityEngine.Object
                .FindObjectsByType<StatsHandler>(FindObjectsSortMode.None)
                .Where(x => x.saveable)
                .ToList();
#else
            List<StatsHandler> results = UnityEngine.Object
                .FindObjectsOfType<StatsHandler>()
                .Where(x => x.saveable)
                .ToList();
#endif

            if (MiniJSON.Deserialize(data) is not List<object> list)
            {
                Debug.LogWarning("[Stat System] Failed to deserialize stats data.");
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Dictionary<string, object> handlerData &&
                    handlerData.TryGetValue("Name", out object nameObj) &&
                    nameObj is string handlerName)
                {
                    StatsHandler handler = results.Find(x => x.HandlerName == handlerName);
                    if (handler != null)
                    {
                        handler.SetObjectData(handlerData);
                    }
                }
            }

            if (StatsManager.DefaultSettings.debugMessages)
                Debug.Log("[Stat System] Stats loaded: " + data);
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

        public static void RegisterStatsHandler(StatsHandler handler)
        {
            if (!StatsManager.Current.m_StatsHandler.Contains(handler))
            {
                StatsManager.Current.m_StatsHandler.Add(handler);
            }
        }

        public static StatsHandler GetStatsHandler(string name)
        {
            return StatsManager.Current.m_StatsHandler.Find(x => x.HandlerName == name);
        }

    }
}