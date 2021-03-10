using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI; 
public class StartMenu : MonoBehaviour
{
	private int nextScene = 0; // the current first level in the build may need to change later if it gets altered 
	//public Button button;

	
	void Start()
	{
		// nextScene = SceneManager.GetActiveScene().buildIndex - 1; // if we want it to change to another scene depending on 
		// which one we want we can change it here 
		//button = GetComponent<Button>(); 
	}
	void Update()
	{
		if (Input.GetMouseButtonDown(0))//EventSystem.current.currentSelectedGameObject == button)
		{
			Debug.Log("Mouse is pressed down");
			SceneManager.LoadScene(nextScene); 
		}
	}
	
}
