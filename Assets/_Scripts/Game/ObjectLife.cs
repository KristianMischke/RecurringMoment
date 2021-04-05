using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectLife : MonoBehaviour
{
    //TODO: rename Bullet or somthing more representative
    //TODO: bullets probably need to be time tracked... but that's tricky because they are spawned in time by another
    //      entity... so I'll need to think about this

    public float timeActive = 2.0f;
    public GameController controller;
    public Canvas mainUIcanvas;
    public RetryPopup retryPrefab;
    float currentFrame = 0;
    

    // Start is called before the first frame update
    void Start()
    {

    }

    public void Init(GameController gc, Canvas c, RetryPopup rp, float t = 2.0f)
    {
        timeActive = t;
        controller = gc;
        mainUIcanvas = c;
        retryPrefab = rp;
    }

    // Update is called once per frame
    void Update()
    {
        currentFrame += Time.deltaTime;
        if (currentFrame >= timeActive)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.FlagDestroy = true; // NOTE: probably want to move this to the "critical" section for time variable manipulation
            Debug.Log("ouch");
        }
        else if(collision.GetType() ==typeof(BoxCollider2D) && !(collision.isTrigger))
        {
            Destroy(gameObject);
        }

    }

}
