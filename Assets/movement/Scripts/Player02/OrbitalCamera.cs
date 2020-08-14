/*

Based off rigidbody movement tutorial by catlikecoding at:

https://catlikecoding.com/unity/tutorials/movement/

*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitalCamera : MonoBehaviour
{
    [SerializeField]
    Transform focus = default;

    [SerializeField, Range(1f, 20f)]
    float distance = 5f;

    [SerializeField]
	Vector3 offset = new Vector3(1f,1f,0f);

    [SerializeField, Min(0f)]
    float focusRadius = 1f;

    [SerializeField, Range(0f, 1f)]
    float focusCentering = 0.5f;

    //todo rename to something more like mouse rotation speed
    [SerializeField, Range(1f, 360f)]
    float rotationSpeed = 90f;

    [SerializeField, Range(-89f, 89f)]
    float minVerticalAngle = -30f, maxVerticalAngle = 60f;

    [SerializeField, Min(0f)]
    float alignDelay = 5f; //todo rename

    [SerializeField, Range(0f, 90f)]
	float alignSmoothRange = 45f;

    Vector3 focusPoint, previousFocusPoint;
    Vector2 orbitAngles = new Vector2(45f, 0f);
    float lastManualRotationTime;

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    void Awake()
    {
        // -------------

        focusPoint = focus.position;
        transform.localRotation = Quaternion.Euler(orbitAngles); // setting inital rotation

        // -------------
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    void OnValidate()
    {
        // -------------

        if (maxVerticalAngle < minVerticalAngle)
        {
            maxVerticalAngle = minVerticalAngle;
        }

        // -------------
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    void LateUpdate()
    {
        // -------------

        UpdateFocusPoint();

        Quaternion lookRotation;
        if (ManualRotation() || AutomaticRotation())
        {
            ConstrainAngles();
            lookRotation = Quaternion.Euler(orbitAngles);
        }
        else
        {
            //! why is this using localRotation
            lookRotation = transform.localRotation;
        }

        Vector3 lookDirection = lookRotation * Vector3.forward;
        Vector3 lookOffset = transform.rotation * offset;
        Vector3 lookPosition = focusPoint + lookOffset - lookDirection * distance;

        // Vector3 lookPosition = transform.InverseTransformPoint(offset) + focusPoint - lookDirection * distance;
        transform.SetPositionAndRotation(lookPosition, lookRotation); //! if we are setting world rotation here

        // -------------
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    //TODO Rework that bool, unclear about method use, 
    //TODO also setting an outer variable, replace orbitAngles with an OUT quaternion
    bool ManualRotation()
    {
        // -------------

        //TODO rework with external input manager to handle those in one place
        //TODO so that mouse roatation can be controlled by a maouse click or locked or slowed down when other conditions happen
        //TODO rework using new unity input system
        Vector2 input = new Vector2(
                    Input.GetAxis("Mouse Y"),
                    Input.GetAxis("Mouse X")
                );

        //TODO add settings to flip vertical rotation
        input.x *= -1;

        //TODO add better sensitivity controlling and options, especially horizontal rotation
        input.y = Mathf.Sign(input.y) * Mathf.Pow(input.y,2f);

        const float e = float.Epsilon; //TODO Consider changing this to higher value to avoid gittery movement
        if (input.x < -e || input.x > e || input.y < -e || input.y > e)
        {
            orbitAngles += rotationSpeed * Time.unscaledDeltaTime * input;
            lastManualRotationTime = Time.unscaledTime;
            return true;
        }

        return false;

        // -------------
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    //TODO Rename
    bool AutomaticRotation()
    {
        // -------------

        // Preventing automatic realignment if player just rotated the camera manualy
        if (Time.unscaledTime - lastManualRotationTime < alignDelay)
        {
            return false;
        }

        Vector2 movement = new Vector2(
            focusPoint.x - previousFocusPoint.x,
            focusPoint.z - previousFocusPoint.z
        );

        float movementDeltaSqr = movement.sqrMagnitude;
        if (movementDeltaSqr < 0.000001f)
        {
            return false;
        }

        float headingAngle = GetYAngle(movement / Mathf.Sqrt(movementDeltaSqr));
        float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));
        float rotationChange = rotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr); //? some little trick here for tiny angles

        // smooth out camera automatic alignment for given range
		if (deltaAbs < alignSmoothRange) {
			rotationChange *= deltaAbs / alignSmoothRange;
		}
        else if (180f - deltaAbs < alignSmoothRange) {
			rotationChange *= (180f - deltaAbs) / alignSmoothRange;
		}

		orbitAngles.y =	Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange);

        return true;

        // -------------
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /* 
    Calculate the focus point of camera with respoect to the position of the followed object
    */
    void UpdateFocusPoint()
    {
        // -------------

        previousFocusPoint = focusPoint;
        Vector3 targetPoint = focus.position;

        if (focusRadius > 0f)
        {
            float distance = Vector3.Distance(targetPoint, focusPoint);
            float t = 1f;

            //todo this isn't the coolest solution because it does not give so much flexibility in terms of camera centering speed
            if (distance > float.Epsilon && focusCentering > 0f)
            {
                t = Mathf.Pow(1 - focusCentering, Time.unscaledDeltaTime);
            }

            if (distance > focusRadius)
            {
                //* focusRadius / distance - "pull the focus toward the target until the distance matches the radius"
                // focusPoint = Vector3.Lerp(targetPoint, focusPoint, focusRadius / distance);

                t = Mathf.Min(t, focusRadius / distance);
            }

            focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
        }
        else
        {
            focusPoint = targetPoint;
        }

        // -------------
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    void ConstrainAngles()
    {
        // -------------

        orbitAngles.x = Mathf
            .Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);

        if (orbitAngles.y < 0f)
        {
            orbitAngles.y += 360f;
        }
        else if (orbitAngles.y >= 360f)
        {
            orbitAngles.y -= 360f;
        }

        // -------------
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    static float GetYAngle(Vector2 direction)
    {
        // -------------
        
        //? Will trigonometry ever feel intuitive to me?
        float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
        return direction.x < 0f ? 360f - angle : angle;
        
        // -------------
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
}
