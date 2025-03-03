using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SnapToGridScript : MonoBehaviour
{
    [Header("Snap Settings")]
    // The width (in pixels) of each item in your content.
    public float itemWidth = 210f;
    public float itemWidthOffset = 70f;
    public int totalItems = 100;
    // Used to finely adjust where the scroll starts and ends
    public int customMinTarget = 0;
    public int customMaxTarget = 0;
    // Used to finely adjust number selections
    public int customMinNum = 0;
    // The strength of the spring-like tug toward the nearest snap position.
    public float snapForce = 50f;
    public float snapDiffMultiplier = 1f;

    [Header("Inertia Settings")]
    // A friction/deceleration multiplier (closer to 1 means slower decay).
    public float decelerationRate = 0.95f;

    [Header("Optional Clamping")]
    // Enable clamping to restrict how far the content can scroll.
    public bool clamp = false;
    public float minX = -500f;
    public float maxX = 500f;

    // The current horizontal velocity (in pixels per second).
    // Set this externally (or via the Spin method) to trigger scrolling.
    public float velocity = 0f;

    // Cached reference to the RectTransform on this GameObject.
    private RectTransform rectTransform;

    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        // Get the current horizontal position.
        float currentX = rectTransform.anchoredPosition.x;
        // Compute the nearest snap targeFt (rounded to the nearest multiple of itemWidth).
        float targetX = Mathf.Round(currentX / itemWidth) * itemWidth;
        targetX = Mathf.Clamp(targetX, (-totalItems + customMaxTarget) * itemWidth + itemWidthOffset, customMinTarget * itemWidth + itemWidthOffset);
        Debug.Log(Mathf.Clamp(Mathf.Abs(Mathf.CeilToInt((rectTransform.anchoredPosition.x - itemWidthOffset - 20) / itemWidth)) + customMinNum, 1, totalItems ));
        float snapDiff = targetX - currentX;

        // Apply a spring-like force to tug the content toward the snap target.
        velocity += snapDiff * snapForce * Time.deltaTime;
        // Apply friction to gradually slow down the velocity.
        velocity *= decelerationRate;

        // Update the content's horizontal position.
        float newX = currentX + velocity * Time.deltaTime;
        if (clamp)
        {
            newX = Mathf.Clamp(newX, minX, maxX);
        }
        rectTransform.anchoredPosition = new Vector2(newX, rectTransform.anchoredPosition.y);

        // If the velocity and distance to the snap target are very small, snap exactly into place.
        if (Mathf.Abs(velocity) < (10f*snapDiffMultiplier) && Mathf.Abs(snapDiff) < (1f*snapDiffMultiplier))
        {
            rectTransform.anchoredPosition = new Vector2(targetX, rectTransform.anchoredPosition.y);
            velocity = 0f;
        }
    }

    public int getNum()
    {
        return Mathf.Clamp(Mathf.Abs(Mathf.CeilToInt((rectTransform.anchoredPosition.x - itemWidthOffset - 20) / itemWidth)) + customMinNum, 1, totalItems );
    }
}
