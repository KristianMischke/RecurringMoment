using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuardMove : MonoBehaviour
{
    public float distLeft = 1f, distRight = 1f, speed = 1f;
    public bool movingRight = false;
    Vector2 startPos;
    float left, right;
    bool wall = false;

    private SpriteRenderer _spriteRenderer;

    // Start is called before the first frame update
    void Start()
    {
        startPos = gameObject.transform.position;
        left = startPos.x - distLeft;
        right = startPos.x + distRight;
	_spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!(gameObject.GetComponent<Shoot>().seen))
        {

	    _spriteRenderer.flipX = movingRight;

            if (movingRight)
            {
                gameObject.transform.Translate(Vector2.right * speed * Time.deltaTime);
                if (gameObject.transform.position.x >= right || wall)
                {
                    movingRight = false;
                }
            }
            else
            {
                gameObject.transform.Translate(Vector2.right * speed * Time.deltaTime * -1.0f);
                if (gameObject.transform.position.x <= left || wall)
                {
                    movingRight = true;
                }
            }

        }
        
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        foreach(var h in collision.contacts)
        {
            if((h.normal.x == 1 && !movingRight) || (h.normal.x == -1 && movingRight) && h.normal.y == 0)
            {
                movingRight = !movingRight;
                break;
            }
        }
    }

}
