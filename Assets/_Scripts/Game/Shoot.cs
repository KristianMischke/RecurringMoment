using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shoot : MonoBehaviour
{
    public GameObject bullet, player;
    public float fireRateSeconds = 3f, shotSpeed = 1f, range = 5f;
    float dist = 0, toFire = 0;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        dist = Mathf.Abs(Vector2.Distance(gameObject.transform.position, player.transform.position));
        toFire -= Time.deltaTime;
        if(dist <= range && toFire <= 0)
        {
            Blast(player.GetComponent<CapsuleCollider2D>());
            toFire = fireRateSeconds;
        }
        
    }


    void Blast(Collider2D target)
    {

        GameObject b = Instantiate(bullet, gameObject.transform.position, gameObject.transform.rotation);
        Rigidbody2D r = b.GetComponent<Rigidbody2D>();
        r.velocity = (target.gameObject.transform.position - gameObject.transform.position).normalized * shotSpeed;

    }
}
