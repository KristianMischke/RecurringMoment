using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ButtonController : ActivatableBehaviour
{
    public GameObject PressedArt;
    public GameObject NormalArt;

    private HashSet<GameObject> triggeringObjects = new HashSet<GameObject>();

    public int collisionCount;

    public override bool IsActivated => triggeringObjects.Count >= collisionCount;

    public void UpdateArt()
    {
        PressedArt.SetActive(IsActivated);
        NormalArt.SetActive(!IsActivated);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("TriggerObject") || collision.gameObject.CompareTag("Guard"))
        {
            triggeringObjects.Add(collision.gameObject);
        }
        UpdateArt();
    }
    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("TriggerObject") || collision.gameObject.CompareTag("Guard"))
        {
            triggeringObjects.Remove(collision.gameObject);
        }
        UpdateArt();
    }
}
