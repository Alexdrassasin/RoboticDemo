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
    [Range(0.05f, 1f)]
    public float density = 0.1f;
    public Button generatePathButton, startButton;
    public Transform pathPointsContainer;
    private Queue<GameObject> pathPointPool = new Queue<GameObject>();
    private Vector3 InitialPosition_Mover;

    public List<Vector3> orderedPath;
    public int currentTargetIndex = 0;
    public bool CanMove = false;

    // LineRenderer for path visualization
    private LineRenderer pathLineRenderer;
    public bool showPath = true; // Toggle to show/hide the path visualization
    public float lineWidth = 0.05f; // Width of the line
    public Color lineColor = Color.green; // Color of the line

    private void Start()
    {
        InitialPosition_Mover = moverObject.transform.position;

        // Initialize LineRenderer
        pathLineRenderer = gameObject.AddComponent<LineRenderer>();
        pathLineRenderer.positionCount = 0; // Start with no points
        pathLineRenderer.startWidth = lineWidth;
        pathLineRenderer.endWidth = lineWidth;
        pathLineRenderer.material = new Material(Shader.Find("Sprites/Default")); // Simple material
        pathLineRenderer.startColor = lineColor;
        pathLineRenderer.endColor = lineColor;
    }

    private void Update()
    {
        if (CanMove)
        {
            if (orderedPath.Count > 0 && currentTargetIndex < orderedPath.Count)
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
        float travelTime = Vector3.Distance(moverObject.transform.position, startPosition) / speed;

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

        // Clear the LineRenderer
        pathLineRenderer.positionCount = 0;

        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;

        List<Vector3> worldVertices = vertices.Select(v => targetObject.transform.TransformPoint(v)).ToList();

        if (worldVertices.Count == 0)
        {
            Debug.LogWarning("No vertices found in the mesh to generate path points.");
            return;
        }

        Vector3 minBounds = new Vector3(
            worldVertices.Min(p => p.x),
            worldVertices.Min(p => p.y),
            worldVertices.Min(p => p.z)
        );
        Vector3 maxBounds = new Vector3(
            worldVertices.Max(p => p.x),
            worldVertices.Max(p => p.y),
            worldVertices.Max(p => p.z)
        );
        Vector3 boundsRange = maxBounds - minBounds;

        string axis1, axis2, fixedAxis;
        if (boundsRange.x <= boundsRange.y && boundsRange.x <= boundsRange.z)
        {
            axis1 = "Y";
            axis2 = "Z";
            fixedAxis = "X";
        }
        else if (boundsRange.y <= boundsRange.x && boundsRange.y <= boundsRange.z)
        {
            axis1 = "X";
            axis2 = "Z";
            fixedAxis = "Y";
        }
        else
        {
            axis1 = "X";
            axis2 = "Y";
            fixedAxis = "Z";
        }

        Debug.Log($"Dominant plane: {axis1}-{axis2}, fixed axis: {fixedAxis}");

        int totalVertices = worldVertices.Count;
        int desiredPointCount = Mathf.Max(2, Mathf.FloorToInt(totalVertices * density));

        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(desiredPointCount));
        int gridWidth = gridSize;
        int gridHeight = gridSize;

        float rangeAxis1 = axis1 == "X" ? boundsRange.x : axis1 == "Y" ? boundsRange.y : boundsRange.z;
        float rangeAxis2 = axis2 == "X" ? boundsRange.x : axis2 == "Y" ? boundsRange.y : boundsRange.z;
        float cellWidth = rangeAxis1 / gridWidth;
        float cellHeight = rangeAxis2 / gridHeight;

        List<Vector3>[,] grid = new List<Vector3>[gridWidth, gridHeight];
        for (int i = 0; i < gridWidth; i++)
        {
            for (int j = 0; j < gridHeight; j++)
            {
                grid[i, j] = new List<Vector3>();
            }
        }

        foreach (Vector3 vertex in worldVertices)
        {
            float value1 = axis1 == "X" ? vertex.x : axis1 == "Y" ? vertex.y : vertex.z;
            float value2 = axis2 == "X" ? vertex.x : axis2 == "Y" ? vertex.y : vertex.z;

            int gridX = Mathf.FloorToInt((value1 - (axis1 == "X" ? minBounds.x : axis1 == "Y" ? minBounds.y : minBounds.z)) / cellWidth);
            int gridY = Mathf.FloorToInt((value2 - (axis2 == "X" ? minBounds.x : axis2 == "Y" ? minBounds.y : minBounds.z)) / cellHeight);

            gridX = Mathf.Clamp(gridX, 0, gridWidth - 1);
            gridY = Mathf.Clamp(gridY, 0, gridHeight - 1);

            grid[gridX, gridY].Add(vertex);
        }

        List<Vector3> sampledPoints = new List<Vector3>();
        for (int i = 0; i < gridWidth; i++)
        {
            for (int j = 0; j < gridHeight; j++)
            {
                if (grid[i, j].Count > 0)
                {
                    Vector3 averagePosition = Vector3.zero;
                    foreach (Vector3 vertex in grid[i, j])
                    {
                        averagePosition += vertex;
                    }
                    averagePosition /= grid[i, j].Count;
                    sampledPoints.Add(averagePosition);
                }
            }
        }

        if (sampledPoints.Count < 2)
        {
            Debug.LogWarning("Too few points sampled. Adding a second point to enable path generation.");
            if (sampledPoints.Count == 1)
            {
                sampledPoints.Add(worldVertices[worldVertices.Count - 1]);
            }
            else
            {
                sampledPoints.AddRange(worldVertices.Take(2));
            }
        }

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

        string dominantAxis = (boundsRange.x > boundsRange.y && boundsRange.x > boundsRange.z) ? "X" :
                             (boundsRange.y > boundsRange.x && boundsRange.y > boundsRange.z) ? "Y" : "Z";

        Debug.Log("DominantAxis: " + dominantAxis);

        float maxRange = Mathf.Max(boundsRange.x, boundsRange.y, boundsRange.z);

        var sortedDominantValues = pathPoints
            .Select(p => GetAxisValue(p, dominantAxis))
            .OrderBy(v => v)
            .ToList();

        float totalSpacing = 0f;
        for (int i = 1; i < sortedDominantValues.Count; i++)
        {
            totalSpacing += Mathf.Abs(sortedDominantValues[i] - sortedDominantValues[i - 1]);
        }
        float avgSpacing = totalSpacing / Mathf.Max(1, sortedDominantValues.Count - 1);

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

        for (int i = 0; i < rows.Count; i++)
        {
            string secondaryAxis = dominantAxis == "X" ? (boundsRange.y > boundsRange.z ? "Y" : "Z") :
                                  dominantAxis == "Y" ? (boundsRange.x > boundsRange.z ? "X" : "Z") :
                                  (boundsRange.x > boundsRange.y ? "X" : "Y");

            var sortedRow = rows[i].OrderBy(p => GetAxisValue(p, secondaryAxis)).ToList();

            if (i % 2 == 1)
            {
                sortedRow.Reverse();
            }

            orderedPath.AddRange(sortedRow);
        }

        // Update the LineRenderer to visualize the path
        if (showPath && orderedPath.Count > 1)
        {
            pathLineRenderer.positionCount = orderedPath.Count;
            pathLineRenderer.SetPositions(orderedPath.ToArray());
        }
        else
        {
            pathLineRenderer.positionCount = 0; // Hide the line if showPath is false or not enough points
        }
    }

    private float GetAxisValue(Vector3 point, string axis)
    {
        return axis == "X" ? point.x : axis == "Y" ? point.y : point.z;
    }
    #endregion
}