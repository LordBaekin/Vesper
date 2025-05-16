using DevionGames.UIWidgets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DevionGames.LoginSystem
{
    public class LoginWindow : UIWidget
    {
        public override string[] Callbacks
        {
            get
            {
                List<string> callbacks = new List<string>(base.Callbacks);
                callbacks.Add("OnLogin");
                callbacks.Add("OnFailedToLogin");
                callbacks.Add("OnSessionExpired"); // Add callback for session expiration
                return callbacks.ToArray();
            }
        }

        [Header("Reference")]
        /// <summary>
        /// Referenced UI field
        /// </summary>
        [SerializeField]
        protected InputField username;
        /// <summary>
        /// Referenced UI field
        /// </summary>
        [SerializeField]
        protected InputField password;
        /// <summary>
        /// Referenced UI field
        /// </summary>
        [SerializeField]
        protected Toggle rememberMe;
        /// <summary>
        /// Referenced UI field
        /// </summary>
        [SerializeField]
        protected Button loginButton;
        [SerializeField]
        protected GameObject loadingIndicator;

        protected override void OnStart()
        {
            base.OnStart();
            username.text = PlayerPrefs.GetString("username", string.Empty);

            // We don't store actual passwords anymore for security reasons - we use tokens instead
            password.text = string.Empty;

            if (rememberMe != null)
            {
                rememberMe.isOn = !string.IsNullOrEmpty(username.text);
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            EventHandler.Register("OnLogin", OnLogin);
            EventHandler.Register("OnFailedToLogin", OnFailedToLogin);
            EventHandler.Register("OnSessionExpired", OnSessionExpired); // Register for session expiration

            loginButton.onClick.AddListener(LoginUsingFields);

            // Check if we have a JWT token already - if so, we can attempt to auto-login
            // by refreshing the token instead of showing the login screen
            string token = LoginManager.GetAuthToken();
            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(username.text))
            {
                // Auto-refresh if we have a token
                StartCoroutine(AttemptTokenRefresh());
            }
        }

        private IEnumerator AttemptTokenRefresh()
        {
            loginButton.interactable = false;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }

            LoginManager.RefreshToken();

            // Wait a reasonable amount of time for the refresh to complete
            float timer = 0f;
            bool refreshComplete = false;

            // Register one-time listeners for success/failure
            System.Action successAction = () => {
                refreshComplete = true;
                OnLogin();
            };

            System.Action failureAction = () => {
                refreshComplete = true;
                // Reset UI but don't show error - just let user log in normally
                loginButton.interactable = true;
                if (loadingIndicator != null)
                {
                    loadingIndicator.SetActive(false);
                }
            };

            EventHandler.Register("OnTokenRefreshed", successAction);
            EventHandler.Register("OnSessionExpired", failureAction);

            // Wait for either success/failure or timeout
            while (!refreshComplete && timer < 5f)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // Clean up event listeners
            EventHandler.Unregister("OnTokenRefreshed", successAction);
            EventHandler.Unregister("OnSessionExpired", failureAction);

            // If we timed out, reset UI
            if (!refreshComplete)
            {
                loginButton.interactable = true;
                if (loadingIndicator != null)
                {
                    loadingIndicator.SetActive(false);
                }
            }
        }

        public void LoginUsingFields()
        {
            LoginManager.LoginAccount(username.text, password.text);
            loginButton.interactable = false;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }
        }

        private void OnLogin()
        {
            if (rememberMe != null && rememberMe.isOn)
            {
                // Only remember username, never remember password for security
                PlayerPrefs.SetString("username", username.text);
                PlayerPrefs.DeleteKey("password"); // Clear any old password storage
            }
            else
            {
                PlayerPrefs.DeleteKey("username");
                PlayerPrefs.DeleteKey("password");
            }

            Execute("OnLogin", new CallbackEventData());

            if (LoginManager.DefaultSettings.loadSceneOnLogin)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(LoginManager.DefaultSettings.sceneToLoad);
            }
        }

        private void OnFailedToLogin()
        {
            Execute("OnFailedToLogin", new CallbackEventData());
            password.text = ""; // Clear password field for security
            LoginManager.Notifications.loginFailed.Show(delegate (int result) { Show(); }, "OK");
            loginButton.interactable = true;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            Close();
        }

        private void OnSessionExpired()
        {
            // Show login window when session expires
            Show();
            Execute("OnSessionExpired", new CallbackEventData());
            // Use existing notification with a different message
            LoginManager.Notifications.loginFailed.Show(delegate (int result) { Show(); }, "OK", "Session expired. Please log in again.");
            loginButton.interactable = true;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventHandler.Unregister("OnLogin", OnLogin);
            EventHandler.Unregister("OnFailedToLogin", OnFailedToLogin);
            EventHandler.Unregister("OnSessionExpired", OnSessionExpired);
        }
    }
}