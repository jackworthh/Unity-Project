using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class FirstPersonController : MonoBehaviour
{
    public Transform head;
    public TextMeshProUGUI moneyText;

    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.6f;
    public float crawlMultiplier = 0.4f;
    public float jumpForce = 5f;

    public float acceleration = 14f;
    public float deceleration = 16f;
    public float airControl = 0.35f;

    public float mouseSensitivity = 0.08f;
    public float maxLookAngle = 85f;

    public float crawlHeight = 0.1f;
    public float heightTransitionSpeed = 10f;

    public float interactDistance = 3f;
    public float moneyWeight = 1f;
    public float currentWeight = 0f;
    public float weightSlowFactor = 0.1f;
    public float minSpeedMultiplier = 0.4f;

    public float bobFrequency = 1.8f;
    public float bobAmplitude = 0.04f;
    public float sprintBobMultiplier = 1.35f;
    public float bobReturnSpeed = 12f;
    public float bobLerpSpeed = 18f;

    public float animSmooth = 10f;
    public float walkAnimValue = 1f;
    public float sprintAnimValue = 1.6f;
    public float crawlAnimValue = 0.6f;

    Rigidbody rb;
    CapsuleCollider cap;
    Animator anim;

    float pitch;
    float normalHeight;
    bool moving;
    bool sprinting;
    bool crawling;

    Vector3 headBaseLocalPos;
    float bobTimer;
    float animSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cap = GetComponent<CapsuleCollider>();
        anim = GetComponentInChildren<Animator>();

        rb.freezeRotation = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        normalHeight = cap.height;
        headBaseLocalPos = head.localPosition;

        UpdateMoneyUI();
    }

    void Update()
    {
        Look();
        Crawl();
        Jump();
        Interact();
        UpdateAnim();
    }

    void FixedUpdate()
    {
        Move();
    }

    void LateUpdate()
    {
        HeadBob();
    }

    void Look()
    {
        Vector2 d = Mouse.current.delta.ReadValue();

        transform.Rotate(0f, d.x * mouseSensitivity, 0f);

        pitch -= d.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        float x = 0;
        float z = 0;

        if (Keyboard.current.aKey.isPressed) x -= 1;
        if (Keyboard.current.dKey.isPressed) x += 1;
        if (Keyboard.current.wKey.isPressed) z += 1;
        if (Keyboard.current.sKey.isPressed) z -= 1;

        Vector3 input = new Vector3(x, 0f, z).normalized;
        moving = input.sqrMagnitude > 0.001f;

        sprinting = Keyboard.current.leftShiftKey.isPressed && !crawling;

        float speed = moveSpeed;

        float weightMult = 1f - currentWeight * weightSlowFactor;
        if (weightMult < minSpeedMultiplier) weightMult = minSpeedMultiplier;

        speed *= weightMult;

        if (sprinting) speed *= sprintMultiplier;
        if (crawling) speed *= crawlMultiplier;

        Vector3 want = transform.TransformDirection(input) * speed;

        Vector3 v = rb.linearVelocity;
        Vector3 change = want - new Vector3(v.x, 0f, v.z);

        bool inAir = Mathf.Abs(rb.linearVelocity.y) > 0.05f;
        float control = inAir ? airControl : 1f;

        float accel = moving ? acceleration : deceleration;

        Vector3 limited = Vector3.ClampMagnitude(change, accel * Time.fixedDeltaTime);
        rb.AddForce(limited * control, ForceMode.VelocityChange);
    }

    void Jump()
    {
        if (!crawling &&
            Keyboard.current.spaceKey.wasPressedThisFrame &&
            Mathf.Abs(rb.linearVelocity.y) < 0.05f)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }
    }

    void Crawl()
    {
        crawling = Keyboard.current.leftCtrlKey.isPressed;

        float target = crawling ? crawlHeight : normalHeight;

        float oldH = cap.height;
        cap.height = Mathf.Lerp(cap.height, target, heightTransitionSpeed * Time.deltaTime);

        float diff = cap.height - oldH;
        cap.center += new Vector3(0f, diff * 0.5f, 0f);

        Vector3 b = headBaseLocalPos;
        b.y = cap.height - 0.1f;
        headBaseLocalPos = Vector3.Lerp(headBaseLocalPos, b, heightTransitionSpeed * Time.deltaTime);
    }

    void Interact()
    {
        if (!Keyboard.current.eKey.wasPressedThisFrame) return;

        Ray r = new Ray(Camera.main.transform.position, Camera.main.transform.forward);

        if (Physics.Raycast(r, out RaycastHit hit, interactDistance))
        {
            if (hit.collider.CompareTag("Money"))
            {
                currentWeight += moneyWeight;
                UpdateMoneyUI();
                Destroy(hit.collider.gameObject);
            }
        }
    }

    void UpdateMoneyUI()
    {
        moneyText.text = "Money: " + currentWeight.ToString("0");
    }

    void UpdateAnim()
    {
        float target;

        if (!moving) target = 0f;
        else if (crawling) target = crawlAnimValue;
        else if (sprinting) target = sprintAnimValue;
        else target = walkAnimValue;

        animSpeed = Mathf.Lerp(animSpeed, target, animSmooth * Time.deltaTime);

        anim.SetFloat("Speed", animSpeed);
        anim.SetBool("IsCrawling", crawling);
    }

    void HeadBob()
    {
        if (!moving || crawling)
        {
            head.localPosition = Vector3.Lerp(head.localPosition, headBaseLocalPos, bobReturnSpeed * Time.deltaTime);
            bobTimer = 0f;
            return;
        }

        float freq = bobFrequency;
        float amp = bobAmplitude;

        if (sprinting)
        {
            freq *= sprintBobMultiplier;
            amp *= 1.15f;
        }

        bobTimer += Time.deltaTime * freq;

        float b1 = Mathf.Sin(bobTimer * Mathf.PI * 2f) * amp;
        float b2 = Mathf.Sin(bobTimer * Mathf.PI * 4f) * (amp * 0.35f);

        Vector3 t = headBaseLocalPos + new Vector3(0f, b1 + b2, 0f);
        head.localPosition = Vector3.Lerp(head.localPosition, t, bobLerpSpeed * Time.deltaTime);
    }
}
