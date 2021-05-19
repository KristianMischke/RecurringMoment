using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class DoorController : BasicTimeTracker
{
    public List<ActivatableBehaviour> requiredActivatables = new List<ActivatableBehaviour>();

    [SerializeField] float slideTime;
    [SerializeField] float offset;

    bool doorMoving = false;
    float timer;
    Vector2 originalPos;

    [SerializeField] GameObject OpenObject;
    [SerializeField] GameObject ClosedObject;

    private BoxCollider2D collider;
    private Vector2 _colliderDefaults;

    private void Start()
    {
        originalPos = gameObject.transform.Find("Art/MovingDoor").position;
	    collider = GetComponentInChildren<BoxCollider2D>();
	    _colliderDefaults = collider.size;
    }

    void Update()
    {
        gameObject.transform.Find("Art/MovingDoor").position = Vector3.Lerp(originalPos, originalPos + Vector2.up * offset, timer/slideTime);
	    collider.size = Vector2.Lerp(_colliderDefaults, new Vector2(_colliderDefaults.x, 0), timer/slideTime);
	    collider.offset = Vector2.Lerp(Vector2.zero, Vector2.down * offset / 2, timer/slideTime);
    }

    public override void GameUpdate()
    {
        if (AllActivated())
        {
            timer += Time.fixedDeltaTime;
        }
        else
        {
            timer -= Time.fixedDeltaTime;
        }
        timer = Mathf.Clamp(timer, 0, slideTime);
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

    public override bool ShouldPoolObject => false;
    
    public override void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force = false)
    {
        snapshotDictionary.Set(nameof(timer), timer, force:force, clearFuture:true);
    }

    public override void PreUpdateLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        timer = snapshotDictionary.Get<float>(nameof(timer));
    }

    public override void ForceRestoreSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        timer = snapshotDictionary.Get<float>(nameof(timer));
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
