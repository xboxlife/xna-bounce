
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
        var clipCorners = new[]
            {
                new Vector3( -1, 1, near ),
                new Vector3( 1, 1, near ), 
                new Vector3( 1, -1, near ),
                new Vector3( -1, -1, near ), 
                new Vector3( -1, 1, far ),
                new Vector3( 1, 1, far ),
                new Vector3( 1, -1, far ),
                new Vector3( -1, -1, far )
            };

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
}