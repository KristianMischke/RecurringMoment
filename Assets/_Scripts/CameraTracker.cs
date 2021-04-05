using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraTracker : MonoBehaviour
{

    private GameController gameController;
    public Vector2 relativeMin, relativeMax;
    private Vector2 startPos;

    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
        gameController = (GameController)GameObject.FindObjectsOfType(typeof(GameController))[0];
    }

    // Update is called once per frame
    void Update()
    {
	     //Follow the player's position
         this.transform.position = new Vector3(
             Mathf.Clamp(gameController.player.transform.position.x, relativeMin.x + startPos.x, relativeMax.x + startPos.x),
             Mathf.Clamp(gameController.player.transform.position.y, relativeMin.y + startPos.y, relativeMax.y + startPos.y),
             -10);
    }
}
