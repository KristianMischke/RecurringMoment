using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SizeMatcher : MonoBehaviour
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

    private SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer
    {
        get
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            return _spriteRenderer;
        }
    }

    [SerializeField] private bool matchBoxCollider;
    [SerializeField] private bool matchSpriteRenderer;
    [SerializeField] private float gridSize;

    private void Start()
    {
    }

    void Update()
    {
        UpdateDims();
    }

    void OnGUI()
    {
        UpdateDims();
    }
    private void UpdateDims()
    {
        Vector2? dims = null;

        if (matchSpriteRenderer && SpriteRenderer != null && SpriteRenderer.drawMode == SpriteDrawMode.Sliced)
        {
            dims = SpriteRenderer.size;
        }
        if (matchBoxCollider && BoxCollider2D != null)
        {
            dims = BoxCollider2D.size;
        }

        if (gridSize > 0)
            transform.position = (Vector3)Vector3Int.RoundToInt(transform.position / gridSize) * gridSize;

        if (dims.HasValue)
        {
            if (gridSize > 0)
            {
                dims = (Vector2)Vector2Int.RoundToInt(dims.Value / gridSize) * gridSize;
            }

            if (SpriteRenderer != null && SpriteRenderer.drawMode == SpriteDrawMode.Sliced) SpriteRenderer.size = dims.Value;
            if (BoxCollider2D != null)
            {
                BoxCollider2D.size = dims.Value;
                BoxCollider2D.offset =  Vector2.zero;
            }
        }
    }
}
