using Godot;

public partial class OrbitCamera : Node3D
{
    [Export] public float Distance = 6.0f;
    [Export] public float MinDistance = 2.0f;
    [Export] public float MaxDistance = 20.0f;
    [Export] public float RotationSpeed = 0.01f;
    [Export] public float ZoomSpeed = 1.2f;
    [Export] public float MoveSpeed = 8.0f;
    [Export] public float VerticalSpeed = 6.0f;
    [Export] public bool EnableMovement = true;

    private float _pitch;
    private float _yaw;
    private bool _rotating;
    private Camera3D? _camera;

    public override void _Ready()
    {
        EnsureInputActions();
        _camera = GetNodeOrNull<Camera3D>("Camera3D");
        if (_camera != null)
        {
            _camera.Position = new Vector3(0, 0, Distance);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                _rotating = mouseButton.Pressed;
                if (_rotating)
                {
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                }
                else
                {
                    Input.MouseMode = Input.MouseModeEnum.Visible;
                }
            }

            if (mouseButton.Pressed)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    Distance = Mathf.Max(MinDistance, Distance - ZoomSpeed);
                    UpdateCamera();
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    Distance = Mathf.Min(MaxDistance, Distance + ZoomSpeed);
                    UpdateCamera();
                }
            }
        }

        if (@event is InputEventMouseMotion motion && _rotating)
        {
            _yaw -= motion.Relative.X * RotationSpeed;
            _pitch -= motion.Relative.Y * RotationSpeed;

            float minPitch = Mathf.DegToRad(-80.0f);
            float maxPitch = Mathf.DegToRad(80.0f);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Rotation = new Vector3(_pitch, _yaw, 0);
        }
    }

    public override void _Process(double delta)
    {
        if (!EnableMovement)
        {
            return;
        }

        Vector3 forward = -GlobalBasis.Z;
        Vector3 right = GlobalBasis.X;
        forward.Y = 0;
        right.Y = 0;
        if (forward.LengthSquared() > 0)
        {
            forward = forward.Normalized();
        }

        if (right.LengthSquared() > 0)
        {
            right = right.Normalized();
        }

        Vector3 move = Vector3.Zero;
        if (Input.IsActionPressed("move_forward"))
        {
            move += forward;
        }

        if (Input.IsActionPressed("move_back"))
        {
            move -= forward;
        }

        if (Input.IsActionPressed("move_left"))
        {
            move -= right;
        }

        if (Input.IsActionPressed("move_right"))
        {
            move += right;
        }

        if (move != Vector3.Zero)
        {
            GlobalPosition += move.Normalized() * MoveSpeed * (float)delta;
        }

        float vertical = 0f;
        if (Input.IsActionPressed("move_up"))
        {
            vertical += 1f;
        }

        if (Input.IsActionPressed("move_down"))
        {
            vertical -= 1f;
        }

        if (Mathf.Abs(vertical) > 0.001f)
        {
            GlobalPosition += Vector3.Up * vertical * VerticalSpeed * (float)delta;
        }
    }

    public void Focus(Vector3 target, float radius)
    {
        GlobalPosition = target;
        float desired = Mathf.Clamp(radius * 2.4f, MinDistance, MaxDistance);
        Distance = desired;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (_camera != null)
        {
            _camera.Position = new Vector3(0, 0, Distance);
        }
    }

    private static void EnsureInputActions()
    {
        AddAction("move_forward", Key.W);
        AddAction("move_back", Key.S);
        AddAction("move_left", Key.A);
        AddAction("move_right", Key.D);
        AddAction("move_up", Key.Space);
        AddAction("move_down", Key.C);
    }

    private static void AddAction(string name, Key key)
    {
        if (InputMap.HasAction(name))
        {
            return;
        }

        InputMap.AddAction(name);
        InputEventKey ev = new()
        {
            Keycode = key,
        };
        InputMap.ActionAddEvent(name, ev);
    }
}
