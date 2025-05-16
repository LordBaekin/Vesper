using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevionGames.LoginSystem.Configuration
{
    [System.Serializable]
    public class Server : Settings
    {
        public override string Name
        {
            get
            {
                return "Server";
            }
        }

        [Header("Server Settings:")]
        public string serverAddress = "http://localhost:5000/";

        [Header("Authentication Endpoints:")]
        public string createAccount = "auth/register";
        public string login = "auth/login";
        public string recoverPassword = "auth/request-password-reset";
        public string resetPassword = "auth/reset-password";
        public string refreshToken = "auth/refresh";

        [Header("Legacy Settings:")]
        public string accountKey = "Account";  // Still used for backward compatibility
    }
}