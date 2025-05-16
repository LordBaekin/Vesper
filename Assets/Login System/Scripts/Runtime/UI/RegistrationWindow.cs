using DevionGames.UIWidgets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DevionGames.LoginSystem
{
    public class RegistrationWindow : UIWidget
    {
        public override string[] Callbacks
        {
            get
            {
                List<string> callbacks = new List<string>(base.Callbacks);
                callbacks.Add("OnAccountCreated");
                callbacks.Add("OnFailedToCreateAccount");
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
        protected InputField confirmPassword;
        /// <summary>
        /// Referenced UI field
        /// </summary>
        [SerializeField]
        protected InputField email;
        /// <summary>
        /// Referenced UI field
        /// </summary>
        [SerializeField]
        protected Toggle termsOfUse;
        /// <summary>
        /// Referenced UI field
        /// </summary>
        [SerializeField]
        protected Button registerButton;

        [SerializeField]
        protected GameObject loadingIndicator;

        // Optionally track server errors to show better messages
        protected string lastServerError;

        protected override void OnStart()
        {
            base.OnStart();
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            EventHandler.Register("OnAccountCreated", OnAccountCreated);
            EventHandler.Register("OnFailedToCreateAccount", OnFailedToCreateAccount);

            registerButton.onClick.AddListener(CreateAccountUsingFields);

            // Clear fields when opening the window for security
            ClearFields();
        }

        // Helper method to clear sensitive fields
        private void ClearFields()
        {
            username.text = "";
            password.text = "";
            confirmPassword.text = "";
            email.text = "";
            if (termsOfUse != null)
            {
                termsOfUse.isOn = false;
            }
        }

        /// <summary>
        /// Creates the account using data from referenced fields.
        /// </summary>
        private void CreateAccountUsingFields()
        {
            // Reset last error
            lastServerError = null;

            // Check for empty fields
            if (string.IsNullOrEmpty(username.text) ||
                string.IsNullOrEmpty(password.text) ||
                string.IsNullOrEmpty(confirmPassword.text) ||
                string.IsNullOrEmpty(email.text))
            {
                LoginManager.Notifications.emptyField.Show(delegate (int result) { Show(); }, "OK");
                Close();
                return;
            }

            // Check password match
            if (password.text != confirmPassword.text)
            {
                password.text = "";
                confirmPassword.text = "";
                LoginManager.Notifications.passwordMatch.Show(delegate (int result) { Show(); }, "OK");
                Close();
                return;
            }

            // Validate email format
            if (!LoginManager.ValidateEmail(email.text))
            {
                email.text = "";
                LoginManager.Notifications.invalidEmail.Show(delegate (int result) { Show(); }, "OK");
                Close();
                return;
            }

            // Check for terms of use agreement
            if (termsOfUse != null && !termsOfUse.isOn)
            {
                LoginManager.Notifications.termsOfUse.Show(delegate (int result) { Show(); }, "OK");
                Close();
                return;
            }

            // Perform additional validation for password strength
            if (password.text.Length < 8)
            {
                password.text = "";
                confirmPassword.text = "";
                LoginManager.Notifications.passwordMatch.Show(delegate (int result) { Show(); }, "OK",
                    "Password must be at least 8 characters long.");
                Close();
                return;
            }

            registerButton.interactable = false;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }

            // Create account via the LoginManager
            LoginManager.CreateAccount(username.text, password.text, email.text);
        }

        private void OnAccountCreated()
        {
            Execute("OnAccountCreated", new CallbackEventData());

            // Account created successfully, show notification and redirect to login
            LoginManager.Notifications.accountCreated.Show(delegate (int result) {
                // Clear fields for security
                ClearFields();

                // Auto-login if we got a token (which we should have from CreateAccount)
                if (!string.IsNullOrEmpty(LoginManager.GetAuthToken()))
                {
                    // We're already logged in, so skip to the main scene if enabled
                    if (LoginManager.DefaultSettings.loadSceneOnLogin)
                    {
                        UnityEngine.SceneManagement.SceneManager.LoadScene(LoginManager.DefaultSettings.sceneToLoad);
                    }
                    // Otherwise show the login window (which will auto-login with the token)
                    else
                    {
                        LoginManager.UI.loginWindow.Show();
                    }
                }
                else
                {
                    // Fallback in case token wasn't received
                    LoginManager.UI.loginWindow.Show();
                }
            }, "OK");

            registerButton.interactable = true;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            Close();
        }

        private void OnFailedToCreateAccount()
        {
            Execute("OnFailedToCreateAccount", new CallbackEventData());

            username.text = "";

            // Show specific error message if available
            if (!string.IsNullOrEmpty(lastServerError))
            {
                // Use the server's specific error message
                LoginManager.Notifications.userExists.Show(delegate (int result) { Show(); }, "OK", lastServerError);
            }
            else
            {
                // Use default message for user exists
                LoginManager.Notifications.userExists.Show(delegate (int result) { Show(); }, "OK");
            }

            registerButton.interactable = true;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            Close();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventHandler.Unregister("OnAccountCreated", OnAccountCreated);
            EventHandler.Unregister("OnFailedToCreateAccount", OnFailedToCreateAccount);
        }
    }
}