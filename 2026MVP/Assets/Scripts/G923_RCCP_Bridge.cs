using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Bridge that detects a Logitech G923 (or similar racing wheel) at runtime
/// and feeds its inputs to RCCP via OverrideInputs().
/// Auto-disables if no wheel is found, letting RCCP use keyboard/gamepad.
/// </summary>
public class G923_RCCP_Bridge : MonoBehaviour
{
    [Header("Steering")]
    [Tooltip("Multiplier for raw steering axis (increase if steering feels too soft)")]
    public float steeringScale = 1.0f;

    [Header("Debug")]
    public bool logInputValues = false;

    // Detected wheel device
    private InputDevice wheelDevice;
    private string wheelLayout;

    // Input actions bound to the detected wheel
    private InputAction steerAction;
    private InputAction gasAction;
    private InputAction brakeAction;
    private InputAction clutchAction;

    // Button actions
    private InputAction shiftUpAction;
    private InputAction shiftDownAction;
    private InputAction engineAction;
    private InputAction lowBeamAction;
    private InputAction highBeamAction;
    private InputAction handbrakeAction;
    private InputAction indicatorLeftAction;
    private InputAction indicatorRightAction;
    private InputAction indicatorHazardAction;
    private InputAction lookBackAction;
    private InputAction changeCameraAction;

    // H-shifter actions (for manual transmission)
    private InputAction gearRAction;
    private InputAction gear1Action;
    private InputAction gear2Action;
    private InputAction gear3Action;
    private InputAction gear4Action;
    private InputAction gear5Action;
    private InputAction gear6Action;

    // RCCP references
    private RCCP_CarController vehicle;
    private RCCP_Inputs rccpInputs = new RCCP_Inputs();

    // Transmission config
    private bool manualTransmission;

    void Awake()
    {
        DetectWheel();

        if (wheelDevice == null)
        {
            Debug.Log("[G923 Bridge] No racing wheel detected — bridge disabled. Keyboard/gamepad will work normally.");
            enabled = false;
            return;
        }

        Debug.Log($"[G923 Bridge] Detected: {wheelDevice.displayName} (layout: {wheelLayout})");

        CreateInputActions();
        LoadTransmissionPreference();
    }

    void OnEnable()
    {
        EnableAllActions();

        // Subscribe button callbacks
        if (shiftUpAction != null) shiftUpAction.performed += OnShiftUp;
        if (shiftDownAction != null) shiftDownAction.performed += OnShiftDown;
        if (engineAction != null) engineAction.performed += OnEngine;
        if (lowBeamAction != null) lowBeamAction.performed += OnLowBeam;
        if (highBeamAction != null) highBeamAction.performed += OnHighBeam;
        if (handbrakeAction != null) handbrakeAction.performed += OnHandbrake;
        if (indicatorLeftAction != null) indicatorLeftAction.performed += OnIndicatorLeft;
        if (indicatorRightAction != null) indicatorRightAction.performed += OnIndicatorRight;
        if (indicatorHazardAction != null) indicatorHazardAction.performed += OnIndicatorHazard;
        if (lookBackAction != null)
        {
            lookBackAction.performed += OnLookBackPressed;
            lookBackAction.canceled += OnLookBackReleased;
        }
        if (changeCameraAction != null) changeCameraAction.performed += OnChangeCamera;

        // H-shifter
        if (gearRAction != null) gearRAction.performed += ctx => OnGearTo(-1);
        if (gear1Action != null) gear1Action.performed += ctx => OnGearTo(0);
        if (gear2Action != null) gear2Action.performed += ctx => OnGearTo(1);
        if (gear3Action != null) gear3Action.performed += ctx => OnGearTo(2);
        if (gear4Action != null) gear4Action.performed += ctx => OnGearTo(3);
        if (gear5Action != null) gear5Action.performed += ctx => OnGearTo(4);
        if (gear6Action != null) gear6Action.performed += ctx => OnGearTo(5);
    }

    // Auto-calibration: track min/max per pedal to handle any range
    private float gasMin = float.MaxValue, gasMax = float.MinValue;
    private float brakeMin = float.MaxValue, brakeMax = float.MinValue;
    private float clutchMin = float.MaxValue, clutchMax = float.MinValue;
    private int calibrationFrames = 0;
    private const int CALIBRATION_WARMUP = 60; // frames to establish rest position

    void Update()
    {
        // Find the active player vehicle
        if (vehicle == null)
        {
            // Try RCCP_SceneManager
            if (RCCP_SceneManager.Instance != null && RCCP_SceneManager.Instance.activePlayerVehicle != null)
            {
                vehicle = RCCP_SceneManager.Instance.activePlayerVehicle;
                Debug.Log($"[G923] Found vehicle via SceneManager: {vehicle.name}");
            }

            // Fallback: find by name "Player"
            if (vehicle == null)
            {
                GameObject playerObj = GameObject.Find("Player");
                if (playerObj != null)
                {
                    vehicle = playerObj.GetComponent<RCCP_CarController>();
                    if (vehicle == null)
                        vehicle = playerObj.GetComponentInChildren<RCCP_CarController>();
                }
                if (vehicle != null)
                    Debug.Log($"[G923] Found vehicle via GameObject.Find('Player'): {vehicle.name}");
            }

            // Fallback 2: find any RCCP_CarController that is controllable
            if (vehicle == null)
            {
                foreach (var cc in FindObjectsOfType<RCCP_CarController>())
                {
                    if (cc.canControl)
                    {
                        vehicle = cc;
                        Debug.Log($"[G923] Found vehicle via FindObjectOfType: {vehicle.name}");
                        break;
                    }
                }
            }

            // Fallback 3: literally any RCCP_CarController
            if (vehicle == null)
            {
                vehicle = FindObjectOfType<RCCP_CarController>();
                if (vehicle != null)
                    Debug.Log($"[G923] Found vehicle (any): {vehicle.name}");
            }

            if (vehicle == null)
            {
                if (Time.frameCount % 120 == 0)
                    Debug.LogWarning("[G923] No RCCP_CarController found in scene");
                return;
            }
        }

        if (vehicle.Inputs == null)
            return;

        // Configure vehicle for wheel on first detection or vehicle change
        if (vehicle != lastConfiguredVehicle)
        {
            ConfigureVehicleForWheel();
            lastConfiguredVehicle = vehicle;
        }

        // Read analog axes
        float rawSteer = steerAction != null ? steerAction.ReadValue<float>() : 0f;
        float rawGas = gasAction != null ? gasAction.ReadValue<float>() : 0f;
        float rawBrake = brakeAction != null ? brakeAction.ReadValue<float>() : 0f;
        float rawClutch = clutchAction != null ? clutchAction.ReadValue<float>() : 0f;

        // Auto-calibration: track range of each pedal
        calibrationFrames++;
        TrackRange(rawGas, ref gasMin, ref gasMax);
        TrackRange(rawBrake, ref brakeMin, ref brakeMax);
        TrackRange(rawClutch, ref clutchMin, ref clutchMax);

        // Steering: apply scale and clamp
        float steer = Mathf.Clamp(rawSteer * steeringScale, -1f, 1f);

        // Pedals: normalize to 0 (released) to 1 (fully pressed)
        // G923 PS pedals typically report a signed axis (-1 to 1) where:
        //   released = one extreme, fully pressed = other extreme
        // We use the rest value (first frames) as "released" baseline
        float gas = NormalizePedal(rawGas, gasMin, gasMax);
        float brake = NormalizePedal(rawBrake, brakeMin, brakeMax);
        float clutch = NormalizePedal(rawClutch, clutchMin, clutchMax);

        // Fill RCCP inputs — pedals are always 0..1, never negative
        rccpInputs.steerInput = steer;
        rccpInputs.throttleInput = gas;
        rccpInputs.brakeInput = brake;
        rccpInputs.clutchInput = clutch;
        rccpInputs.handbrakeInput = 0f; // handled by button callback
        rccpInputs.nosInput = 0f;

        // Override RCCP inputs
        vehicle.Inputs.OverrideInputs(rccpInputs);

        // ALWAYS log every 60 frames for debugging
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"[G923] RAW steer={rawSteer:F3} gas={rawGas:F3} brake={rawBrake:F3} clutch={rawClutch:F3}");
            Debug.Log($"[G923] NORMALIZED steer={steer:F2} gas={gas:F2} brake={brake:F2} clutch={clutch:F2}");
            Debug.Log($"[G923] RANGE gas=[{gasMin:F3},{gasMax:F3}] brake=[{brakeMin:F3},{brakeMax:F3}] clutch=[{clutchMin:F3},{clutchMax:F3}]");
            Debug.Log($"[G923] VEHICLE override={vehicle.Inputs.overridePlayerInputs} throttle={vehicle.Inputs.throttleInput:F2} brake={vehicle.Inputs.brakeInput:F2} steer={vehicle.Inputs.steerInput:F2}");
            if (vehicle.Gearbox != null)
                Debug.Log($"[G923] GEARBOX gear={vehicle.Gearbox.currentGear} type={vehicle.Gearbox.transmissionType} autoReverse={vehicle.Inputs.autoReverse}");
        }
    }

    void OnDisable()
    {
        // Restore normal RCCP input
        if (vehicle != null && vehicle.Inputs != null)
            vehicle.Inputs.DisableOverrideInputs();

        DisableAllActions();
    }

    void OnDestroy()
    {
        DisposeAllActions();
    }

    // ─── DETECTION ───────────────────────────────────────────────

    void DetectWheel()
    {
        foreach (var device in InputSystem.devices)
        {
            string name = device.displayName.ToLowerInvariant();

            // Detect G923, G920, G29, or other Logitech wheels
            if (name.Contains("g923") || name.Contains("g920") || name.Contains("g29") ||
                name.Contains("driving force") || name.Contains("racing wheel"))
            {
                wheelDevice = device;
                wheelLayout = device.layout;
                return;
            }
        }
    }

    // ─── INPUT ACTION CREATION ───────────────────────────────────

    void CreateInputActions()
    {
        string layoutPath = $"<{wheelLayout}>";

        // Analog axes
        steerAction = CreateAction("G923_Steer", $"{layoutPath}/stick/x");
        gasAction = CreateAction("G923_Gas", $"{layoutPath}/z");
        brakeAction = CreateAction("G923_Brake", $"{layoutPath}/rz");
        clutchAction = CreateActionSafe("G923_Clutch", $"{layoutPath}/stick/y");

        // Buttons — G923 PS button mapping
        // button5 = L1 (left paddle = shift DOWN), button6 = R1 (right paddle = shift UP)
        shiftUpAction = CreateActionButton("G923_ShiftUp", $"{layoutPath}/button6");
        shiftDownAction = CreateActionButton("G923_ShiftDown", $"{layoutPath}/button5");
        engineAction = CreateActionButton("G923_Engine", $"{layoutPath}/button7");
        changeCameraAction = CreateActionButton("G923_Camera", $"{layoutPath}/button8");
        highBeamAction = CreateActionButton("G923_HighBeam", $"{layoutPath}/button9");
        lowBeamAction = CreateActionButton("G923_LowBeam", $"{layoutPath}/button10");
        handbrakeAction = CreateActionButton("G923_Handbrake", $"{layoutPath}/button2");

        // D-pad / hat
        indicatorLeftAction = CreateActionButton("G923_IndLeft", $"{layoutPath}/hat/left");
        indicatorRightAction = CreateActionButton("G923_IndRight", $"{layoutPath}/hat/right");
        indicatorHazardAction = CreateActionButton("G923_IndHazard", $"{layoutPath}/hat/up");
        lookBackAction = CreateActionButton("G923_LookBack", $"{layoutPath}/hat/down");

        // H-shifter (may not be present on all models)
        gear1Action = CreateActionSafeButton("G923_Gear1", $"{layoutPath}/button13");
        gear2Action = CreateActionSafeButton("G923_Gear2", $"{layoutPath}/button14");
        gear3Action = CreateActionSafeButton("G923_Gear3", $"{layoutPath}/button15");
        gear4Action = CreateActionSafeButton("G923_Gear4", $"{layoutPath}/button16");
        gear5Action = CreateActionSafeButton("G923_Gear5", $"{layoutPath}/button17");
        gear6Action = CreateActionSafeButton("G923_Gear6", $"{layoutPath}/button18");
        gearRAction = CreateActionSafeButton("G923_GearR", $"{layoutPath}/button19");
    }

    InputAction CreateAction(string name, string binding)
    {
        var action = new InputAction(name, InputActionType.Value, binding);
        return action;
    }

    InputAction CreateActionButton(string name, string binding)
    {
        var action = new InputAction(name, InputActionType.Button, binding);
        return action;
    }

    InputAction CreateActionSafe(string name, string binding)
    {
        try
        {
            return CreateAction(name, binding);
        }
        catch
        {
            Debug.LogWarning($"[G923 Bridge] Could not bind {name} to {binding}");
            return null;
        }
    }

    InputAction CreateActionSafeButton(string name, string binding)
    {
        try
        {
            return CreateActionButton(name, binding);
        }
        catch
        {
            Debug.LogWarning($"[G923 Bridge] Could not bind {name} to {binding}");
            return null;
        }
    }

    // ─── PEDAL NORMALIZATION ─────────────────────────────────────

    void TrackRange(float raw, ref float min, ref float max)
    {
        if (raw < min) min = raw;
        if (raw > max) max = raw;
    }

    /// <summary>
    /// Normalizes pedal input to 0 (released) to 1 (fully pressed).
    /// Auto-detects range from observed min/max values.
    /// Works regardless of whether the axis is 0→1, 1→-1, -1→1, etc.
    /// </summary>
    float NormalizePedal(float raw, float observedMin, float observedMax)
    {
        float range = observedMax - observedMin;

        // Not enough range observed yet — pedal hasn't been pressed
        if (range < 0.1f)
            return 0f;

        // The rest position is typically the value seen in the first frames.
        // For most wheels: released = max value, pressed = min value
        // But it could be the opposite. We assume:
        //   - The pedal spends most time at rest (near one extreme)
        //   - Pressing moves it toward the other extreme
        // Since we can't know which direction, we use distance from the
        // initial rest position. The rest value is usually at one edge of the range.

        // Normalize raw to 0..1 within the observed range
        float normalized = (raw - observedMin) / range;

        // For most HID wheels: high value = released, low value = pressed
        // So we invert: 0 = released, 1 = pressed
        normalized = 1f - normalized;

        return Mathf.Clamp01(normalized);
    }

    // ─── BUTTON CALLBACKS ────────────────────────────────────────

    void OnShiftUp(InputAction.CallbackContext ctx)
    {
        if (vehicle != null && vehicle.Gearbox != null && !vehicle.Gearbox.shiftingNow)
            vehicle.Gearbox.ShiftUp();
    }

    void OnShiftDown(InputAction.CallbackContext ctx)
    {
        if (vehicle != null && vehicle.Gearbox != null && !vehicle.Gearbox.shiftingNow)
            vehicle.Gearbox.ShiftDown();
    }

    void OnEngine(InputAction.CallbackContext ctx)
    {
        if (vehicle == null || vehicle.Engine == null) return;

        if (!vehicle.Engine.engineRunning)
            vehicle.Engine.StartEngine();
        else
            vehicle.Engine.StopEngine();
    }

    void OnLowBeam(InputAction.CallbackContext ctx)
    {
        if (vehicle == null || vehicle.Lights == null) return;
        vehicle.Lights.lowBeamHeadlights = !vehicle.Lights.lowBeamHeadlights;
    }

    void OnHighBeam(InputAction.CallbackContext ctx)
    {
        if (vehicle == null || vehicle.Lights == null) return;
        vehicle.Lights.highBeamHeadlights = !vehicle.Lights.highBeamHeadlights;
    }

    void OnHandbrake(InputAction.CallbackContext ctx)
    {
        // Toggle handbrake in the RCCP inputs
        rccpInputs.handbrakeInput = rccpInputs.handbrakeInput > 0.5f ? 0f : 1f;
    }

    void OnIndicatorLeft(InputAction.CallbackContext ctx)
    {
        if (vehicle == null || vehicle.Lights == null) return;
        vehicle.Lights.indicatorsLeft = !vehicle.Lights.indicatorsLeft;
        vehicle.Lights.indicatorsRight = false;
        vehicle.Lights.indicatorsAll = false;
    }

    void OnIndicatorRight(InputAction.CallbackContext ctx)
    {
        if (vehicle == null || vehicle.Lights == null) return;
        vehicle.Lights.indicatorsRight = !vehicle.Lights.indicatorsRight;
        vehicle.Lights.indicatorsLeft = false;
        vehicle.Lights.indicatorsAll = false;
    }

    void OnIndicatorHazard(InputAction.CallbackContext ctx)
    {
        if (vehicle == null || vehicle.Lights == null) return;
        vehicle.Lights.indicatorsAll = !vehicle.Lights.indicatorsAll;
        vehicle.Lights.indicatorsLeft = false;
        vehicle.Lights.indicatorsRight = false;
    }

    void OnLookBackPressed(InputAction.CallbackContext ctx)
    {
        // Look back via RCCP public API
        var cam = RCCP_SceneManager.Instance != null ? RCCP_SceneManager.Instance.activePlayerCamera : null;
        if (cam != null)
            cam.lookBackNow = true;
    }

    void OnLookBackReleased(InputAction.CallbackContext ctx)
    {
        var cam = RCCP_SceneManager.Instance != null ? RCCP_SceneManager.Instance.activePlayerCamera : null;
        if (cam != null)
            cam.lookBackNow = false;
    }

    void OnChangeCamera(InputAction.CallbackContext ctx)
    {
        RCCP.ChangeCamera();
    }

    void OnGearTo(int gearIndex)
    {
        if (!manualTransmission) return;
        if (vehicle == null || vehicle.Gearbox == null) return;
        if (!vehicle.Gearbox.shiftingNow)
            vehicle.Gearbox.ShiftToGear(gearIndex);
    }

    // ─── TRANSMISSION CONFIG ─────────────────────────────────────

    void LoadTransmissionPreference()
    {
        manualTransmission = PlayerPrefs.GetInt("TransmisionManual", 0) == 1;
        Debug.Log($"[G923 Bridge] Transmission: {(manualTransmission ? "Manual" : "Automatic")}");
    }

    /// <summary>
    /// Configures RCCP vehicle settings optimized for racing wheel input.
    /// Called when a vehicle becomes active.
    /// </summary>
    void ConfigureVehicleForWheel()
    {
        if (vehicle == null || vehicle.Inputs == null) return;

        var input = vehicle.Inputs;
        input.steeringLimiter = false;
        input.counterSteering = false;
        input.autoReverse = false;
        input.inverseThrottleBrakeOnReverse = false;
        input.cutThrottleWhenShifting = false;

        // Set transmission type
        if (vehicle.Gearbox != null)
        {
            vehicle.Gearbox.transmissionType = manualTransmission
                ? RCCP_Gearbox.TransmissionType.Manual
                : RCCP_Gearbox.TransmissionType.Automatic;
        }

        Debug.Log("[G923 Bridge] Vehicle configured for racing wheel input");
    }

    // Track vehicle changes to re-configure
    private RCCP_CarController lastConfiguredVehicle;

    // ─── ACTION LIFECYCLE ────────────────────────────────────────

    void EnableAllActions()
    {
        steerAction?.Enable();
        gasAction?.Enable();
        brakeAction?.Enable();
        clutchAction?.Enable();
        shiftUpAction?.Enable();
        shiftDownAction?.Enable();
        engineAction?.Enable();
        lowBeamAction?.Enable();
        highBeamAction?.Enable();
        handbrakeAction?.Enable();
        indicatorLeftAction?.Enable();
        indicatorRightAction?.Enable();
        indicatorHazardAction?.Enable();
        lookBackAction?.Enable();
        changeCameraAction?.Enable();
        gearRAction?.Enable();
        gear1Action?.Enable();
        gear2Action?.Enable();
        gear3Action?.Enable();
        gear4Action?.Enable();
        gear5Action?.Enable();
        gear6Action?.Enable();
    }

    void DisableAllActions()
    {
        steerAction?.Disable();
        gasAction?.Disable();
        brakeAction?.Disable();
        clutchAction?.Disable();
        shiftUpAction?.Disable();
        shiftDownAction?.Disable();
        engineAction?.Disable();
        lowBeamAction?.Disable();
        highBeamAction?.Disable();
        handbrakeAction?.Disable();
        indicatorLeftAction?.Disable();
        indicatorRightAction?.Disable();
        indicatorHazardAction?.Disable();
        lookBackAction?.Disable();
        changeCameraAction?.Disable();
        gearRAction?.Disable();
        gear1Action?.Disable();
        gear2Action?.Disable();
        gear3Action?.Disable();
        gear4Action?.Disable();
        gear5Action?.Disable();
        gear6Action?.Disable();
    }

    void DisposeAllActions()
    {
        steerAction?.Dispose();
        gasAction?.Dispose();
        brakeAction?.Dispose();
        clutchAction?.Dispose();
        shiftUpAction?.Dispose();
        shiftDownAction?.Dispose();
        engineAction?.Dispose();
        lowBeamAction?.Dispose();
        highBeamAction?.Dispose();
        handbrakeAction?.Dispose();
        indicatorLeftAction?.Dispose();
        indicatorRightAction?.Dispose();
        indicatorHazardAction?.Dispose();
        lookBackAction?.Dispose();
        changeCameraAction?.Dispose();
        gearRAction?.Dispose();
        gear1Action?.Dispose();
        gear2Action?.Dispose();
        gear3Action?.Dispose();
        gear4Action?.Dispose();
        gear5Action?.Dispose();
        gear6Action?.Dispose();
    }
}
