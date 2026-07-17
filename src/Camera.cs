using OpenTK.Mathematics;

namespace Mars;

/// <summary>
/// Камера от первого лица: yaw/pitch, WASD по направлению взгляда в 3D.
/// </summary>
public class Camera
{
    public Vector3 Position = Vector3.Zero;

    /// <summary>Ориентация в радианах: X — yaw, Y — pitch.</summary>
    public Vector3 Orientation = new((float)Math.PI, 0f, 0f);

    public float MoveSpeed { get; set; }
    public float MouseSensitivity { get; set; }

    private const float MaxPitch = MathF.PI / 2f - 0.01f;
    private const float MaxMouseDelta = 80f;

    public Camera(float moveSpeed, float mouseSensitivity)
    {
        MoveSpeed = moveSpeed;
        MouseSensitivity = mouseSensitivity;
    }

    public Matrix4 GetViewMatrix()
    {
        var forward = GetLookDirection().Normalized();
        var up = PickUpVector(forward);
        return Matrix4.LookAt(Position, Position + forward, up);
    }

    /// <summary>Up-вектор для LookAt без переворота при взгляде вверх/вниз.</summary>
    private Vector3 PickUpVector(Vector3 forward)
    {
        if (MathF.Abs(forward.Y) < 0.99f)
            return Vector3.UnitY;

        // Вблизи полюса — опора на горизонтальное направление yaw.
        return new Vector3(MathF.Sin(Orientation.X), 0f, MathF.Cos(Orientation.X)).Normalized();
    }

    public Vector3 GetLookDirection()
    {
        float yaw = Orientation.X;
        float pitch = Orientation.Y;

        return new Vector3(
            MathF.Sin(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch),
            MathF.Cos(yaw) * MathF.Cos(pitch));
    }

    public Vector3 GetForward() => GetLookDirection().Normalized();

    public Vector3 GetRight()
    {
        var look = GetForward();
        var right = Vector3.Cross(Vector3.UnitY, look);

        if (right.LengthSquared > float.Epsilon)
            return right.Normalized();

        return new Vector3(MathF.Cos(Orientation.X), 0f, -MathF.Sin(Orientation.X));
    }

    public void MoveRelative(float forward, float right, float deltaTime)
    {
        Vector3 move = Vector3.Zero;

        if (forward != 0f)
            move += GetForward() * forward;
        if (right != 0f)
            move += GetRight() * right;

        if (move.LengthSquared <= float.Epsilon)
            return;

        if (forward != 0f && right != 0f)
            move.Normalize();

        Position += move * (MoveSpeed * deltaTime);
    }

    public void MoveForwardStep(float signedSteps, float stepDistance)
    {
        if (signedSteps == 0f)
            return;

        Position += GetForward() * (signedSteps * stepDistance);
    }

    public void AddRotation(float deltaX, float deltaY)
    {
        deltaX = Math.Clamp(deltaX, -MaxMouseDelta, MaxMouseDelta);
        deltaY = Math.Clamp(deltaY, -MaxMouseDelta, MaxMouseDelta);

        Orientation.X -= deltaX * MouseSensitivity;
        Orientation.Y = Math.Clamp(
            Orientation.Y - deltaY * MouseSensitivity,
            -MaxPitch,
            MaxPitch);

        Orientation.X = MathF.IEEERemainder(Orientation.X, MathF.PI * 2f);
    }

    public override string ToString() =>
        $"Position: {Position}       Orientation: {Orientation}";
}
