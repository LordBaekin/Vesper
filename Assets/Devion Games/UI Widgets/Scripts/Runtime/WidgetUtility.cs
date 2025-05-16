using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace DevionGames.UIWidgets
{
	public static class WidgetUtility
	{
        /// <summary>
        /// The widget cache.
        /// </summary>
        private static Dictionary<string, List<UIWidget>> widgetCache = new();

		/// <summary>
		/// Get an UIWidget by name.
		/// </summary>
		/// <param name="name">Name.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public static T Find<T> (string name) where T: UIWidget
		{
			return (T)(FindAll<T> (name).FirstOrDefault ());
		}

		static WidgetUtility() {
			UnityEngine.SceneManagement.SceneManager.activeSceneChanged += ChangedActiveScene;
		}

		private static void ChangedActiveScene(UnityEngine.SceneManagement.Scene Current, UnityEngine.SceneManagement.Scene next)
		{
			widgetCache.Clear();
		}

        /// <summary>
        /// Get an UIWidget by name.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public static T[] FindAll<T>(string name) where T : UIWidget
        {

            if (!widgetCache.TryGetValue(name, out List<UIWidget> Current) || Current.Count == 0)
            {
                Current = new List<UIWidget>();

#if UNITY_2023_1_OR_NEWER
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
                Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
#endif

                foreach (var canvas in canvases)
                {
                    T[] widgets = canvas.GetComponentsInChildren<T>(true);
                    Current.AddRange(
                        widgets.Where(x => x.Name == name)
                               .OrderByDescending(y => y.priority)
                               .Cast<UIWidget>()
                    );
                }

                Current = Current.Distinct().ToList();

                if (!widgetCache.ContainsKey(name))
                {
                    widgetCache.Add(name, Current);
                }
                else
                {
                    widgetCache[name] = Current;
                }
            }

            return Current.Where(x => typeof(T).IsAssignableFrom(x.GetType()))
                          .Cast<T>()
                          .ToArray();
        }


        public static T[] FindAll<T>() where T : UIWidget
        {
            List<UIWidget> Current = new();

        #if UNITY_2023_1_OR_NEWER
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        #else
            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
        #endif

            foreach (var canvas in canvases)
            {
                T[] widgets = canvas.GetComponentsInChildren<T>(true);
                Current.AddRange(
                    widgets.OrderByDescending(y => y.priority)
                           .Cast<UIWidget>()
                );
            }

            return Current.Distinct()
                          .Where(x => typeof(T).IsAssignableFrom(x.GetType()))
                          .Cast<T>()
                          .ToArray();
        }



        private static AudioSource audioSource;

        /// <summary>
        /// Play an AudioClip.
        /// </summary>
        /// <param name="clip">Clip.</param>
        /// <param name="volume">Volume.</param>
        public static void PlaySound(AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            if (audioSource == null)
            {
			#if UNITY_2023_1_OR_NEWER
                AudioListener listener = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
			#else
					AudioListener listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
			#endif

                if (listener != null)
                {
                    audioSource = listener.GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        audioSource = listener.gameObject.AddComponent<AudioSource>();
                    }
                }
            }

            if (audioSource != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }


        /// <summary>
        /// Converts a color to hex.
        /// </summary>
        /// <returns>Hex string</returns>
        /// <param name="color">Color.</param>
        public static string ColorToHex (Color32 color)
		{
			string hex = color.r.ToString ("X2") + color.g.ToString ("X2") + color.b.ToString ("X2");
			return hex;
		}

		/// <summary>
		/// Converts a hex string to color.
		/// </summary>
		/// <returns>Color</returns>
		/// <param name="hex">Hex.</param>
		public static Color HexToColor (string hex)
		{
			hex = hex.Replace ("0x", "");
			hex = hex.Replace ("#", "");
			byte a = 255;
			byte r = byte.Parse (hex.Substring (0, 2), System.Globalization.NumberStyles.HexNumber);
			byte g = byte.Parse (hex.Substring (2, 2), System.Globalization.NumberStyles.HexNumber);
			byte b = byte.Parse (hex.Substring (4, 2), System.Globalization.NumberStyles.HexNumber);
			if (hex.Length == 8) {
				a = byte.Parse (hex.Substring (4, 2), System.Globalization.NumberStyles.HexNumber);
			}
			return new Color32 (r, g, b, a);
		}

		/// <summary>
		/// Colors the string.
		/// </summary>
		/// <returns>The colored string.</returns>
		/// <param name="value">Value.</param>
		/// <param name="color">Color.</param>
		public static string ColorString (string value, Color color)
		{
            if (string.IsNullOrEmpty(value)) { return string.Empty; }
			return "<color=#" + ColorToHex (color) + ">" + value + "</color>";
		}
    }
}