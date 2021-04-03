using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectLife : MonoBehaviour
{

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
        if (collision.gameObject.tag == "Player")
        {
            Destroy(gameObject);
            Destroy(collision.gameObject);

            //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            Debug.Log("ouch");
            var popup = Instantiate(retryPrefab, mainUIcanvas.transform);
            popup.Init("Uh oh!", "You/past you got SHOT", controller.RetryLevel, controller.RespawnLatest);
        }
        else if(collision.GetType() ==typeof(BoxCollider2D))
        {
            Destroy(gameObject);
        }

    }

}
