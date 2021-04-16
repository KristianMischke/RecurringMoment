using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CameraTracker : MonoBehaviour
{

    private GameController gameController;
    private Camera _camera;

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

    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
        gameController = (GameController)GameObject.FindObjectsOfType(typeof(GameController))[0];
    }

    // Update is called once per frame
    void Update()
    {
        if (gameController.TimeStep > 0)
        {
            Vector2 playerPos = gameController.GetSnapshotValue<Vector2>(gameController.player, gameController.TimeStep,
                gameController.player.Position.CurrentName, defaultValue:Vector2.positiveInfinity);

            if (float.IsPositiveInfinity(playerPos.x) && float.IsPositiveInfinity(playerPos.y))
            {
                playerPos = gameController.player.Position.Current;
            }
            
            //Follow the player's position
            this.transform.position = new Vector3(
                Mathf.Clamp(playerPos.x, relativeMin.x + startPos.x, relativeMax.x + startPos.x),
                Mathf.Clamp(playerPos.y, relativeMin.y + startPos.y, relativeMax.y + startPos.y),
                -10);
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
