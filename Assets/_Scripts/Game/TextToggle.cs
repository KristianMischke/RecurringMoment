using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TextToggle : MonoBehaviour
{
    
    private GameController gameController;

    void Start()
    {
        gameController = (GameController)GameObject.FindObjectsOfType(typeof(GameController))[0];
	gameObject.GetComponent<TextMeshProUGUI>().enabled = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
	if(other == gameController.player.CapsuleCollider)
	{
	    gameObject.GetComponent<TextMeshProUGUI>().enabled = true;
	}
    }

    void OnTriggerExit2D(Collider2D other)
    {
	if(other == gameController.player.CapsuleCollider)
	{
	    gameObject.GetComponent<TextMeshProUGUI>().enabled = false;	
	}	
    }
}
