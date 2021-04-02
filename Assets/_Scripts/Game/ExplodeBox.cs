using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplodeBox : MonoBehaviour
{
	public List<ActivatableBehaviour> requiredActivatables = new List<ActivatableBehaviour>();
	public GameObject self;
    [SerializeField] bool exploded = false;
	[SerializeField] float distance = 3; 

    bool doorMoving = false;
    float timer;
    Vector2 originalPos;

    private void Start()
    {
        originalPos = transform.position;
    }

    void Update()
    {
		
        if (AllActivated())
        {
			Vector2 loc = self.transform.position;
			Debug.Log("The location is : " + loc.x + "and "+ loc.y);
			float angle = 45.0f;
			
			
			
			List<RaycastHit2D> hits = new List<RaycastHit2D>();
			hits.AddRange(Physics2D.RaycastAll(loc, Vector2.up, distance)); 
			hits.AddRange(Physics2D.RaycastAll(loc, Vector2.down, distance)); 
			hits.AddRange(Physics2D.RaycastAll(loc, Vector2.left, distance)); 
			hits.AddRange(Physics2D.RaycastAll(loc, Vector2.right, distance)); 
			
			// gets the side angles as well 
			hits.AddRange(Physics2D.RaycastAll(loc, GetDirectionVector2D(angle), distance));
			angle = 145.0f;
			hits.AddRange(Physics2D.RaycastAll(loc, GetDirectionVector2D(angle), distance)); 
			angle = 245.0f;
			hits.AddRange(Physics2D.RaycastAll(loc, GetDirectionVector2D(angle), distance)); 
			angle = 345.0f;
			hits.AddRange(Physics2D.RaycastAll(loc, GetDirectionVector2D(angle), distance)); 

			foreach(var hit in hits)
			{
				Debug.Log("The collider hit is :" + hit.collider.transform.gameObject.tag + " or - " + hit.collider.transform.tag); 
				if (hit.collider.transform.gameObject.tag == "ExplodeWall")
				{
					hit.collider.transform.gameObject.SetActive(false); 
				}
			}
            self.SetActive(false); 
			exploded = true;
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
