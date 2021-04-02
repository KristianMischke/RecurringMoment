using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraTracker : MonoBehaviour
{

    private GameController gameController;
    public Vector2 min, max;
	public GameObject itemShow; 
	public int xOffSet, yOffSet; 

    // Start is called before the first frame update
    void Start()
    {
        gameController = (GameController)GameObject.FindObjectsOfType(typeof(GameController))[0];
    }

    // Update is called once per frame
    void Update()
    {
	     //Follow the player's position
        this.transform.position = new Vector3 (gameController.player.transform.position.x, gameController.player.transform.position.y, -10);
		itemShow.transform.position = new Vector3 (this.transform.position.x + xOffSet, this.transform.position.y + yOffSet, 0);
    }
}
