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
        List<Vector3> worldVertices = vertices.Select(v => targetObject.transform.TransformPoint(v)).ToList();

        //  Dynamic Density-Based Sampling
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

        //  Auto-Adjust Row Detection Based on Density
        float dynamicThreshold = Mathf.Max(0.05f, 0.2f * density); // Adjust spacing dynamically
        List<List<Vector3>> rows = new List<List<Vector3>>();
        List<Vector3> currentRow = new List<Vector3>();
        float lastZ = pathPoints[0].z;

        foreach (Vector3 point in pathPoints.OrderBy(p => p.z))
        {
            if (Mathf.Abs(point.z - lastZ) > dynamicThreshold)
            {
                rows.Add(new List<Vector3>(currentRow));
                currentRow.Clear();
            }

            currentRow.Add(point);
            lastZ = point.z;
        }
        if (currentRow.Count > 0) rows.Add(currentRow);

        //  Ensure Zigzag Traversal Without Crossing
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i] = rows[i].OrderBy(p => p.x).ToList();
            if (i % 2 == 1) rows[i].Reverse(); // U-turn effect
        }

        foreach (var row in rows)
        {
            orderedPath.AddRange(row);
        }
    }
    #endregion
}