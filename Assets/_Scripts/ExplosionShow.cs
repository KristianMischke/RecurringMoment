using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionShow : MonoBehaviour
{
	// this is in case there are multple explosion at once 
	// so there is a list that drags each one and then removes it after the time passes 
	// - I hope it does that at least....
	public List<LineRenderer> timeEnd;
	public List<int> timeFin;  
	public int timeHeld = 5; // a number you can change to change how long the circle stays on the screen 
	
	void Start()
	{
		// starts the two list empty 
		List<LineRenderer> timeEnd = new List<LineRenderer>(); 
		List<int> timeFin = new List<int>(); 
	}
	
	
    void Update()
	{
		Component [] showExplosion = GetComponentsInChildren(typeof(LineRenderer)); // gets all of the current explosions (hopefully) and then goes through them 
		int counter = 0; // random variable for counting and stuff 
		int num;		
		foreach(var e in showExplosion) // for each linerenderer aka explosion check if there is already a copy in the list - if not add it and the time it will last 
		{
			bool copy = false;
			foreach (var s in timeEnd)
			{
				if (s == e)
				{
					copy = true; 
				}
			}
			if (copy == false) // if not already in list put it in list and get the current timestep (which i believe is the current time...hopefully) 
			{
				timeEnd.AddRange(e.GetComponentsInChildren<LineRenderer>()); // adds the linerenderer and stuff to their lists 
				GameObject w = GameObject.Find("GameController");
				GameController wS = w.GetComponent<GameController>();
				num = wS.TimeStep + timeHeld;
				timeFin.Add(num);				
			}
			
		}
		// gets current time and compare it to the finish times for the circle explosion thingy 
		GameObject g = GameObject.Find("GameController");
		GameController gS = g.GetComponent<GameController>();
		int currTime = gS.TimeStep;
		counter = 0;
		
		// adds each time that is over the current time into the delete (can't do it during this for loop since it causes a bunch of errors....) 
		List <int> delete = new List<int>();
		foreach (var s in timeFin)
		{
			if(currTime > s)
			{
				//timeFin.RemoveAt(counter);
				//timeEnd[counter].gameObject.SetActive(false);
				delete.Add(counter); 
				//timeEnd.RemoveAt(counter); 
			}
			counter += 1;
		}
		
		
		// go through the list to delete and remove it from the list and also remove the object that holds the explosion radius 
		foreach (var x in delete)
		{
			timeFin.RemoveAt(x);
			timeEnd[x].gameObject.SetActive(false);
			timeEnd.RemoveAt(x); 
		}
		
	}
		
}
