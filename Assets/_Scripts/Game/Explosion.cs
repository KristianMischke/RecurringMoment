using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]


public class Explosion : BasicTimeTracker
{
	public float radius;
	public int lifetime;
	
	public ParticleSystem blastZone;
	public CircleCollider2D explosionArea;
	
	private LineRenderer _lineRenderer;
	public Material fire;
	public Material smoke; 

	public LineRenderer LineRenderer
	{
		get
		{
			if (_lineRenderer == null)
			{
				_lineRenderer = GetComponent<LineRenderer>();
			}

			return _lineRenderer;
		}
	}
	public int destroyStep = -1;
	
	public override bool ShouldPoolObject => true;
	public override bool SetItemState(bool state) => false;

	public void DrawExplosion()
	{
		// stting up the particle system instead 
		blastZone = GetComponent<ParticleSystem>();
		explosionArea = GetComponent<CircleCollider2D>();
		explosionArea.radius = radius;
		
		LineRenderer.startColor = Color.red;
		LineRenderer.endColor = Color.red;
		
		LineRenderer.useWorldSpace = false; 
		LineRenderer.positionCount = 361; // all of the degrees plus one to make the circle 
		Vector3 [] explosionCircle = new Vector3[361];
		for (int x = 0; x < 361; x++)
		{
			var rad = Mathf.Deg2Rad * (x * 360f / 360);
			explosionCircle[x] = new Vector3(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius, 0); 
		}
		
		LineRenderer.SetPositions(explosionCircle);
		LineRenderer.loop = true; // make it connect at the end
	}

	public override void GameUpdate()
	{
		base.GameUpdate();

		// shrink radius on time
		explosionArea.radius = Mathf.Lerp(0, radius, (destroyStep - (float)gameController.TimeStep) / lifetime);
		
		// if the game is past or at the frame we disappear, destroy us 
		if (gameController.TimeStep >= destroyStep)
		{
			// HACK: remove this object from the history
			gameController.SetSnapshotValue(this, 0, GameController.FLAG_DESTROY, true, true, true);
			
			FlagDestroy = true;
			gameController.SaveObjectToPool(this); // manually save to pool, because we aren't going to save it in time
		}
	}

	public override void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force = false)
	{
		base.SaveSnapshot(snapshotDictionary, force);
		snapshotDictionary.Set(nameof(destroyStep), destroyStep, force);
	}

	public override void PreUpdateLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
	{
		base.PreUpdateLoadSnapshot(snapshotDictionary);
		destroyStep = snapshotDictionary.Get<int>(nameof(destroyStep));
	}

	public override void ForceRestoreSnapshot(TimeDict.TimeSlice snapshotDictionary)
	{
		base.ForceRestoreSnapshot(snapshotDictionary);
		destroyStep = snapshotDictionary.Get<int>(nameof(destroyStep));
	}
}
