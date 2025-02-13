﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallPlayer : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f;

    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 10f;

    [SerializeField]
	Rect allowedArea = new Rect(-5f, -5f, 10f, 10f);

    Vector3 velocity;

    void Update()
    {
        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);
        Vector3 desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;
        float maxSpeedChange = maxAcceleration * Time.deltaTime;

        // if (velocity.x < desiredVelocity.x)
        // {
        //     velocity.x = Mathf.Min(velocity.x + maxSpeedChange, desiredVelocity.x);
        // }
        // else if (velocity.x > desiredVelocity.x)
        // {
        //     velocity.x = Mathf.Max(velocity.x - maxSpeedChange, desiredVelocity.x);
        // }
        velocity.x = Mathf.MoveTowards(velocity.x, desiredVelocity.x, maxSpeedChange);
        velocity.z = Mathf.MoveTowards(velocity.z, desiredVelocity.z, maxSpeedChange);

        Vector3 displacement = velocity * Time.deltaTime;
        Vector3 newPosition = transform.localPosition + displacement;
        if (!allowedArea.Contains(new Vector2(newPosition.x, newPosition.z)))
        {

            newPosition.x = Mathf.Clamp(newPosition.x, allowedArea.xMin, allowedArea.xMax);
            newPosition.z = Mathf.Clamp(newPosition.z, allowedArea.yMin, allowedArea.yMax);

        }
        transform.localPosition = newPosition;   
    }
}
