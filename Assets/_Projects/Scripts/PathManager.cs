using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PathManager : MonoBehaviour
{

    public GameObject pathPointPrefab;
    public GameObject targetObject;
    public GameObject moverObject;
    public float speed = 5f;
    [Range(0.05f,1f)]
    public float density = 0.1f;
    public Button generatePathButton, startButton;
    public Transform pathPointsContainer;
    private Queue<GameObject> pathPointPool = new Queue<GameObject>();
    private Vector3 InitialPosition_Mover;

    public List<Vector3> orderedPath;
    public int currentTargetIndex = 0;
    public bool CanMove = false;

    private void Start()
    {
        InitialPosition_Mover = moverObject.transform.position;
    }

    private void Update()
    {
        if (CanMove)
        {
            if(orderedPath.Count > 0 && currentTargetIndex < orderedPath.Count)
            {
                Vector3 targetPosition = orderedPath[currentTargetIndex];
                moverObject.transform.position = Vector3.MoveTowards(moverObject.transform.position, targetPosition, speed * Time.deltaTime);

                if (Vector3.Distance(moverObject.transform.position, targetPosition) < 0.1f)
                {
                    currentTargetIndex++;
                }
            }
            else
            {
                CanMove = false;
            }
        }
    }

    public void StartMoving()
    {
        CanMove = false;
        currentTargetIndex = 0;
        Vector3 startPosition = orderedPath.Count > 0 ? orderedPath[0] : InitialPosition_Mover;
        float travelTime = Vector3.Distance(moverObject.transform.position, startPosition) / speed; // Speed-based duration

        moverObject.transform.DOMove(startPosition, travelTime, false).SetEase(Ease.Linear).OnComplete(() =>
        {
            CanMove = true;
        });

    }

    public void GeneratePathPoints()
    {
        // Clear previous points
        foreach (Transform child in pathPointsContainer)
        {
            pathPointPool.Enqueue(child.gameObject);
            child.gameObject.SetActive(false);
        }

        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;



        //  Dynamic Density-Based Sampling
        List<Vector3> worldVertices = vertices.Select(v => targetObject.transform.TransformPoint(v)).ToList();
        int sampleCount = Mathf.Max(1, Mathf.FloorToInt(worldVertices.Count * density));
        List<Vector3> sampledPoints = worldVertices.OrderBy(p => p.x).Take(sampleCount).ToList();

        foreach (Vector3 pointPosition in sampledPoints)
        {
            GameObject pathPoint = GetPathPointFromPool();
            pathPoint.transform.position = pointPosition;
            pathPoint.SetActive(true);
        }

        SortPoints(sampledPoints);
    }

    GameObject GetPathPointFromPool()
    {
        return pathPointPool.Count > 0 ? pathPointPool.Dequeue() : Instantiate(pathPointPrefab, pathPointsContainer);
    }

    #region Path Sorting
    public void SortPoints(List<Vector3> pathPoints)
    {
        orderedPath.Clear();

        // Automatically Determine Major Axis for Sorting
        Vector3 minBounds = new Vector3(
            pathPoints.Min(p => p.x),
            pathPoints.Min(p => p.y),
            pathPoints.Min(p => p.z)
        );

        Vector3 maxBounds = new Vector3(
            pathPoints.Max(p => p.x),
            pathPoints.Max(p => p.y),
            pathPoints.Max(p => p.z)
        );

        Vector3 boundsRange = maxBounds - minBounds;

        // Find dominant axis (largest range)
        string dominantAxis = (boundsRange.x > boundsRange.y && boundsRange.x > boundsRange.z) ? "X" :
                             (boundsRange.y > boundsRange.x && boundsRange.y > boundsRange.z) ? "Y" : "Z";

        Debug.Log("DominantAxis: " + dominantAxis);
        // Dynamic Row Detection
        float maxRange = Mathf.Max(boundsRange.x, boundsRange.y, boundsRange.z);

        //Calculate Threshold
        var sortedDominantValues = pathPoints
    .Select(p => GetAxisValue(p, dominantAxis))
    .OrderBy(v => v)
    .ToList();

        // Compute average spacing between consecutive dominant axis values
        float totalSpacing = 0f;
        for (int i = 1; i < sortedDominantValues.Count; i++)
        {
            totalSpacing += Mathf.Abs(sortedDominantValues[i] - sortedDominantValues[i - 1]);
        }
        float avgSpacing = totalSpacing / Mathf.Max(1, sortedDominantValues.Count - 1);

        // Now use that as a dynamic threshold multiplier
        float dynamicThreshold = avgSpacing * 1.5f;

        Debug.Log(dynamicThreshold);

        List<List<Vector3>> rows = new List<List<Vector3>>();
        List<Vector3> currentRow = new List<Vector3>();
        float lastAxisValue = GetAxisValue(pathPoints[0], dominantAxis);

        foreach (Vector3 point in pathPoints.OrderBy(p => GetAxisValue(p, dominantAxis)))
        {
            if (Mathf.Abs(GetAxisValue(point, dominantAxis) - lastAxisValue) > dynamicThreshold)
            {
                rows.Add(new List<Vector3>(currentRow));
                currentRow.Clear();
            }

            currentRow.Add(point);
            lastAxisValue = GetAxisValue(point, dominantAxis);
        }
        if (currentRow.Count > 0) rows.Add(currentRow);

        // Corrected ] Shaped Traversal with Alternating Direction
        for (int i = 0; i < rows.Count; i++)
        {
            // Determine secondary axis for sorting within the row (perpendicular to dominant axis)
            string secondaryAxis = dominantAxis == "X" ? (boundsRange.y > boundsRange.z ? "Y" : "Z") :
                                  dominantAxis == "Y" ? (boundsRange.x > boundsRange.z ? "X" : "Z") :
                                  (boundsRange.x > boundsRange.y ? "X" : "Y");

            // Sort row by secondary axis
            var sortedRow = rows[i].OrderBy(p => GetAxisValue(p, secondaryAxis)).ToList();

            // Reverse every other row (e.g., even-indexed rows go forward, odd-indexed rows go backward)
            if (i % 2 == 1)
            {
                sortedRow.Reverse();
            }

            orderedPath.AddRange(sortedRow); // Add row to the path
        }
    }

    // Helper function to get value for the dominant axis
    private float GetAxisValue(Vector3 point, string axis)
    {
        return axis == "X" ? point.x : axis == "Y" ? point.y : point.z;
    }


    #endregion
}