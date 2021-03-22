using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraTracker : MonoBehaviour
{

    private GameController gameController;
    public Vector2 min, max;

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
    }
}
