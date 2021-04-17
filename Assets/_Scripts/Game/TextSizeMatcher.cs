using UnityEngine;
#if UNITY_EDITOR

#endif

[ExecuteInEditMode]
public class TextSizeMatcher : MonoBehaviour
{
    [SerializeField] private bool isCanvas;
    [SerializeField] private bool matchBoxCollider;
    [SerializeField] private float gridSize;

    public Color gizmoColor;


    private BoxCollider2D _boxCollider;

    private RectTransform _rectTransform;

    public BoxCollider2D BoxCollider2D
    {
        get
        {
            if (isCanvas == false)
            {
                if (_boxCollider == null) _boxCollider = GetComponent<BoxCollider2D>();
                return _boxCollider;
            }

            if (_boxCollider == null) _boxCollider = GetComponentInChildren<BoxCollider2D>();
            return _boxCollider;
        }
    }

    public RectTransform RectTransform
    {
        get
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            return _rectTransform;
        }
    }

    private void Start()
    {
    }

    private void Update()
    {
        UpdateDims();
    }

    private void OnGUI()
    {
        UpdateDims();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        if (BoxCollider2D != null)
            Gizmos.DrawWireCube(transform.position, new Vector3(BoxCollider2D.size.x, BoxCollider2D.size.y, 0.1f));
    }
#endif

    private void UpdateDims()
    {
        Vector2? dims = null;

        if (matchBoxCollider && BoxCollider2D != null) dims = BoxCollider2D.size;

        if (gridSize > 0) transform.position = (Vector3)Vector3Int.RoundToInt(transform.position / gridSize) * gridSize;

        if (dims.HasValue)
        {
            if (gridSize > 0) dims = (Vector2)Vector2Int.RoundToInt(dims.Value / gridSize) * gridSize;

            if (BoxCollider2D != null)
            {
                BoxCollider2D.size = dims.Value;
                BoxCollider2D.offset = Vector2.zero;
                RectTransform.sizeDelta = dims.Value;
            }
        }
    }
}