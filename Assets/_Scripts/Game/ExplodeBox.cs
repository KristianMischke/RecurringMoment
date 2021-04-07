﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class ExplodeBox : BasicTimeTracker
{
	public List<int> requiredActivatableIDs = new List<int>();
	public List<ActivatableBehaviour> requiredActivatables = new List<ActivatableBehaviour>();
	[SerializeField] float distance = 3;

	public override void Init(GameController gameController, int id)
	{
		base.Init(gameController, id);

		// Gather the ICustomObject of the activatables so we don't lose track of them after time travelling
		requiredActivatableIDs.Clear();
		foreach (var activatable in requiredActivatables)
		{
			Assert.IsNotNull(activatable);
			requiredActivatableIDs.Add(activatable.ID);
		}
	}

	public override void CopyTimeTrackerState(ITimeTracker other)
	{
		ExplodeBox otherBox = other as ExplodeBox;
		if (otherBox != null)
		{
			Position.Copy(otherBox.Position);
			ItemForm.Copy(otherBox.ItemForm);

			_shouldPoolObject = otherBox._shouldPoolObject;
			_isItemable = otherBox._isItemable;

			distance = otherBox.distance;
			requiredActivatableIDs.AddRange(otherBox.requiredActivatableIDs);
			requiredActivatables.AddRange(otherBox.requiredActivatables);
		}
		else
		{
			gameController.LogError($"Cannot copy state from {other.GetType()} to {nameof(ExplodeBox)}");
		}
	}

	public override void OnPoolRelease()
	{
		base.OnPoolRelease();
		requiredActivatables.Clear();
		requiredActivatableIDs.Clear();
	}

	public override void GameUpdate()
    {
	    if (AllActivated())
	    {
		    Vector2 loc = transform.position;
		    Debug.Log("The location is : " + loc.x + "and " + loc.y);

		    // get list of angles we wish to cast @ 15 degree increments
		    List<Vector2> angles = new List<Vector2>();
		    for (int i = 0; i < 360; i += 15)
		    {
			    angles.Add(GetDirectionVector2D(i));			    
		    }

		    foreach (var angle in angles)
		    {
			    // get hits for this angle
			    List<RaycastHit2D> hits = new List<RaycastHit2D>();
			    hits.AddRange(Physics2D.RaycastAll(loc, angle, distance)); 

			    foreach(var hit in hits)
			    {
				    if (hit.collider.gameObject == this.gameObject) continue; // skip if we hit our own collider
				    
				    Debug.Log("The collider hit is :" + hit.collider.gameObject.tag);

				    // block the explosion if it hits a platform
				    bool blockExplosion = hit.collider.gameObject.layer == LayerMask.NameToLayer("LevelPlatforms");

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

				    if (blockExplosion) // break the hits loop if we encountered a platform
				    {
					    break;
				    }
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

    private string prevActivatableString = null;
    private void LoadActivatables(TimeDict.TimeSlice snapshotDictionary)
    {
	    string newActivatableString = snapshotDictionary.Get<string>(nameof(requiredActivatableIDs));
	    if (prevActivatableString != newActivatableString)
	    {
		    prevActivatableString = newActivatableString;
		    
		    requiredActivatables.Clear();
		    requiredActivatableIDs.Clear();
		    
		    string[] activatableStringIDs = newActivatableString.Split(',');
		    foreach (var stringID in activatableStringIDs)
		    {
			    if (int.TryParse(stringID, out int id))
			    {
				    var activatableBehaviour = gameController.GetObjectByID(id) as ActivatableBehaviour;
				    Assert.IsNotNull(activatableBehaviour);
				    requiredActivatables.Add(activatableBehaviour);
				    requiredActivatableIDs.Add(id);
			    }
		    }
	    }
    }
    
    public override void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force = false)
    {
	    base.SaveSnapshot(snapshotDictionary, force);
	    
	    snapshotDictionary.Set(nameof(requiredActivatableIDs), string.Join(",", requiredActivatableIDs), force:force);
    }

    public override void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
	    base.LoadSnapshot(snapshotDictionary);
		LoadActivatables(snapshotDictionary);
    }
    public override void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
	    base.ForceLoadSnapshot(snapshotDictionary);
	    LoadActivatables(snapshotDictionary);
    }
}
