using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class InvisibleObject : MonoBehaviour
{
    private BoxCollider2D _boxCollider;
    public BoxCollider2D BoxCollider2D
    {
        get
        {
            if (_boxCollider == null)
            {
                _boxCollider = GetComponent<BoxCollider2D>();
            }
            return _boxCollider;
        }
    }

    public Color gizmoColor;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        if (BoxCollider2D != null)
        {
            Gizmos.DrawWireCube(transform.position + (Vector3)BoxCollider2D.offset, new Vector3(BoxCollider2D.size.x, BoxCollider2D.size.y, 0.1f));
        }
    }
#endif
}
