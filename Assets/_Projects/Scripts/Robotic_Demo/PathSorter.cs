using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public abstract class PathSorter
{
    public abstract List<Vector3> Sort(List<Vector3> points, bool showPath, LineRenderer lineRenderer, Color unpassedColor);
}

public class PathSorter2D : PathSorter
{
    public override List<Vector3> Sort(List<Vector3> pathPoints, bool showPath, LineRenderer lineRenderer, Color unpassedColor)
    {
        List<Vector3> orderedPath = new List<Vector3>();
        if (pathPoints.Count == 0) return orderedPath;

        Vector3 minBounds = new Vector3(pathPoints.Min(p => p.x), pathPoints.Min(p => p.y), pathPoints.Min(p => p.z));
        Vector3 maxBounds = new Vector3(pathPoints.Max(p => p.x), pathPoints.Max(p => p.y), pathPoints.Max(p => p.z));
        Vector3 boundsRange = maxBounds - minBounds;

        string dominantAxis = boundsRange.x > boundsRange.y && boundsRange.x > boundsRange.z ? "X" :
                             boundsRange.y > boundsRange.x && boundsRange.y > boundsRange.z ? "Y" : "Z";

        Debug.Log("DominantAxis: " + dominantAxis);

        float maxRange = Mathf.Max(boundsRange.x, boundsRange.y, boundsRange.z);
        var sortedDominantValues = pathPoints.Select(p => GetAxisValue(p, dominantAxis)).OrderBy(v => v).ToList();

        float totalSpacing = 0f;
        for (int i = 1; i < sortedDominantValues.Count; i++)
        {
            totalSpacing += Mathf.Abs(sortedDominantValues[i] - sortedDominantValues[i - 1]);
        }
        float avgSpacing = totalSpacing / Mathf.Max(1, sortedDominantValues.Count - 1);
        float dynamicThreshold = avgSpacing * 1.5f;

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
            if (i % 2 == 1) sortedRow.Reverse();
            orderedPath.AddRange(sortedRow);
        }

        UpdateLineRenderer(orderedPath, showPath, lineRenderer, unpassedColor);
        return orderedPath;
    }

    private float GetAxisValue(Vector3 point, string axis)
    {
        return axis == "X" ? point.x : axis == "Y" ? point.y : point.z;
    }

    private void UpdateLineRenderer(List<Vector3> orderedPath, bool showPath, LineRenderer lineRenderer, Color unpassedColor)
    {
        if (showPath && orderedPath.Count > 1)
        {
            lineRenderer.positionCount = orderedPath.Count;
            lineRenderer.SetPositions(orderedPath.ToArray());
            lineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
        }
        else
        {
            lineRenderer.positionCount = 0;
        }
    }

    private Gradient CreateGradient(Color startColor, Color endColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
}

public class PathSorter3D : PathSorter
{
    public override List<Vector3> Sort(List<Vector3> pathPoints, bool showPath, LineRenderer lineRenderer, Color unpassedColor)
    {
        List<Vector3> orderedPath = new List<Vector3>();
        if (pathPoints.Count == 0) return orderedPath;

        List<Vector3> remainingPoints = new List<Vector3>(pathPoints);
        Vector3 currentPoint = remainingPoints[0];
        orderedPath.Add(currentPoint);
        remainingPoints.RemoveAt(0);

        while (remainingPoints.Count > 0)
        {
            int nearestIndex = 0;
            float minDistance = float.MaxValue;

            for (int i = 0; i < remainingPoints.Count; i++)
            {
                float distance = Vector3.Distance(currentPoint, remainingPoints[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            currentPoint = remainingPoints[nearestIndex];
            orderedPath.Add(currentPoint);
            remainingPoints.RemoveAt(nearestIndex);
        }

        if (showPath && orderedPath.Count > 1)
        {
            lineRenderer.positionCount = orderedPath.Count;
            lineRenderer.SetPositions(orderedPath.ToArray());
            lineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
        }
        else
        {
            lineRenderer.positionCount = 0;
        }

        return orderedPath;
    }

    private Gradient CreateGradient(Color startColor, Color endColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
}