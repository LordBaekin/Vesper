using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveLoadPlayerData : MonoBehaviour
{
	void Start()
	{
		LoadPlayerData();
	}

	public void LoadPlayerData()
	{
		string characterID = PlayerPrefs.GetString("CurrentCharacterID", "");
		if (!string.IsNullOrEmpty(characterID))
		{
			// Load saved scene
			string savedScene = PlayerPrefs.GetString(characterID + "_Scene", SceneManager.GetActiveScene().name);

			// If the saved scene is different from the current one, load the correct scene first
			if (savedScene != SceneManager.GetActiveScene().name)
			{
				SceneManager.LoadScene(savedScene);
				return; // Prevent setting position in the wrong scene
			}

			// Load saved position
			float x = PlayerPrefs.GetFloat(characterID + "_PosX", transform.position.x);
			float y = PlayerPrefs.GetFloat(characterID + "_PosY", transform.position.y);
			float z = PlayerPrefs.GetFloat(characterID + "_PosZ", transform.position.z);

			// Load saved rotation
			float rotX = PlayerPrefs.GetFloat(characterID + "_RotX", transform.rotation.eulerAngles.x);
			float rotY = PlayerPrefs.GetFloat(characterID + "_RotY", transform.rotation.eulerAngles.y);
			float rotZ = PlayerPrefs.GetFloat(characterID + "_RotZ", transform.rotation.eulerAngles.z);

			// Apply the saved position and rotation
			transform.position = new Vector3(x, y, z);
			transform.rotation = Quaternion.Euler(rotX, rotY, rotZ);
		}
	}

	public void SavePlayerData()
	{
		string characterID = PlayerPrefs.GetString("CurrentCharacterID", "");
		if (!string.IsNullOrEmpty(characterID))
		{
			// Save scene name
			PlayerPrefs.SetString(characterID + "_Scene", SceneManager.GetActiveScene().name);

			// Save position
			PlayerPrefs.SetFloat(characterID + "_PosX", transform.position.x);
			PlayerPrefs.SetFloat(characterID + "_PosY", transform.position.y);
			PlayerPrefs.SetFloat(characterID + "_PosZ", transform.position.z);

			// Save rotation
			PlayerPrefs.SetFloat(characterID + "_RotX", transform.rotation.eulerAngles.x);
			PlayerPrefs.SetFloat(characterID + "_RotY", transform.rotation.eulerAngles.y);
			PlayerPrefs.SetFloat(characterID + "_RotZ", transform.rotation.eulerAngles.z);

			PlayerPrefs.Save();
		}
	}

	void OnApplicationQuit()
	{
		SavePlayerData(); // Save when quitting the game
	}
}
