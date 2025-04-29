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
    public bool showPath = true;
    public float lineWidth = 0.05f;
    public Color passedColor = Color.red; // Color for passed segments
    public Color unpassedColor = Color.green; // Color for unpassed segments

    private void Start()
    {
        InitialPosition_Mover = moverObject.transform.position;

        // Initialize LineRenderer
        pathLineRenderer = gameObject.AddComponent<LineRenderer>();
        pathLineRenderer.positionCount = 0;
        pathLineRenderer.startWidth = lineWidth;
        pathLineRenderer.endWidth = lineWidth;
        pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        pathLineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor); // Default to all green
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
                    UpdateLineColors(); // Update colors when reaching a new point
                }

                // Update colors during movement for smooth transition
                UpdateLineColors();
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

        // Reset all lines to green
        if (showPath && orderedPath.Count > 1)
        {
            pathLineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
        }

        moverObject.transform.DOMove(startPosition, travelTime, false).SetEase(Ease.Linear).OnComplete(() =>
        {
            CanMove = true;
        });
    }

    public void GeneratePathPoints()
    {
        foreach (Transform child in pathPointsContainer)
        {
            pathPointPool.Enqueue(child.gameObject);
            child.gameObject.SetActive(false);
        }

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

    // Helper method to create a gradient for the LineRenderer
    private Gradient CreateGradient(Color startColor, Color endColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }

    // Update the LineRenderer colors based on the currentTargetIndex
    private void UpdateLineColors()
    {
        if (!showPath || orderedPath.Count < 2) return;

        Gradient gradient = new Gradient();
        List<GradientColorKey> colorKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> alphaKeys = new List<GradientAlphaKey>();

        // Calculate the fraction of the path completed
        float fractionPerPoint = 1f / (orderedPath.Count - 1);
        float currentFraction = currentTargetIndex * fractionPerPoint;

        // Determine the fraction of the current segment the mover has traveled
        if (currentTargetIndex < orderedPath.Count - 1)
        {
            Vector3 currentPos = moverObject.transform.position;
            Vector3 startPos = orderedPath[currentTargetIndex];
            Vector3 endPos = orderedPath[currentTargetIndex + 1];
            float segmentDistance = Vector3.Distance(startPos, endPos);
            float traveledDistance = Vector3.Distance(startPos, currentPos);
            float segmentFraction = segmentDistance > 0 ? traveledDistance / segmentDistance : 0;
            currentFraction += segmentFraction * fractionPerPoint;
        }

        // Set color keys: red up to the current fraction, green after
        colorKeys.Add(new GradientColorKey(passedColor, 0f));
        colorKeys.Add(new GradientColorKey(passedColor, currentFraction));
        colorKeys.Add(new GradientColorKey(unpassedColor, currentFraction));
        colorKeys.Add(new GradientColorKey(unpassedColor, 1f));

        // Set alpha keys (fully opaque)
        alphaKeys.Add(new GradientAlphaKey(1f, 0f));
        alphaKeys.Add(new GradientAlphaKey(1f, 1f));

        gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
        pathLineRenderer.colorGradient = gradient;
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

        if (showPath && orderedPath.Count > 1)
        {
            pathLineRenderer.positionCount = orderedPath.Count;
            pathLineRenderer.SetPositions(orderedPath.ToArray());
            pathLineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor); // Start with all green
        }
        else
        {
            pathLineRenderer.positionCount = 0;
        }
    }

    private float GetAxisValue(Vector3 point, string axis)
    {
        return axis == "X" ? point.x : axis == "Y" ? point.y : point.z;
    }
    #endregion
}