using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI; 
public class StartMenu : MonoBehaviour
{
	private int nextScene = 0; // the current first level in the build may need to change later if it gets altered 
	public Button button;
	void Start()
	{
		button.onClick.AddListener(NextLevel);
		
	}
	
	void NextLevel()
	{
		Debug.Log("You have clicked the button"); 
		SceneManager.LoadScene(nextScene); 
	}
	
}
