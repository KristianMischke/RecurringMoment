using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CameraTracker : MonoBehaviour
{

    private GameController gameController;
    private Camera _camera;
	private GameObject midGround; 

    public Camera Camera
    {
        get
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            return _camera;
        }
    }
    
    public Vector2 relativeMin, relativeMax;
    private Vector2 startPos;
	
	[SerializeField]public float percentChange = .2f;
	private Vector3 midGroundPos;
	private Vector3 oldCamPosition;
	private Vector3 newCamPosition; 

    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
		oldCamPosition = transform.position; 
		newCamPosition = transform.position;
        gameController = (GameController)GameObject.FindObjectsOfType(typeof(GameController))[0];
		Component [] allGrounds = GetComponentsInChildren<SpriteRenderer>();
		foreach (var ob in allGrounds)
		{
			if(ob.gameObject.name == "Mid-Ground")
			{
				Debug.Log("Found the midground"); 
				midGround = ob.gameObject;
				midGroundPos = midGround.transform.position;
			}
		}
    }

    // Update is called once per frame
    void Update()
    {
        if (gameController.TimeStep > 0)
        {
            Vector2 playerPos = gameController.GetSnapshotValue<Vector2>(gameController.Player, gameController.TimeStep,
                gameController.Player.Position.CurrentName, defaultValue:Vector2.positiveInfinity);

            if (float.IsPositiveInfinity(playerPos.x) && float.IsPositiveInfinity(playerPos.y))
            {
                playerPos = gameController.Player.Position.Current;
            }
            
			oldCamPosition = this.transform.position; 
            //Follow the player's position
            this.transform.position = new Vector3(
                Mathf.Clamp(playerPos.x, relativeMin.x + startPos.x, relativeMax.x + startPos.x),
                Mathf.Clamp(playerPos.y, relativeMin.y + startPos.y, relativeMax.y + startPos.y),
                -10);
				
				
			newCamPosition = this.transform.position;
			Vector3 midGroundChange = newCamPosition - oldCamPosition; 
			midGroundChange.x = midGroundChange.x * percentChange;
			midGroundChange.y = midGroundChange.y * percentChange;
			midGroundChange.z = midGroundChange.z * percentChange; 
			//oldCamPosition = this.transform.position; 
			//Vector3 startPos3 = new Vector3(startPos.x, startPos.y, 0);
			//Vector3 camDifference = (midGroundChange - startPos3); //* percentChange;
			if (midGround != null)
			{
				midGround.transform.position -= midGroundChange;
			}


			//midGround.transform.position = new Vector3( ((startPos.x - midGroundChange.x) * percentChange) + midGroundPos.x,
			//((startPos.y - midGroundChange.y) * percentChange) + midGroundPos.y,
			//((startPos.z - midGroundChange.z) * percentChange) + midGroundPos.z );
			//midGround.transform.position.y = (startPos.y - midGroundChange.y) * percentChange;
        }
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        var lower = Camera.ViewportToWorldPoint(new Vector3(0, 0, Camera.nearClipPlane));
        var upper = Camera.ViewportToWorldPoint(new Vector3(1, 1, Camera.nearClipPlane));
        var radius = (upper - lower) / 2;
        lower += (Vector3)relativeMin;
        upper += (Vector3)relativeMax;

        Vector2 size = upper - lower;
        Vector3 center = gameController != null && gameController.TimeStep > 0 ? (Vector3)startPos: transform.position;
        center += (Vector3)relativeMax/2 + (Vector3)relativeMin/2;
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
