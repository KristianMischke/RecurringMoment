using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplodeBox : BasicTimeTracker
{
	public List<ActivatableBehaviour> requiredActivatables = new List<ActivatableBehaviour>();
	[SerializeField] float distance = 3;

	void FixedUpdate()
    {
        if (AllActivated())
        {
			Vector2 loc = transform.position;
			Debug.Log("The location is : " + loc.x + "and "+ loc.y);
			
			List<RaycastHit2D> hits = new List<RaycastHit2D>();
			hits.AddRange(Physics2D.RaycastAll(loc, Vector2.up, distance)); 
			hits.AddRange(Physics2D.RaycastAll(loc, Vector2.down, distance)); 
			hits.AddRange(Physics2D.RaycastAll(loc, Vector2.left, distance)); 
			hits.AddRange(Physics2D.RaycastAll(loc, Vector2.right, distance)); 
			
			// gets the diagonal angles as well
			float angle = 45.0f;
			hits.AddRange(Physics2D.RaycastAll(loc, GetDirectionVector2D(angle), distance));
			angle = 135.0f;
			hits.AddRange(Physics2D.RaycastAll(loc, GetDirectionVector2D(angle), distance)); 
			angle = 225.0f;
			hits.AddRange(Physics2D.RaycastAll(loc, GetDirectionVector2D(angle), distance)); 
			angle = 315.0f;
			hits.AddRange(Physics2D.RaycastAll(loc, GetDirectionVector2D(angle), distance)); 

			foreach(var hit in hits)
			{
				Debug.Log("The collider hit is :" + hit.collider.gameObject.tag);
				
				// get the time tracker from the object or its parent(s)
				ITimeTracker timeTracker = GameController.GetTimeTrackerComponent(hit.collider.gameObject, checkParents:true);
				
				if (timeTracker != null && timeTracker.gameObject.CompareTag("ExplodeWall"))
				{
					timeTracker.FlagDestroy = true;
				}
				else if (hit.collider.gameObject.CompareTag("ExplodeWall"))
				{
					hit.collider.gameObject.SetActive(false);
					Debug.LogWarning($"[ExploadBox] Warning: setting {hit.collider.gameObject.name} to inactive, but this object has no {nameof(ITimeTracker)} so it won't be recorded in time");
				}
			}

			FlagDestroy = true; // mark object for destruction in time
        }
    }

	public Vector2 GetDirectionVector2D(float angle)
	{
		return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)).normalized; 
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
}
