using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class RigidbodyBallPlayer : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)]
    float _maxSpeed = 10f;

    // ! Rework acceleration system, it isn't responsive enough
    [SerializeField, Range(0f, 1000f)]
    float _maxAcceleration = 10f, _maxAirAcceleration = 1f;

    [SerializeField, Range(0f, 10f)]
    float _maxJumpHeight = 2f;

    [SerializeField, Range(0, 5)]
    int _maxAirJumps = 0;

    [SerializeField, Range(0f, 100f)]
    [Tooltip("Used to avoid ground snapping while speed is high enough. Warning! Shouldn't be equal to MaxSpeed")]
    float _maxSnapSpeed = 100f; // ! Warning, shouldn't be equal to maxSpeed

    [SerializeField, Range(0, 90)]
    float _maxGroundAngle = 25f, _maxStairsAngle = 50f;

    [SerializeField, Min(0f)]
    float _maxSnapProbeDistance = 1f;

    [SerializeField]
    LayerMask _groundSnapProbeMask = -1, _stairsMask = -1;

    [SerializeField]
    bool _printDebugs = false;

    [SerializeField]
    Button _debugContinueButton;

    /* 
     Interpolate mode of a Rigidbody. Setting it to Interpolate makes it linearly 
     interpolate between its last and current position, so it will lag a bit behind 
     its actual position according to PhysX. The other option is Extrapolate, which 
     interpolates to its guessed position according to its velocity, which is only
     really acceptable for objects that have a mostly constant velocity.
    */
    Rigidbody _body;

    public Vector3 _velocity;
    Vector3 _desiredVelocity;
    Vector3 _contactNormal, _steepContactNormal;
    bool _desiredJump;
    public int _jumpPhase;
    int _groundContactCount, _steepContactCount;
    float _minGroundDotProduct, _minStairsDotProduct;

    bool IsGrounded => _groundContactCount > 0;
    bool OnSteep => _steepContactCount > 0;

    void Awake()
    {
        // -------------

        _body = GetComponent<Rigidbody>();
        OnValidate();

        // -------------
    }

    void OnValidate()
    {
        // -------------

        // ? If we are then comparing it against the y component of normal vectors why do we use cos and not sin
        // ? I know it is dot product but
        _minGroundDotProduct = Mathf.Cos(_maxGroundAngle * Mathf.Deg2Rad);
        _minStairsDotProduct = Mathf.Cos(_maxStairsAngle * Mathf.Deg2Rad);

        // -------------
    }

    void Update()
    {
        // -------------

        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f); // ? is it another way to normalize

        // Update desired velocity on each frame
        _desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * _maxSpeed;

        // * Cool use of OR assignment, this will only be able to set to true
        _desiredJump |= Input.GetButtonDown("Jump");

        // GetComponent<Renderer>().material.SetColor(
        //     "_Color", IsGrounded ? Color.black : Color.white
        // );

        // -------------
    }

    void FixedUpdate()
    {
        // -------------

        UpdateContactState();
        AdjustVelocity();

        if (_desiredJump)
        {
            _desiredJump = false;
            Jump();
        }


        _body.velocity = _velocity;
        ClearState();

        // -------------
    }

    void UpdateContactState()
    {
        // -------------

        _velocity = _body.velocity;

        if (IsGrounded || SnapToGround() || CheckSteepContacts())
        {
            _jumpPhase = 0;

            if (_groundContactCount > 1)
            {
                // Normalize in case there were more than once contact normals and we take their average
                _contactNormal.Normalize();
            }
        }
        else
        {
            // Reset contact normal
            _contactNormal = Vector3.up;
        }

        // -------------
    }

    /*
    Manage snapping to ground on low displacements due to hitting edges etc.
    */
    private bool SnapToGround()
    {
        // -------------

        if (_jumpPhase > 0)
        {        
            return false;
        }

        // We want to avoid snapping for greater speeds
        float speed = _velocity.magnitude;
        if (speed > _maxSnapSpeed)
        {
            return false;
        }
        /*
        Note that we're only considering a single point below us to decide whether we're above ground. 
        This works fine as long as the level geometry isn't too noisy nor too detailed. For example a 
        tiny deep crack could cause this to fail if the ray happened to be cast into it.
        */
        if (!Physics.Raycast(
            _body.position, Vector3.down, out RaycastHit hit,
            _maxSnapProbeDistance, _groundSnapProbeMask
        ))
        {
            return false;
        }

        if (hit.normal.y < GetMinGroundedDotByLayer(hit.collider.gameObject.layer))
        {
            return false;
        }

        _groundContactCount = 1;
        _contactNormal = hit.normal;

        // We also need to align velocity since we will return true on "being grounded"
        // ? I have no idea for its real usefullness, also shouldnt velocity be normalized then
        float dot = Vector3.Dot(_velocity, hit.normal);
        if (dot > 0f)
        {
            _velocity = (_velocity - hit.normal * dot).normalized * speed;
        }

        Debug.Log("Ground snapping");

        return true;

        // -------------
    }

    // TODO I don't like this part, it is a bit cryptic and too specific
    private float GetMinGroundedDotByLayer(int layer)
    {
        // -------------

        return (_stairsMask & (1 << layer)) == 0 ?
            _minGroundDotProduct : _minStairsDotProduct;

        // -------------
    }

    /*
    Handling crevasses
    */
    bool CheckSteepContacts()
    {
        // -------------

        if (_steepContactCount > 1)
        {
            _steepContactNormal.Normalize();

            // TODO check situations where this would be still not satisfied and player would get stuck
            if (_steepContactNormal.y >= _minGroundDotProduct)
            {
                _groundContactCount = 1;
                _contactNormal = _steepContactNormal;
                return true;
            }
        }

        return false;

        // -------------
    }

    void ClearState()
    {
        // -------------

        
        DebugStep("Clearing state" + _jumpPhase, Color.yellow, false);


        _groundContactCount = _steepContactCount = 0;
        _contactNormal = _steepContactNormal = Vector3.zero;

        DebugStep(IsGrounded.ToString(), Color.yellow, false);

        // -------------
    }

    // Without this we will get weird boyancy effect on accelerating down the slope
    // We project the velocity along the slope we are moving over
    void AdjustVelocity()
    {
        // -------------

        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        float currentX = Vector3.Dot(_velocity, xAxis);
        float currentZ = Vector3.Dot(_velocity, zAxis);

        float acceleration = IsGrounded ? _maxAcceleration : _maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;

        float newX = Mathf.MoveTowards(currentX, _desiredVelocity.x, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, _desiredVelocity.z, maxSpeedChange);

        _velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);

        // -------------
    }

    void Jump()
    {
        // -------------

        DebugStep("Trying to jump", Color.red);

        _body.velocity = _velocity;

        if (IsGrounded || _jumpPhase <= _maxAirJumps)
        {
            _jumpPhase += 1;
            float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * _maxJumpHeight);
            float alignedSpeed = Vector3.Dot(_velocity, _contactNormal);
            if (_velocity.y > 0)
            {
                // To limit the jump speed to its maximum
                // * wont this have ridiculous effects when falling
                jumpSpeed = Mathf.Max(jumpSpeed - _velocity.y, 0f);
            };

            // To jump away from ground surface.
            //* That is just a design decision about jump behaviour
            _velocity += _contactNormal * jumpSpeed;

            DebugStep("Jumped" + _jumpPhase, Color.yellow);
        }

        // -------------
    }

    void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        //* This skip is important to avoid IsGrounded overwriting at the beginning of a jump phase
        //todo this will require reworking on wall running probably
        if (_jumpPhase == 0)
        {
            EvaluateCollision(collision);
        }
    }

    private void EvaluateCollision(Collision collision)
    {
        float minDot = GetMinGroundedDotByLayer(collision.gameObject.layer);

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;

            if (normal.y >= minDot)
            {
                // Consider contacts satisfying ground angle condition
                _groundContactCount++;
                _contactNormal += normal; // ? shouldn't we normalize it at the end
            }
            else if (normal.y > -0.01f)
            {
                // Consider steep contacts which are still almost facing up
                // Thus excluding overhangs (except this tiny margin of -0.01f) and ceilings
                _steepContactCount++;
                _steepContactNormal += normal;
            }

            if (IsGrounded)
            {
                DebugStep("Evaluated collision to be grounded", Color.blue, false);
            }
        }
    }

    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        // todo Refresh on dot product:)
        return vector - _contactNormal * Vector3.Dot(vector, _contactNormal);
    }

    void DebugStep(string message, Color color, bool pauseGame = false)
    {
        if (_printDebugs)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>");

            if (pauseGame)
            {
                float fixedDeltaTime = Time.fixedDeltaTime;
                float timeScale = Time.timeScale;

                _debugContinueButton.gameObject.SetActive(true);
                _debugContinueButton.onClick.RemoveAllListeners();
                _debugContinueButton.onClick.AddListener(() => {
                    Time.fixedDeltaTime = fixedDeltaTime;
                    Time.timeScale = timeScale;
                    _debugContinueButton.gameObject.SetActive(false);
                });

                Time.fixedDeltaTime = 0f;
                Time.timeScale = 0f;
            }
        }
    }
}
