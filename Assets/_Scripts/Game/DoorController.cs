using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    public List<ActivatableBehaviour> requiredActivatables = new List<ActivatableBehaviour>();

    [SerializeField] bool slideUp;
    [SerializeField] float slideTime;
    [SerializeField] float offset;

    bool doorMoving = false;
    float timer;
    Vector2 originalPos;

    [SerializeField] GameObject OpenObject;
    [SerializeField] GameObject ClosedObject;

    private void Start()
    {
        originalPos = gameObject.transform.Find("Art/MovingDoor").position;
    }

    void Update()
    {
        if (AllActivated())
        {
            timer += Time.deltaTime;
        }
        else
        {
            timer -= Time.deltaTime;
        }
        timer = Mathf.Clamp(timer, 0, slideTime);

        gameObject.transform.Find("Art/MovingDoor").position = Vector3.Lerp(originalPos, originalPos + Vector2.up * (offset * (slideUp ? 1 : -1)), timer/slideTime);
    }

    private bool AllActivated()
    {
        bool valid = true;
        foreach (ActivatableBehaviour activatable in requiredActivatables)
        {
            valid &= activatable.IsActivated;
        }

        return valid;
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        foreach (var activatable in requiredActivatables)
        {
            if (activatable != null)
            {
                Gizmos.DrawLine(transform.position, activatable.gameObject.transform.position);
            }
        }
    }
#endif
}
