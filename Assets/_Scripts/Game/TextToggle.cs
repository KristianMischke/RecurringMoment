using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TextToggle : MonoBehaviour
{
	private GameController gameController;

	public GameObject childObject;

    void Start()
    {
        gameController = (GameController)GameObject.FindObjectsOfType(typeof(GameController))[0];
        childObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
		if(other == gameController.player.CapsuleCollider)
		{
			childObject.SetActive(true);
		}
    }

    void OnTriggerExit2D(Collider2D other)
    {
		if(other == gameController.player.CapsuleCollider)
		{
			childObject.SetActive(false);	
		}	
    }
}
