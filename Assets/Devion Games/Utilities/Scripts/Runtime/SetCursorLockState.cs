using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevionGames
{
    public class SetCursorLockState : CallbackHandler
    {
        public KeyCode key = KeyCode.LeftControl;

        public override string[] Callbacks {
            get { return new string[] {"OnCursorLocked","OnCursorUnlocked" }; }
        }

        private void Update()
        {
            CursorLockMode CurrentMode = Cursor.lockState;

            if (Input.GetKey(key))
            {
                if (CurrentMode != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Execute("OnCursorUnlocked", new CallbackEventData());
                }
            }
            else
            {
                if (CurrentMode != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Execute("OnCursorLocked", new CallbackEventData());
                }
            }
        }
    }
}