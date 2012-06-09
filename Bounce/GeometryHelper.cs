
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;

public class GeometryHelper
{
    public static BoundingBox transformBoundingBox(BoundingBox boundingBox, Matrix m)
    {
        var xa = m.Right * boundingBox.Min.X;
        var xb = m.Right * boundingBox.Max.X;

        var ya = m.Up * boundingBox.Min.Y;
        var yb = m.Up * boundingBox.Max.Y;

        var za = m.Backward * boundingBox.Min.Z;
        var zb = m.Backward * boundingBox.Max.Z;

        return new BoundingBox(
            Vector3.Min(xa, xb) + Vector3.Min(ya, yb) + Vector3.Min(za, zb) + m.Translation,
            Vector3.Max(xa, xb) + Vector3.Max(ya, yb) + Vector3.Max(za, zb) + m.Translation
        );
    }

    public static IEnumerable<Vector3> splitFrustum(float near, float far, Matrix m)
    {
        var clipCorners = new BoundingBox(new Vector3(-1, -1, near), new Vector3(1, 1, far)).GetCorners();
        return clipCorners.Select(v =>
        {
            var vt = Vector4.Transform(v, m);
            vt /= vt.W;

            return new Vector3(vt.X, vt.Y, vt.Z);
        });
    }

    public static IEnumerable<float> practicalSplitScheme(int numSplits, float n, float f)
    {
        for (int i = 0; i < numSplits; ++i)
        {
            float p = ((float)i) / numSplits;
            float c_log = n * (float)System.Math.Pow(f / n, p);
            float c_lin = n + (f - n) * p;

            yield return 0.5f * (c_log + c_lin);
        }

        yield return f;
    }

    public static float[] determineShadowMinMax1D(IEnumerable<float> values, float cam, float desiredSize)
    {
        var min = values.Min();
        var max = values.Max();

        if (cam > max)
        {
            return new[] { max - desiredSize, max };
        }
        else if (cam < min)
        {
            return new[] { min, min + desiredSize };
        }
        else
        {
            var currentSize = max - min;
            var l = (cam - min) / currentSize * desiredSize;
            var r = (max - cam) / currentSize * desiredSize;

            return new[]
                {
                    cam - l,
                    cam + r
                };
        }
    }

    public static IEnumerable<VertexPositionColorTexture> fullscreenQuad(Color color, float depth)
    {
        var clipCorners = new[]
        {
            new Vector2(-1, -1),
            new Vector2(-1,  1),
            new Vector2( 1, -1),
            new Vector2( 1,  1)
        };
        
        return clipCorners.Select(p =>
        {
            var pos = new Vector3(p.X, p.Y, depth);
            var texCoord = new Vector2((p.X + 1) / 2.0f, (-p.Y + 1) / 2.0f);

            return new VertexPositionColorTexture(pos, color, texCoord);
        });
    }

    public static IEnumerable<Vector2> poissonKernel()
    {
        return new[]
        {
            new Vector2(-0.326212f, -0.405810f),
            new Vector2(-0.840144f, -0.073580f),
            new Vector2(-0.695914f,  0.457137f),
            new Vector2(-0.203345f,  0.620716f),
            new Vector2( 0.962340f, -0.194983f), 
            new Vector2( 0.473434f, -0.480026f),
            new Vector2( 0.519456f,  0.767022f), 
            new Vector2( 0.185461f, -0.893124f),
            new Vector2( 0.507431f,  0.064425f), 
            new Vector2( 0.896420f,  0.412458f),
            new Vector2(-0.321940f, -0.932615f),
            new Vector2(-0.791559f, -0.597710f)
        };
    }

    public static IEnumerable<Vector3> cubeTriangleList(Vector3[] cubeCorners)
    {
        return new[]
        {
            // face 1
            cubeCorners[6], cubeCorners[2], cubeCorners[1],
            cubeCorners[1], cubeCorners[5], cubeCorners[6],
 
            // face 2
            cubeCorners[3], cubeCorners[2], cubeCorners[6],
            cubeCorners[6], cubeCorners[7], cubeCorners[3],

            // face 3
            cubeCorners[0], cubeCorners[3], cubeCorners[7],
            cubeCorners[7], cubeCorners[4], cubeCorners[0],

            // face 4
            cubeCorners[5], cubeCorners[1], cubeCorners[0],
            cubeCorners[0], cubeCorners[4], cubeCorners[5],

            // face 5
            cubeCorners[6], cubeCorners[5], cubeCorners[4],
            cubeCorners[4], cubeCorners[7], cubeCorners[6],

            // face 6
            cubeCorners[0], cubeCorners[1], cubeCorners[2],
            cubeCorners[2], cubeCorners[3], cubeCorners[0], 
        };
    }

    public static IEnumerable<Vector4> normalizedGridVertices(int width, int height)
    {
        float border = 0.0251f;

        // figure out shading values
        var lightDirection = Vector3.Normalize(new Vector3(0.2f, 1, 0.25f));
        var faceNormals = new[]
            {
                Vector3.Right,
                Vector3.Backward,
                Vector3.Left,
                Vector3.Forward,
                Vector3.Up
            };

        var colors = faceNormals.Select(n => Vector3.Dot(n, lightDirection)*0.25f + 0.75f).ToArray();
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                var min = new Vector3((x + border) / width, (y + border) / height, 0.0f);
                var max = new Vector3((x + 1 - border) / width, (y + 1 - border) / height, 1.0f);

                var axisAlignedCube = new BoundingBox(min, max);
                var triangles = cubeTriangleList(axisAlignedCube.GetCorners()).Take(30).ToArray();

                for (int i = 0; i < triangles.Length; ++i)
                {
                    var c = colors[i / 6];
                    yield return new Vector4(triangles[i].X, triangles[i].Y, triangles[i].Z, c);
                }
            }
        }
    }
}