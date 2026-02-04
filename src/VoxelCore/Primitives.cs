using Godot;

namespace VoxelCore;

public static class Primitives
{
    public static void Box(VoxelGrid grid, Vector3I min, Vector3I maxExclusive, byte material)
    {
        grid.FillBox(min, maxExclusive, material);
    }

    // TODO: Cylinder, Sphere, Ellipsoid, TaperedCylinder, ExtrudeInset
}
