using UnityEngine;

public class GroundSnapOnStart : MonoBehaviour
{
    [SerializeField] private bool snapOnStart = true;
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private float yOffset = 0f;
    [SerializeField] private LayerMask groundLayer;

    private void Start()
    {
        if (!snapOnStart)
        {
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(transform.position + Vector3.up * 0.5f, Vector2.down, rayDistance, groundLayer);
        if (!hit)
        {
            return;
        }

        Vector3 p = transform.position;
        p.y = hit.point.y + yOffset;
        transform.position = p;
    }
}
