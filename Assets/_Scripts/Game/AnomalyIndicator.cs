using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

public class AnomalyIndicator : MonoBehaviour
{
    private Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();

    public Color tint;
    
    public void Apply()
    {
        foreach (var sr in gameObject.GetComponentsInChildren<SpriteRenderer>())
        {
            originalColors[sr] = sr.color;
            sr.color = tint;
        }
    }
    
    public void Remove()
    {
        foreach (var kvp in originalColors)
        {
            kvp.Key.color = kvp.Value;
        }
        Destroy(this);
    }
}
