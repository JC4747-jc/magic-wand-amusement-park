using UnityEngine;

// PXR_CameraImage.GetCameraExtrinsics already reflects the native head pose in Z.
// Reflect optical (u-right, v-down, Z-positive) points in Z exactly once too.
public static class StereoFusionGeometry
{
    public static Vector3 OpticalToHead(Vector3 optical, Pose sdkCameraPose)
        => sdkCameraPose.position + sdkCameraPose.rotation * new Vector3(optical.x, optical.y, -optical.z);

    public static Vector3 HeadToOptical(Vector3 headPoint, Pose sdkCameraPose)
    {
        Vector3 p = Quaternion.Inverse(sdkCameraPose.rotation) * (headPoint - sdkCameraPose.position);
        return new Vector3(p.x, p.y, -p.z);
    }

    public static bool ProjectAim(float opticalZ, Pose cameraPose, Vector4 k,
        out Vector2 pixel, out Vector3 headPoint)
    {
        pixel = default;
        headPoint = default;
        Vector3 origin = HeadToOptical(Vector3.zero, cameraPose);
        Vector3 direction = HeadToOptical(Vector3.forward, cameraPose) - origin;
        if (!Finite(opticalZ) || opticalZ <= 0 || direction.z <= 0.01f) return false;
        float distance = (opticalZ - origin.z) / direction.z;
        if (!Finite(distance) || distance < 0.15f || distance > 10f) return false;
        Vector3 optical = origin + direction * distance;
        pixel = new Vector2(k.z + k.x * optical.x / optical.z, k.w + k.y * optical.y / optical.z);
        headPoint = Vector3.forward * distance;
        return Finite(pixel.x) && Finite(pixel.y);
    }

    public static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);

    public static bool TryTabletopPosition(Vector3 center, Vector3 measuredBottom, Vector3 registeredBottom,
        float registeredCenterY, out Vector3 position)
    {
        position = center;
        if (!Finite(center.x) || !Finite(center.y) || !Finite(center.z) || !Finite(measuredBottom.y) ||
            !Finite(registeredBottom.y) || !Finite(registeredCenterY) || Mathf.Abs(measuredBottom.y - registeredBottom.y) > .025f) return false;
        position.y = registeredCenterY;
        return true;
    }

    public static Quaternion UprightFacing(Vector3 position, Vector3 viewer, Quaternion fallback)
    {
        Vector3 forward = viewer - position; forward.y = 0;
        return forward.sqrMagnitude > .0025f ? Quaternion.LookRotation(forward, Vector3.up) : fallback;
    }

    // Intersect bbox top/bottom rays with a vertical plane through the measured
    // center. Unlike equal-optical-Z unprojection, this handles looking down at a table.
    public static bool TryUprightExtent(Vector3 center, Vector3 cameraOrigin,
        Vector3 topRay, Vector3 bottomRay, out Vector3 bottom, out float height)
    {
        bottom = default; height = 0;
        Vector3 normal = center - cameraOrigin; normal.y = 0;
        if (normal.sqrMagnitude < .0025f) return false;
        normal.Normalize();
        float planeDistance = Vector3.Dot(center - cameraOrigin, normal);
        float topDenom = Vector3.Dot(topRay, normal), bottomDenom = Vector3.Dot(bottomRay, normal);
        if (topDenom <= .05f || bottomDenom <= .05f) return false;
        Vector3 topHit = cameraOrigin + topRay * (planeDistance / topDenom);
        Vector3 bottomHit = cameraOrigin + bottomRay * (planeDistance / bottomDenom);
        height = topHit.y - bottomHit.y;
        if (!Finite(height) || height < .025f || height > .25f) return false;
        bottom = new Vector3(center.x, bottomHit.y, center.z);
        return Finite(bottom.x) && Finite(bottom.y) && Finite(bottom.z);
    }
}
