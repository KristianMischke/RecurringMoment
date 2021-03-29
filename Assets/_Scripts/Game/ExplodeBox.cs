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
			RaycastHit2D [] hits = Physics2D.RaycastAll(loc, Vector2.up, distance);
			
			// checks the up direction 
			foreach(var hit in hits)
			{
				Debug.Log("The collider hit is :" + hit.collider.transform.gameObject.tag + " or - " + hit.collider.transform.tag); 
				if (hit.collider.transform.gameObject.tag == "ExplodeWall")
				{
					Debug.Log("It is the exploseWall part");
					hit.collider.transform.gameObject.SetActive(false); 
				}
			}
			
			// checks the down direction 
			hits = Physics2D.RaycastAll(loc, Vector2.down, distance);
			foreach(var hit in hits)
			{
				Debug.Log("The collider hit is :" + hit.collider.transform.gameObject.tag + " or - " + hit.collider.transform.tag); 
				if (hit.collider.transform.gameObject.tag == "ExplodeWall")
				{
					Debug.Log("It is the exploseWall part");
					hit.collider.transform.gameObject.SetActive(false); 
				}
			}
			// checks the left direction 
			hits = Physics2D.RaycastAll(loc, Vector2.left, distance);
			foreach(var hit in hits)
			{
				Debug.Log("The collider hit is :" + hit.collider.transform.gameObject.tag + " or - " + hit.collider.transform.tag); 
				if (hit.collider.transform.gameObject.tag == "ExplodeWall")
				{
					Debug.Log("It is the exploseWall part");
					hit.collider.transform.gameObject.SetActive(false); 
				}
			}
			
			hits = Physics2D.RaycastAll(loc, Vector2.right, distance);
			foreach(var hit in hits)
			{
				Debug.Log("The collider hit is :" + hit.collider.transform.gameObject.tag + " or - " + hit.collider.transform.tag); 
				if (hit.collider.transform.gameObject.tag == "ExplodeWall")
				{
					Debug.Log("It is the exploseWall part");
					hit.collider.transform.gameObject.SetActive(false); 
				}
			}
            self.SetActive(false); 
			exploded = true;
        }
        
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
