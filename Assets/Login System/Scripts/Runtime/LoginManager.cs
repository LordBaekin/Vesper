using DevionGames.LoginSystem.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using System.Text;
using System;

namespace DevionGames.LoginSystem
{
    [Serializable]
    public class AuthResponse
    {
        public string token;
        public string refresh;
        public int expires_in;
        public string id;
        public string username;
        public string message;
        public string error;
    }

    [Serializable]
    public class MessageResponse
    {
        public string message;
        public string error;
    }

    public class LoginManager : MonoBehaviour
    {
        private static LoginManager m_Current;

        /// <summary>
        /// The LoginManager singleton object. This object is set inside Awake()
        /// </summary>
        public static LoginManager current
        {
            get
            {
                Assert.IsNotNull(m_Current, "Requires Login Manager.Create one from Tools > Devion Games > Login System > Create Login Manager!");
                return m_Current;
            }
        }

        /// <summary>
        /// Constructs a proper endpoint URL by ensuring the base URL has a trailing slash
        /// </summary>
        /// <param name="endpointName">The endpoint name to append to the base URL</param>
        /// <returns>A properly formatted full URL</returns>
        private static string GetEndpoint(string endpointName)
        {
            string baseUrl = LoginManager.Server.serverAddress.TrimEnd('/') + "/";
            return baseUrl + endpointName;
        }

        /// <summary>
        /// Helper method to check if a web request was successful based on Unity version
        /// </summary>
        private static bool IsRequestFailed(UnityWebRequest www)
        {
#if UNITY_2020_1_OR_NEWER
            return www.result != UnityWebRequest.Result.Success;
#else
            return www.isNetworkError || www.isHttpError;
#endif
        }

        /// <summary>
        /// Gets the JWT token from PlayerPrefs
        /// </summary>
        /// <returns>The stored JWT token or null if not found</returns>
        public static string GetAuthToken()
        {
            return PlayerPrefs.GetString("jwt_token", null);
        }

        /// <summary>
        /// Gets the refresh token from PlayerPrefs
        /// </summary>
        public static string GetRefreshToken()
        {
            return PlayerPrefs.GetString("refresh_token", null);
        }

        /// <summary>
        /// Sets the Authorization header for a request with the stored JWT token
        /// </summary>
        /// <param name="request">The UnityWebRequest to add the header to</param>
        public static void SetAuthHeader(UnityWebRequest request)
        {
            string token = GetAuthToken();
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }
        }

        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        private void Awake()
        {
            if (LoginManager.m_Current != null)
            {
                if (LoginManager.DefaultSettings.debug)
                    Debug.Log("Multiple LoginManager in scene...this is not supported. Destroying instance!");
                Destroy(gameObject);
                return;
            }
            else
            {
                LoginManager.m_Current = this;
                if (LoginManager.DefaultSettings.debug)
                    Debug.Log("LoginManager initialized.");

                // Share the server base URL with other systems via PlayerPrefs
                PlayerPrefs.SetString("ServerBaseUrl", LoginManager.Server.serverAddress);

                // Listen for token expiration events from other systems
                EventHandler.Register("OnAuthTokenExpired", OnAuthTokenExpired);
            }
        }

        private void OnDestroy()
        {
            EventHandler.Unregister("OnAuthTokenExpired", OnAuthTokenExpired);
        }

        /// <summary>
        /// Handle token expiration events from other systems
        /// </summary>
        private void OnAuthTokenExpired()
        {
            // When another system detects an expired token, attempt to refresh it
            RefreshToken();
        }

        private void Start()
        {
            if (LoginManager.DefaultSettings.skipLogin)
            {
                if (LoginManager.DefaultSettings.debug)
                    Debug.Log("Login System is disabled...Loading " + LoginManager.DefaultSettings.sceneToLoad + " scene.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(LoginManager.DefaultSettings.sceneToLoad);
            }
        }

        [SerializeField]
        private LoginConfigurations m_Configurations = null;

        /// <summary>
        /// Gets the login configurations. Configurate it inside the editor.
        /// </summary>
        /// <value>The database.</value>
        public static LoginConfigurations Configurations
        {
            get
            {
                if (LoginManager.current != null)
                {
                    Assert.IsNotNull(LoginManager.current.m_Configurations, "Please assign Login Configurations to the Login Manager!");
                    return LoginManager.current.m_Configurations;
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

        private static Server m_Server;
        public static Server Server
        {
            get
            {
                if (m_Server == null)
                {
                    m_Server = GetSetting<Server>();
                }
                return m_Server;
            }
        }

        private static T GetSetting<T>() where T : Configuration.Settings
        {
            if (LoginManager.Configurations != null)
            {
                return (T)LoginManager.Configurations.settings.Where(x => x.GetType() == typeof(T)).FirstOrDefault();
            }
            return default(T);
        }

        public static void CreateAccount(string username, string password, string email)
        {
            if (LoginManager.current != null)
            {
                LoginManager.current.StartCoroutine(CreateAccountInternal(username, password, email));
            }
        }

        private static IEnumerator CreateAccountInternal(string username, string password, string email)
        {
            if (LoginManager.Configurations == null)
            {
                EventHandler.Execute("OnFailedToCreateAccount");
                yield break;
            }
            if (LoginManager.DefaultSettings.debug)
                Debug.Log("[CreateAccount]: Trying to register a new account with username: " + username);

            // Prepare JSON payload
            var uri = GetEndpoint(LoginManager.Server.createAccount);
            var payload = JsonUtility.ToJson(new { username = username, password = password, email = email });
            using (var www = new UnityWebRequest(uri, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (IsRequestFailed(www))
                {
                    Debug.LogError("[CreateAccount] Error: " + www.error);
                    EventHandler.Execute("OnFailedToCreateAccount");
                }
                else
                {
                    // Check for error message first
                    try
                    {
                        var response = JsonUtility.FromJson<AuthResponse>(www.downloadHandler.text);

                        // Check for error
                        if (!string.IsNullOrEmpty(response.error))
                        {
                            Debug.LogWarning("[CreateAccount] Server error: " + response.error);
                            EventHandler.Execute("OnFailedToCreateAccount");
                            yield break;
                        }

                        // Valid token = success
                        if (!string.IsNullOrEmpty(response.token))
                        {
                            PlayerPrefs.SetString("jwt_token", response.token);
                            if (!string.IsNullOrEmpty(response.refresh))
                            {
                                PlayerPrefs.SetString("refresh_token", response.refresh);
                            }
                            PlayerPrefs.Save();

                            if (LoginManager.DefaultSettings.debug)
                                Debug.Log("[CreateAccount] Account creation successful, token stored.");
                            EventHandler.Execute("OnAccountCreated");
                        }
                        else
                        {
                            Debug.LogWarning("[CreateAccount] No token in response");
                            EventHandler.Execute("OnFailedToCreateAccount");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[CreateAccount] JSON parse error: " + ex.Message);
                        Debug.LogError("Response was: " + www.downloadHandler.text);
                        EventHandler.Execute("OnFailedToCreateAccount");
                    }
                }
            }
        }

        /// <summary>
        /// Logins the account.
        /// </summary>
        /// <param name="username">Username.</param>
        /// <param name="password">Password.</param>
        public static void LoginAccount(string username, string password)
        {
            if (LoginManager.current != null)
            {
                LoginManager.current.StartCoroutine(LoginAccountInternal(username, password));
            }
        }

        private static IEnumerator LoginAccountInternal(string username, string password)
        {
            if (LoginManager.Configurations == null)
            {
                EventHandler.Execute("OnFailedToLogin");
                yield break;
            }
            if (LoginManager.DefaultSettings.debug)
                Debug.Log("[LoginAccount] Trying to login using username: " + username);

            // Prepare JSON payload
            var uri = GetEndpoint(LoginManager.Server.login);
            var payload = JsonUtility.ToJson(new { username = username, password = password });
            using (var www = new UnityWebRequest(uri, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (IsRequestFailed(www))
                {
                    Debug.LogError("[LoginAccount] Error: " + www.error);
                    EventHandler.Execute("OnFailedToLogin");
                }
                else
                {
                    try
                    {
                        var response = JsonUtility.FromJson<AuthResponse>(www.downloadHandler.text);

                        // Check for error
                        if (!string.IsNullOrEmpty(response.error))
                        {
                            Debug.LogWarning("[LoginAccount] Server error: " + response.error);
                            EventHandler.Execute("OnFailedToLogin");
                            yield break;
                        }

                        // Valid token = success
                        if (!string.IsNullOrEmpty(response.token))
                        {
                            // Store the username using the server's account key (for compatibility)
                            PlayerPrefs.SetString(LoginManager.Server.accountKey, username);

                            // Store JWT tokens
                            PlayerPrefs.SetString("jwt_token", response.token);
                            if (!string.IsNullOrEmpty(response.refresh))
                            {
                                PlayerPrefs.SetString("refresh_token", response.refresh);
                            }
                            PlayerPrefs.Save();

                            // Update the server URL in PlayerPrefs for other systems
                            PlayerPrefs.SetString("ServerBaseUrl", LoginManager.Server.serverAddress);

                            if (LoginManager.DefaultSettings.debug)
                                Debug.Log("[LoginAccount] Login successful, token stored.");
                            EventHandler.Execute("OnLogin");
                        }
                        else
                        {
                            Debug.LogWarning("[LoginAccount] No token in response");
                            EventHandler.Execute("OnFailedToLogin");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[LoginAccount] JSON parse error: " + ex.Message);
                        Debug.LogError("Response was: " + www.downloadHandler.text);
                        EventHandler.Execute("OnFailedToLogin");
                    }
                }
            }
        }

        /// <summary>
        /// Recovers the password.
        /// </summary>
        /// <param name="email">Email.</param>
        public static void RecoverPassword(string email)
        {
            if (LoginManager.current != null)
            {
                LoginManager.current.StartCoroutine(RecoverPasswordInternal(email));
            }
        }

        private static IEnumerator RecoverPasswordInternal(string email)
        {
            if (LoginManager.Configurations == null)
            {
                EventHandler.Execute("OnFailedToRecoverPassword");
                yield break;
            }
            if (LoginManager.DefaultSettings.debug)
                Debug.Log("[RecoverPassword] Trying to recover password using email: " + email);

            // Prepare JSON payload
            var uri = GetEndpoint(LoginManager.Server.recoverPassword);
            var payload = JsonUtility.ToJson(new { email = email });
            using (var www = new UnityWebRequest(uri, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (IsRequestFailed(www))
                {
                    Debug.LogError("[RecoverPassword] Error: " + www.error);
                    EventHandler.Execute("OnFailedToRecoverPassword");
                }
                else
                {
                    try
                    {
                        // For password reset requests, your server sends a message success response
                        // Parse the response to check for success
                        var responseData = JsonUtility.FromJson<MessageResponse>(www.downloadHandler.text);

                        // Check for error
                        if (!string.IsNullOrEmpty(responseData.error))
                        {
                            Debug.LogWarning("[RecoverPassword] Server error: " + responseData.error);
                            EventHandler.Execute("OnFailedToRecoverPassword");
                            yield break;
                        }

                        // Success case - message exists
                        if (!string.IsNullOrEmpty(responseData.message))
                        {
                            if (LoginManager.DefaultSettings.debug)
                                Debug.Log("[RecoverPassword] Password recovery email sent successfully");
                            EventHandler.Execute("OnPasswordRecovered");
                        }
                        else
                        {
                            Debug.LogWarning("[RecoverPassword] No success message in response");
                            EventHandler.Execute("OnFailedToRecoverPassword");
                        }
                    }
                    catch (Exception ex)
                    {
                        // If JSON parsing fails, try the legacy approach as fallback
                        if (www.downloadHandler.text.Contains("true") || www.downloadHandler.text.Contains("message"))
                        {
                            if (LoginManager.DefaultSettings.debug)
                                Debug.Log("[RecoverPassword] Password recovery email sent successfully");
                            EventHandler.Execute("OnPasswordRecovered");
                        }
                        else
                        {
                            Debug.LogError("[RecoverPassword] Error: " + ex.Message);
                            Debug.LogError("Response was: " + www.downloadHandler.text);
                            EventHandler.Execute("OnFailedToRecoverPassword");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resets the password.
        /// </summary>
        /// <param name="token">The reset token received via email.</param>
        /// <param name="password">The new password to set.</param>
        public static void ResetPassword(string token, string password)
        {
            if (LoginManager.current != null)
            {
                LoginManager.current.StartCoroutine(ResetPasswordInternal(token, password));
            }
        }

        private static IEnumerator ResetPasswordInternal(string token, string password)
        {
            if (LoginManager.Configurations == null)
            {
                EventHandler.Execute("OnFailedToResetPassword");
                yield break;
            }
            if (LoginManager.DefaultSettings.debug)
                Debug.Log("[ResetPassword] Trying to reset password with token");

            // Prepare JSON payload
            var uri = GetEndpoint(LoginManager.Server.resetPassword);
            var payload = JsonUtility.ToJson(new { token = token, new_password = password });
            using (var www = new UnityWebRequest(uri, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (IsRequestFailed(www))
                {
                    Debug.LogError("[ResetPassword] Error: " + www.error);
                    EventHandler.Execute("OnFailedToResetPassword");
                }
                else
                {
                    try
                    {
                        // For password reset, your server sends a success message
                        var responseData = JsonUtility.FromJson<MessageResponse>(www.downloadHandler.text);

                        // Check for error
                        if (!string.IsNullOrEmpty(responseData.error))
                        {
                            Debug.LogWarning("[ResetPassword] Server error: " + responseData.error);
                            EventHandler.Execute("OnFailedToResetPassword");
                            yield break;
                        }

                        // Success case - message exists
                        if (!string.IsNullOrEmpty(responseData.message))
                        {
                            if (LoginManager.DefaultSettings.debug)
                                Debug.Log("[ResetPassword] Password reset successful");
                            EventHandler.Execute("OnPasswordResetted");
                        }
                        else
                        {
                            Debug.LogWarning("[ResetPassword] No success message in response");
                            EventHandler.Execute("OnFailedToResetPassword");
                        }
                    }
                    catch (Exception ex)
                    {
                        // If JSON parsing fails, try the legacy approach as fallback
                        if (www.downloadHandler.text.Contains("true") || www.downloadHandler.text.Contains("message"))
                        {
                            if (LoginManager.DefaultSettings.debug)
                                Debug.Log("[ResetPassword] Password reset successful");
                            EventHandler.Execute("OnPasswordResetted");
                        }
                        else
                        {
                            Debug.LogError("[ResetPassword] Error: " + ex.Message);
                            Debug.LogError("Response was: " + www.downloadHandler.text);
                            EventHandler.Execute("OnFailedToResetPassword");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validates the email.
        /// </summary>
        /// <returns><c>true</c>, if email was validated, <c>false</c> otherwise.</returns>
        /// <param name="email">Email.</param>
        public static bool ValidateEmail(string email)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$");
            System.Text.RegularExpressions.Match match = regex.Match(email);
            if (match.Success)
            {
                if (LoginManager.DefaultSettings.debug)
                    Debug.Log("Email validation was successful for email: " + email);
            }
            else
            {
                if (LoginManager.DefaultSettings.debug)
                    Debug.Log("Email validation failed for email: " + email);
            }

            return match.Success;
        }

        /// <summary>
        /// Refreshes the JWT token using the stored refresh token
        /// </summary>
        public static void RefreshToken()
        {
            if (LoginManager.current != null)
            {
                string refreshToken = GetRefreshToken();
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    LoginManager.current.StartCoroutine(RefreshTokenInternal(refreshToken));
                }
                else
                {
                    Debug.LogWarning("No refresh token available to refresh the session");
                    EventHandler.Execute("OnSessionExpired");
                }
            }
        }

        private static IEnumerator RefreshTokenInternal(string refreshToken)
        {
            if (LoginManager.Configurations == null)
            {
                Debug.LogError("Unable to refresh token: configurations not found");
                EventHandler.Execute("OnSessionExpired");
                yield break;
            }

            if (LoginManager.DefaultSettings.debug)
                Debug.Log("[RefreshToken] Attempting to refresh authentication token");

            // Prepare JSON payload
            var uri = GetEndpoint(LoginManager.Server.refreshToken);
            var payload = JsonUtility.ToJson(new { refresh_token = refreshToken });

            using (var www = new UnityWebRequest(uri, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (IsRequestFailed(www))
                {
                    Debug.LogError("[RefreshToken] Error: " + www.error);
                    EventHandler.Execute("OnSessionExpired");
                }
                else
                {
                    try
                    {
                        var response = JsonUtility.FromJson<AuthResponse>(www.downloadHandler.text);

                        // Check for error
                        if (!string.IsNullOrEmpty(response.error))
                        {
                            Debug.LogWarning("[RefreshToken] Server error: " + response.error);
                            EventHandler.Execute("OnSessionExpired");
                            yield break;
                        }

                        // Valid token = success
                        if (!string.IsNullOrEmpty(response.token))
                        {
                            // Store JWT tokens
                            PlayerPrefs.SetString("jwt_token", response.token);
                            if (!string.IsNullOrEmpty(response.refresh))
                            {
                                PlayerPrefs.SetString("refresh_token", response.refresh);
                            }

                            // Set flag for other systems
                            PlayerPrefs.SetString("jwt_token_refreshed", "true");

                            PlayerPrefs.Save();

                            if (LoginManager.DefaultSettings.debug)
                                Debug.Log("[RefreshToken] Token refreshed successfully");

                            EventHandler.Execute("OnTokenRefreshed");
                        }
                        else
                        {
                            Debug.LogWarning("[RefreshToken] No token in response");
                            EventHandler.Execute("OnSessionExpired");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[RefreshToken] JSON parse error: " + ex.Message);
                        Debug.LogError("Response was: " + www.downloadHandler.text);
                        EventHandler.Execute("OnSessionExpired");
                    }
                }
            }
        }
    }
}