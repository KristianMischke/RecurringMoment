using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class Guard_AI : BasicTimeTracker
{

    public float distLeft = 1f, distRight = 1f, moveSpeed = 1f;
    public bool movingRight = false;
    public GameObject bullet;
    public float fireRateSeconds = 3f, shotSpeed = 1f, range = 5f, bulletLife = 1f;
    public GameObject gameController;
    public Canvas mainUIcanvas;
    public RetryPopup retryPopupPrefab;
    Vector2 startPos;
    float left, right, minDist, toFire = 0f, dist =0f;
    bool wall = false, seen = false;
    GameObject closest;
    
    private SpriteRenderer _spriteRenderer;

    // Start is called before the first frame update
    void Start()
    {
        startPos = gameObject.transform.position;
        left = startPos.x - distLeft;
        right = startPos.x + distRight;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public override void GameUpdate()
    {
        base.GameUpdate();
        if (!seen)
        {
            _spriteRenderer.flipX = movingRight;
            
            if (movingRight)
            {
                gameObject.transform.Translate(Vector2.right * moveSpeed * Time.deltaTime);
                if (gameObject.transform.position.x >= right || wall)
                {
                    movingRight = false;
                }
            }
            else
            {
                gameObject.transform.Translate(Vector2.right * moveSpeed * Time.deltaTime * -1.0f);
                if (gameObject.transform.position.x <= left || wall)
                {
                    movingRight = true;
                }
            }

        }
        if (Detected())
        {
            toFire -= Time.deltaTime;
            if (toFire <= 0)
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        foreach (var h in collision.contacts)
        {
            if ((h.normal.x == 1 && !movingRight) || (h.normal.x == -1 && movingRight) && h.normal.y == 0)
            {
                movingRight = !movingRight;
                break;
            }
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

            if (dist < minDist && ((angle <= 45 && movingRight) || (angle >= 135 && !movingRight)))
            {
                minDist = dist;
                closest = p.gameObject;
            }
        }

        dist = Mathf.Abs(Vector2.Distance(gameObject.transform.position, gameController.GetComponent<GameController>().player.transform.position));
        direc = gameController.GetComponent<GameController>().player.transform.position - gameObject.transform.position;
        angle = Mathf.Atan2(direc.y, direc.x) * Mathf.Rad2Deg;
        angle = Mathf.Abs(angle);
        if (dist < minDist && ((angle <= 45 && movingRight) || (angle >= 135 && !movingRight)))
        {
            minDist = dist;
            closest = gameController.GetComponent<GameController>().player.gameObject;
        }
        if (!seen && (minDist <= range))
        {
            StartCoroutine(Alerted());
        }
        seen = (minDist <= range);
        return seen;

    }

    IEnumerator Alerted()
    {

        transform.GetChild(0).gameObject.SetActive(true);
        for (int i = 0; i < 45; i++)
        {
            yield return null;
        }
        transform.GetChild(0).gameObject.SetActive(false);

    }

    public void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        if (EditorApplication.isPlaying)
        {
            Gizmos.DrawLine(new Vector3(left, startPos.y, 0), new Vector3(right, startPos.y, 0));
        }
        else
        {
            Gizmos.DrawLine(new Vector3(transform.position.x - distLeft, transform.position.y, 0), new Vector3(transform.position.x + distRight, transform.position.y, 0));
        }
    }
}
