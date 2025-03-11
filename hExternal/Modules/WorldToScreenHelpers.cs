using hExternal.Modules;
using System.Numerics;

internal static class WorldToScreenHelpers
{
    public static Vector2 WorldToScreen(ViewMatrix matrix, Vector3 pos, int width, int height)
    {
        Vector2 screenCoordinates = new Vector2();

        // Screen width
        float screenW = (matrix.m41 * pos.X) + (matrix.m42 * pos.Y) + (matrix.m43 * pos.Z) + matrix.m44;

        if (screenW > 0.001f)
        {
            // Screen x and y values
            float screenX = (matrix.m11 * pos.X) + (matrix.m12 * pos.Y) + (matrix.m13 * pos.Z) + matrix.m14;
            float screenY = (matrix.m21 * pos.X) + (matrix.m22 * pos.Y) + (matrix.m23 * pos.Z) + matrix.m24;

            // Center camera
            float camX = width / 2;
            float camY = height / 2;

            // Handle perspective division
            float X = camX + (camX * screenX / screenW);
            float Y = camY - (camY * screenY / screenW);

            // Return coordinates
            screenCoordinates.X = X;
            screenCoordinates.Y = Y;
            return screenCoordinates;
        }
        else
        {
            // Out of range
            return new Vector2(-99, -99);
        }
    }
}