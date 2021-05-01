using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shoot : MonoBehaviour
{
    public GameObject bullet;
    public float fireRateSeconds = 3f, shotSpeed = 1f, range = 5f, bulletLife = 1f;
    public GameObject gameController;
    public Canvas mainUIcanvas;
    public RetryPopup retryPopupPrefab;
    [HideInInspector]
    public bool seen = false;
    float minDist;
    float dist = 0f, toFire = 0f;

    GameObject closest;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        if(Detected())
        {
            toFire -= Time.deltaTime;
            if(toFire <= 0)
            {
                Blast(closest.GetComponent<CapsuleCollider2D>());
                toFire = fireRateSeconds;
            }
        }
        else
        {
            toFire = fireRateSeconds;
        }
        
    }


    void Blast(Collider2D target)
    {

        GameObject b = Instantiate(bullet, gameObject.transform.position, gameObject.transform.rotation);
        b.GetComponent<ObjectLife>().Init(gameController.GetComponent<GameController>(), mainUIcanvas, retryPopupPrefab, bulletLife);
        Rigidbody2D r = b.GetComponent<Rigidbody2D>();
        r.velocity = (target.gameObject.transform.position - gameObject.transform.position).normalized * shotSpeed;

    }

    bool Detected()
    {

        minDist = Mathf.Infinity;
        float angle = 0f;
        Vector3 direc = Vector3.zero;
        //RaycastHit2D hit = new RaycastHit2D();
        foreach (var p in gameController.GetComponent<GameController>().PastPlayers)
        {
            dist = Mathf.Abs(Vector2.Distance(gameObject.transform.position, p.transform.position));
            direc = p.transform.position - gameObject.transform.position;
            angle = Mathf.Atan2(direc.y, direc.x) * Mathf.Rad2Deg;
            angle = Mathf.Abs(angle);

            if (dist < minDist && (angle <= 45 || angle >= 135))
            {
                minDist = dist;
                closest = p.gameObject;
            }
        }

        dist = Mathf.Abs(Vector2.Distance(gameObject.transform.position, gameController.GetComponent<GameController>().Player.transform.position));
        direc = gameController.GetComponent<GameController>().Player.transform.position - gameObject.transform.position;
        angle = Mathf.Atan2(direc.y, direc.x) * Mathf.Rad2Deg;
        angle = Mathf.Abs(angle);
        if (dist < minDist && (angle <= 45 || angle >= 135))
        {
            minDist = dist;
            closest = gameController.GetComponent<GameController>().Player.gameObject;
        }
        if(!seen && (minDist <= range))
        {
            StartCoroutine(Alerted()) ;
        }
        seen = (minDist <= range);
        return seen;

    }

    IEnumerator Alerted()
    {

        transform.GetChild(0).gameObject.SetActive(true);
        for(int i = 0; i < 45; i++)
        {
            yield return null;
        }
        transform.GetChild(0).gameObject.SetActive(false);

    }
}
