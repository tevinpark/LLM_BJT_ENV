using UnityEngine;
using System.Collections;

public class OscillateMovement : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float turnAngle = 30f;      // Degrees to rotate left and right on Y-axis
    public float rotationTime = 1f;    // Time taken for each rotation step
    public float pauseTime = 1f;       // Pause duration after returning to center

    private Quaternion startRotation;
    private bool isRotating = false;   // Prevents multiple triggers

    void Start()
    {
        startRotation = transform.rotation;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L) && !isRotating)
        {
            StartCoroutine(RotateSequence());
        }
    }

    IEnumerator RotateSequence()
    {
        isRotating = true;

        // Target rotations around the Y-axis
        Quaternion leftRotation = startRotation * Quaternion.Euler(0, -turnAngle, 0); // Rotate left
        Quaternion rightRotation = startRotation * Quaternion.Euler(0, turnAngle, 0); // Rotate right

        // Rotate with ease-in and ease-out
        yield return RotateTo(leftRotation, rotationTime);
        yield return RotateTo(rightRotation, rotationTime);
        yield return RotateTo(startRotation, rotationTime);

        // Pause before allowing another key press
        yield return new WaitForSeconds(pauseTime);

        isRotating = false;
    }

    IEnumerator RotateTo(Quaternion targetRotation, float duration)
    {
        Quaternion initialRotation = transform.rotation;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            t = Mathf.SmoothStep(0, 1, t);  // Apply ease-in and ease-out

            transform.rotation = Quaternion.Lerp(initialRotation, targetRotation, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
    }
}

