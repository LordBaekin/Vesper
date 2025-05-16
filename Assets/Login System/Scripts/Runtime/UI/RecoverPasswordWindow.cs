using DevionGames.UIWidgets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DevionGames.LoginSystem
{
    public class RecoverPasswordWindow : UIWidget
    {
        public override string[] Callbacks
        {
            get
            {
                List<string> callbacks = new List<string>(base.Callbacks);
                callbacks.Add("OnPasswordRecovered");
                callbacks.Add("OnFailedToRecoverPassword");
                return callbacks.ToArray();
            }
        }

        [Header("Reference")]
        [SerializeField]
        protected InputField email;
        [SerializeField]
        protected Button recoverButton;
        [SerializeField]
        protected GameObject loadingIndicator;

        // Track server errors for detailed feedback
        protected string lastServerError;

        protected override void OnStart()
        {
            base.OnStart();
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            EventHandler.Register("OnPasswordRecovered", OnPasswordRecovered);
            EventHandler.Register("OnFailedToRecoverPassword", OnFailedToRecoverPassword);
            recoverButton.onClick.AddListener(RecoverPasswordUsingFields);

            // Clear fields when opening window
            email.text = "";
        }

        private void RecoverPasswordUsingFields()
        {
            // Reset any previous errors
            lastServerError = null;

            // Validate email format
            if (!LoginManager.ValidateEmail(email.text))
            {
                LoginManager.Notifications.invalidEmail.Show(delegate (int result) { Show(); }, "OK");
                Close();
                return;
            }

            recoverButton.interactable = false;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }

            // Call password recovery in LoginManager
            LoginManager.RecoverPassword(email.text);
        }

        private void OnPasswordRecovered()
        {
            Execute("OnPasswordRecovered", new CallbackEventData());

            // Your Vespeyr server sends reset emails instead of revealing if email exists
            // We can update the message to reflect this
            LoginManager.Notifications.passwordRecovered.Show(
                delegate (int result) { LoginManager.UI.loginWindow.Show(); },
                "OK",
                "If your email exists in our system, you'll receive instructions to reset your password. Please check your inbox."
            );

            recoverButton.interactable = true;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            // Clear email field for privacy
            email.text = "";

            Close();
        }

        private void OnFailedToRecoverPassword()
        {
            Execute("OnFailedToRecoverPassword", new CallbackEventData());

            // For security reasons, don't reveal if account exists or not
            // Instead show a generic message that's the same as success
            // This prevents email enumeration attacks
            LoginManager.Notifications.passwordRecovered.Show(
                delegate (int result) { LoginManager.UI.loginWindow.Show(); },
                "OK",
                "If your email exists in our system, you'll receive instructions to reset your password. Please check your inbox."
            );

            recoverButton.interactable = true;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            // Clear email field for privacy
            email.text = "";

            Close();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventHandler.Unregister("OnPasswordRecovered", OnPasswordRecovered);
            EventHandler.Unregister("OnFailedToRecoverPassword", OnFailedToRecoverPassword);
        }
    }
}