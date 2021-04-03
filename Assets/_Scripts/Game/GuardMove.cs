using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuardMove : MonoBehaviour
{
    public float distLeft = 0f, distRight = 0f, speed = 0f;
    public bool movingRight = false;
    Vector2 startPos;
    float left, right;

    // Start is called before the first frame update
    void Start()
    {
        startPos = gameObject.transform.position;
        left = startPos.x - distLeft;
        right = startPos.x + distRight;
    }

    // Update is called once per frame
    void Update()
    {
        if (!(gameObject.GetComponent<Shoot>().seen))
        {

            if (movingRight)
            {
                gameObject.transform.Translate(Vector2.right * speed * Time.deltaTime);
                if (gameObject.transform.position.x >= right)
                {
                    movingRight = false;
                }
            }
            else
            {
                gameObject.transform.Translate(Vector2.right * speed * Time.deltaTime * -1.0f);
                if (gameObject.transform.position.x <= left)
                {
                    movingRight = true;
                }
            }

        }
        
    }

}
