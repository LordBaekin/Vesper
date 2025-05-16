using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevionGames
{
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, null, "Assembly-CSharp")]
    [ComponentMenu("SceneManager/Load Scene")]
    public class LoadScene : Action
    {
        [SerializeField]
        private string m_Scene=string.Empty;

        public override ActionStatus OnUpdate()
        {
            Scene CurrentScene = SceneManager.GetActiveScene();
            if (CurrentScene.name != this.m_Scene)
            {
                SceneManager.LoadScene(this.m_Scene);
            }
            return ActionStatus.Success;
        }
    }
}