using UnityEngine;
using System.Collections.Generic;

public class PathLineRenderer
{
    private readonly LineRenderer lineRenderer;
    private bool showPath;
    private Color passedColor;
    private Color unpassedColor;
    private Color pulseColor;
    private float pulseSpeed;
    private float pulseWidth;
    private bool isPulsing;
    private float pulseTime;

    public PathLineRenderer(GameObject gameObject, float lineWidth, bool showPath, Color passedColor, Color unpassedColor, Color pulseColor, float pulseSpeed, float pulseWidth)
    {
        this.showPath = showPath;
        this.passedColor = passedColor;
        this.unpassedColor = unpassedColor;
        this.pulseColor = pulseColor;
        this.pulseSpeed = pulseSpeed;
        this.pulseWidth = pulseWidth;

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
    }

    public void Update(List<Vector3> path, int currentTargetIndex)
    {
        if (isPulsing && showPath && path.Count > 1)
        {
            pulseTime += Time.deltaTime * pulseSpeed;
            UpdatePulseEffect(path);
        }
        else if (showPath && path.Count > 1)
        {
            UpdateLineColors(path, currentTargetIndex);
        }
    }

    public void SetPath(List<Vector3> path)
    {
        if (showPath && path.Count > 1)
        {
            lineRenderer.positionCount = path.Count;
            lineRenderer.SetPositions(path.ToArray());
            lineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
        }
        else
        {
            lineRenderer.positionCount = 0;
        }
    }

    public void StartPulsing()
    {
        isPulsing = true;
        pulseTime = 0f;
    }

    public void ResetPulse()
    {
        isPulsing = false;
    }

    public void SetGradientToUnpassed()
    {
        if (showPath)
        {
            lineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
        }
    }

    // Updates parameters to reflect real-time changes and reapplies the current path visualization.
    public void UpdateParameters(float lineWidth, bool showPath, Color passedColor, Color unpassedColor, Color pulseColor, float pulseSpeed, float pulseWidth)
    {
        this.showPath = showPath;
        this.passedColor = passedColor;
        this.unpassedColor = unpassedColor;
        this.pulseColor = pulseColor;
        this.pulseSpeed = pulseSpeed;
        this.pulseWidth = pulseWidth;

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
    }

    private void UpdateLineColors(List<Vector3> path, int currentTargetIndex)
    {
        if (!showPath || path.Count < 2) return;

        Gradient gradient = new Gradient();
        List<GradientColorKey> colorKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> alphaKeys = new List<GradientAlphaKey>();

        float fractionPerPoint = 1f / (path.Count - 1);
        float currentFraction = currentTargetIndex * fractionPerPoint;

        colorKeys.Add(new GradientColorKey(passedColor, 0f));
        colorKeys.Add(new GradientColorKey(passedColor, currentFraction));
        colorKeys.Add(new GradientColorKey(unpassedColor, currentFraction));
        colorKeys.Add(new GradientColorKey(unpassedColor, 1f));

        alphaKeys.Add(new GradientAlphaKey(1f, 0f));
        alphaKeys.Add(new GradientAlphaKey(1f, 1f));

        gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
        lineRenderer.colorGradient = gradient;
    }

    private void UpdatePulseEffect(List<Vector3> path)
    {
        Gradient gradient = new Gradient();
        List<GradientColorKey> colorKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> alphaKeys = new List<GradientAlphaKey>();

        float pulsePosition = pulseTime % 1f;
        float pulseStart = Mathf.Clamp(pulsePosition - pulseWidth / 2f, 0f, 1f);
        float pulseEnd = Mathf.Clamp(pulsePosition + pulseWidth / 2f, 0f, 1f);

        if (pulseStart > 0f)
        {
            colorKeys.Add(new GradientColorKey(unpassedColor, 0f));
            colorKeys.Add(new GradientColorKey(unpassedColor, pulseStart));
        }

        if (pulseStart < pulseEnd)
        {
            colorKeys.Add(new GradientColorKey(pulseColor, pulseStart));
            colorKeys.Add(new GradientColorKey(pulseColor, pulseEnd));
        }

        if (pulseEnd < 1f)
        {
            colorKeys.Add(new GradientColorKey(unpassedColor, pulseEnd));
            colorKeys.Add(new GradientColorKey(unpassedColor, 1f));
        }

        alphaKeys.Add(new GradientAlphaKey(1f, 0f));
        alphaKeys.Add(new GradientAlphaKey(1f, 1f));

        gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
        lineRenderer.colorGradient = gradient;
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