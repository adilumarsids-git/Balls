using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementController : MonoBehaviour
{
    [Header("Movement")]
    public float moveForce = 10f;
    public float targetMaxSpeed = 5f; // kept for future limiting if you want
    public float resistanceForce = 0.5f; // kept (currently unused, same as your script)

    private float originalTargetMaxSpeed;
    private float originalMoveForce;

    private Rigidbody rb;
    private Vector2 input;

    [Header("Bump Settings")]
    public float collisionImpulseMultiplier = 2.0f;
    public float basePlayerBounceForce = 10f;
    public float boostedCollisionMultiplier = 2.0f;

    [Header("Growth Settings")]
    public float moveForceIncrease = 1f;
    public float maxSpeedIncrease = 1f;
    public float massIncrease = 0.5f;
    public Vector3 scaleIncrease = new Vector3(0.1f, 0.1f, 0.1f);

    [Header("Boost Settings")]
    [Tooltip("Temporary boosted speed/force multiplier.")]
    public float boostMultiplier = 1.5f;
    [Tooltip("Duration of the boost effect.")]
    public float boostDuration = 0.5f;
    [Tooltip("Time delay between boosts.")]
    public float boostCooldown = 5f;

    [Header("Audio")]
    public AudioClip collisionClip;
    public AudioClip boostClip;
    public AudioClip powerUpClip;

    private AudioSource audioSource;

    private bool isBoosting;
    private bool isOnCooldown;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>(); // optional

        originalTargetMaxSpeed = targetMaxSpeed;
        originalMoveForce = moveForce;
    }

    private void Update()
    {
        input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        if (Input.GetKeyDown(KeyCode.Space) && !isOnCooldown && !isBoosting)
        {
            StartBoost();
        }
    }

    private void FixedUpdate()
    {
        MovePlayer(input);
    }

    private void MovePlayer(Vector2 inputAxis)
    {
        Vector3 movement = new Vector3(inputAxis.x, 0f, inputAxis.y) * moveForce;
        rb.AddForce(movement, ForceMode.Force);

        // Optional speed limiting (your old code had this commented out)
        // Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        // if (horizontalVel.magnitude > targetMaxSpeed)
        // {
        //     Vector3 resistanceDir = -horizontalVel.normalized;
        //     float overspeed = horizontalVel.magnitude - targetMaxSpeed;
        //     rb.AddForce(resistanceDir * resistanceForce * overspeed, ForceMode.Force);
        // }
    }

    private void OnCollisionEnter(Collision other)
    {
        // If you collide with another player (same script), bump both.
        if (other.gameObject.TryGetComponent(out PlayerMovementController otherPlayer))
        {
            Vector3 direction = (transform.position - other.transform.position).normalized;

            float rawImpulseMagnitude = other.impulse.magnitude;
            float exaggeratedImpulse = (rawImpulseMagnitude * collisionImpulseMultiplier) + basePlayerBounceForce;

            // Self bounce
            rb.AddForce(direction * exaggeratedImpulse, ForceMode.Impulse);

            // Other player bounce (your multiplayer version did this via RPC; here we do it directly)
            Vector3 otherDir = -direction;
            float otherForceMag = isBoosting
                ? rawImpulseMagnitude * boostedCollisionMultiplier
                : rawImpulseMagnitude;

            otherPlayer.ApplyExternalImpulse(otherDir * otherForceMag);

            PlayOneShot(collisionClip);
        }
    }

    private void ApplyExternalImpulse(Vector3 impulse)
    {
        rb.AddForce(impulse, ForceMode.Impulse);
    }

    public void CollectItem()
    {
        // Growth (single player)
        moveForce += moveForceIncrease;
        targetMaxSpeed += maxSpeedIncrease;
        rb.mass += massIncrease;
        transform.localScale += scaleIncrease;

        originalTargetMaxSpeed = targetMaxSpeed;
        originalMoveForce = moveForce;

        PlayOneShot(powerUpClip);
    }

    private void StartBoost()
    {
        if (isBoosting || isOnCooldown) return;

        isBoosting = true;
        isOnCooldown = true;

        PlayOneShot(boostClip);
        StartCoroutine(BoostCoroutine());
    }

    private IEnumerator BoostCoroutine()
    {
        float boostedTargetMaxSpeed = originalTargetMaxSpeed * boostMultiplier;
        float boostedMoveForce = originalMoveForce * boostMultiplier;

        targetMaxSpeed = boostedTargetMaxSpeed;
        moveForce = boostedMoveForce;

        yield return new WaitForSeconds(boostDuration);

        targetMaxSpeed = originalTargetMaxSpeed;
        moveForce = originalMoveForce;

        isBoosting = false;

        yield return new WaitForSeconds(boostCooldown);
        isOnCooldown = false;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
