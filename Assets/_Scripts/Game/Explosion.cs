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
		 
		 var em = blastZone.emission;
		 em.enabled = true;
		 em.type = ParticleSystemEmissionType.Time;
		 em.SetBursts(
		 new ParticleSystem.Burst[]{
			 new ParticleSystem.Burst(0.0f, 100),
			 new ParticleSystem.Burst(1.0f, 200)
		 }
		 ); 
		 
		 
		
		
		
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

		// if the game is past or at the frame we disappear, destroy us 
		if (gameController.TimeStep >= destroyStep)
		{
			FlagDestroy = true;
		}
	}

	public override void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force = false)
	{
		base.SaveSnapshot(snapshotDictionary, force);
		snapshotDictionary.Set(nameof(destroyStep), destroyStep, force);
	}

	public override void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
	{
		base.LoadSnapshot(snapshotDictionary);
		destroyStep = snapshotDictionary.Get<int>(nameof(destroyStep));
	}

	public override void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
	{
		base.ForceLoadSnapshot(snapshotDictionary);
		destroyStep = snapshotDictionary.Get<int>(nameof(destroyStep));
	}
}
