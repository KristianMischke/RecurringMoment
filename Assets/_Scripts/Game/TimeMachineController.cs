using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeMachineController : MonoBehaviour
{
    private HashSet<GameObject> triggeringObjects = new HashSet<GameObject>();

    public bool IsTouching(GameObject other)
    {
        return triggeringObjects.Contains(other);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            triggeringObjects.Add(collision.gameObject);
        }
    }
    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            triggeringObjects.Remove(collision.gameObject);
        }
    }
}
