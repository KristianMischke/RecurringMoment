using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteInEditMode]
public class Guard_AI : BasicTimeTracker
{

    public float distLeft = 1f, distRight = 1f, moveSpeed = 1f;
    public bool movingRight = false;
    public GameObject bullet;
    public float fireRateSeconds = 3f, shotSpeed = 1f, range = 5f, bulletLife = 1f;
    public Canvas mainUIcanvas;
    public RetryPopup retryPopupPrefab;
    Vector2 startPos;
    float left, right, minDist, toFire = 0f, dist =0f;
    bool wall = false, seen = false;
    GameObject closest;
    private bool _alertState = false;
    
    [SerializeField] private AudioClip _blastSound;
    [SerializeField] private AudioClip _alertSound;

    private SpriteRenderer _spriteRenderer;

    // Start is called before the first frame update
    void Start()
    {
        startPos = gameObject.transform.position;
        left = startPos.x - distLeft;
        right = startPos.x + distRight;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public override bool ShouldPoolObject => false;

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
        if (_alertState)
        {
            toFire -= Time.deltaTime;
            if (toFire <= 0)
            {
                if(Detected())
		        {
                    Blast(closest.GetComponent<CapsuleCollider2D>());
                    AudioSource.PlayClipAtPoint(_blastSound, Camera.main.transform.position, 0.1f);
                    toFire = fireRateSeconds;
		        }
		        _alertState = false;
            }
        }
        else
        {
	        _alertState = Detected();
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
        b.GetComponent<ShotLife>().Init(gameController.GetComponent<GameController>(), mainUIcanvas, retryPopupPrefab, bulletLife);
        b.GetComponent<LineRenderer>().SetPosition(0, gameObject.transform.position);
        b.GetComponent<LineRenderer>().SetPosition(1, target.transform.position);
        b.GetComponent<CircleCollider2D>().offset = target.transform.position - gameObject.transform.position;

    }

    bool Detected()
    {

        minDist = Mathf.Infinity;
        float angle = 0f;
        Vector3 direc = Vector3.zero;
        //RaycastHit2D hit = new RaycastHit2D();
        foreach (var p in gameController.GetComponent<GameController>().AllPlayers)
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
        
        if (!seen && (minDist <= range))
        {
            StartCoroutine(Alerted());
        }
        seen = (minDist <= range);
        return seen;

    }

    IEnumerator Alerted()
    {
	    AudioSource.PlayClipAtPoint(_alertSound, Camera.main.transform.position, 0.2f);
        transform.GetChild(0).gameObject.SetActive(true);
        for (int i = 0; i < 45; i++)
        {
            yield return null;
        }
        transform.GetChild(0).gameObject.SetActive(false);

    }

    public override void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force = false)
    {
        base.SaveSnapshot(snapshotDictionary, force);
        snapshotDictionary.Set(nameof(movingRight), movingRight);
        snapshotDictionary.Set(nameof(toFire), toFire);
        snapshotDictionary.Set(nameof(_alertState), _alertState);
    }

    public override void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        base.LoadSnapshot(snapshotDictionary);
    }
    
    public override void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        base.ForceLoadSnapshot(snapshotDictionary);

        movingRight = snapshotDictionary.Get<bool>(nameof(movingRight));
        toFire = snapshotDictionary.Get<float>(nameof(toFire));
        _alertState = snapshotDictionary.Get<bool>(nameof(_alertState));
    }

#if UNITY_EDITOR
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
#endif
}
