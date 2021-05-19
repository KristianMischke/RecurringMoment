using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ButtonController : ActivatableBehaviour
{
    public GameObject PressedArt;
    public GameObject NormalArt;
    public GameObject HalfPressArt;

    private HashSet<GameObject> triggeringObjects = new HashSet<GameObject>();

    public int collisionCount;

    public override bool IsActivated => triggeringObjects.Count >= collisionCount;

    [SerializeField] private AudioClip _buttonClick;
    private bool _clickReady = true;

    public void UpdateArt()
    {
        PressedArt.SetActive(IsActivated);
        NormalArt.SetActive(!IsActivated && triggeringObjects.Count == 0);
        HalfPressArt.SetActive(!IsActivated && triggeringObjects.Count > 0);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("TriggerObject") || collision.gameObject.CompareTag("Guard"))
        {
            triggeringObjects.Add(collision.gameObject);
        }
	if(IsActivated && _clickReady)
	{
	    AudioSource.PlayClipAtPoint(_buttonClick, Camera.main.transform.position, 0.6f);
	    _clickReady = false;
	}
        UpdateArt();
    }
    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("TriggerObject") || collision.gameObject.CompareTag("Guard"))
        {
            triggeringObjects.Remove(collision.gameObject);
        }
	if(!IsActivated)
	{
	    _clickReady = true;
	}
        UpdateArt();
    }
}
