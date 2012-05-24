// ========================================================================================== //
//                      ____                             _                                    //
//                     |  _ \                           | |                                   //
//                     | |_) | ___  _   _ _ __   ___ ___| |                                   //
//                     |  _ < / _ \| | | | '_ \ / __/ _ \ |                                   //
//                     | |_) | (_) | |_| | | | | (_|  __/_|                                   //
//                     |____/ \___/ \__,_|_| |_|\___\___(_)                                   //
//                                                                                            //
// ========================================================================================== //
// Developed and implemented by:                                                              //
//     Theodor Mader                                                                          //
//                                                                                            //
// Part of Portfolio projects, www.theomader.com                                              //
//                                                                                            //
// Copyright 2012. All rights reserved.                                                       //
// ========================================================================================== //

//#define RENDER_LIGHT_VIEW

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using System.Linq;

using CollidableModel;

namespace Bounce
{
    /// <summary>
    /// A simple renderer. Contains methods for rendering standard XNA models, as well as
    /// instanced rendering (see class InstancedModel), and rendering of selected triangles. 
    /// The scene light position can be set with the property lightPosition.
    /// </summary>
    public class Renderer
    {
        private Vector3 mLightPosition;
        public Vector3 lightPosition 
        {
            get { return mLightPosition; }
        }

        private GraphicsDevice mGraphicsDevice;

        DynamicVertexBuffer mInstanceDataStream; // secondary vertex buffer used for hardware instancing
        VertexDeclaration mInstanceVertexDeclaration;

        VertexBuffer mCollidingFacesVertices;    // Vertex buffer for colliding faces
        VertexDeclaration mCollidingFacesVertexDeclaration;

        VertexBuffer mDebugVertices;            // Vertex buffer for drawing debug primitives

        Effect mCollisionsShader;   // shader used for drawing colliding triangles
        Effect mDebugShader;        // used for rendering geometric primitives for debugging

        BlendState mAlphaBlendState;

        RenderTarget2D mShadowMap;
        RenderTarget2D mBlurTarget;

        Matrix mShadowView;
        Matrix mShadowProjection;

        public enum RenderPass { ShadowPass, ColourPass };
        RenderPass mCurrentPass = RenderPass.ColourPass;

        const int mNumShadowSplits = 4;
        public bool mSnapShadowMaps = true;

        public Matrix ShadowView
        {
            get { return mShadowView; }
        }

        public Matrix ShadowProjection
        {
            get { return mShadowProjection; }
        }

        public Matrix[] ShadowSplitProjections;
        public Matrix[] ShadowSplitProjectionsWithTiling;
        public Vector4[] ShadowSplitTileBounds;

        public Vector3[][] ViewFrustumSplits;
        public Color ViewFrustumColor = new Color(0, 255, 255, 32);
        public Color[] ShadowSplitColors = new[] 
        { 
            new Color(255, 0, 0, 92),
            new Color(0, 255, 0, 92),
            new Color(0, 0, 255, 92),
            new Color(255, 255, 0, 92)
        };

        public enum ShadowMapOverlayMode
        {
            None,
            ShadowFrustums,
            ShadowMap,
            ShadowMapAndViewFrustum
            
        };

        float[,] ShadowDepthBias = 
        {
            { 2.5f, 0.0009f },
            { 2.5f, 0.0009f },
            { 2.5f, 0.0009f },
            { 2.5f, 0.001f }
        };


        public Vector2[] poissonKernel
        {
            get
            {
               return GeometryHelper.poissonKernel()
                   .Select(v => v / 512.0f)
                   .OrderBy(v => v.Length())
                   .ToArray();
           }
        }

        Texture3D mRandomTexture3D;
        public Texture3D randomTexture3D
        {
            get { return mRandomTexture3D; }
        }

        Texture2D mRandomTexture2D;
        public Texture2D randomTexture2D
        {
            get { return mRandomTexture2D; }
        }

        public Renderer() 
        {
        }

        public void setLightPosition(Vector3 lightPosition, Arena arena)
        {
            mLightPosition = lightPosition;

            var look = Vector3.Normalize(arena.BoundingSphere.Center - mLightPosition);
            var up = Vector3.Cross(look, Vector3.Right);

            // Remember: XNA uses a right handed coordinate system, i.e. -Z goes into the screen
            mShadowView = Matrix.Invert(
                new Matrix(
                    1,              0,              0,              0,
                    0,              0,              -1,              0, 
                    -look.X,        -look.Y,        -look.Z,        0,
                    mLightPosition.X, mLightPosition.Y, mLightPosition.Z, 1
                )
            ); 

            // bounding box
            {
                var bb = GeometryHelper.transformBoundingBox(arena.CollisionData.geometry.boundingBox, mShadowView);
                mShadowProjection = Matrix.CreateOrthographicOffCenter(bb.Min.X, bb.Max.X, bb.Min.Y, bb.Max.Y, -bb.Max.Z, -bb.Min.Z);
            }
        }

        public void setShadowTransforms(Camera camera, Arena arena)
        {
            Matrix viewProj = camera.viewMatrix * camera.projectionMatrix;
            Matrix viewProjInverse = Matrix.Invert(viewProj);
            Matrix projInverse = Matrix.Invert(camera.projectionMatrix);
            Matrix viewInverse = Matrix.Invert(camera.viewMatrix);

            // figure out closest geometry empassing near and far plances based on arena bounding box
            var viewSpaceBB = GeometryHelper.transformBoundingBox(arena.CollisionData.geometry.boundingBox, camera.viewMatrix);
            var viewSpaceMin = Math.Min(-1, viewSpaceBB.Max.Z);
            var viewSpaceMax = Math.Min(0, viewSpaceBB.Min.Z);

            var viewDistance = new[]
                {
                   arena.CollisionData.geometry.boundingBox.Max.X - arena.CollisionData.geometry.boundingBox.Min.X,
                   arena.CollisionData.geometry.boundingBox.Max.Y - arena.CollisionData.geometry.boundingBox.Min.Y,
                   arena.CollisionData.geometry.boundingBox.Max.Z - arena.CollisionData.geometry.boundingBox.Min.Z,
                }.Max() - 200.0f;

            var splitPlanes = GeometryHelper.practicalSplitScheme(mNumShadowSplits, 1, viewDistance)
                .Select(v => -v)
                .ToArray();

            var splitDistances = splitPlanes.Select(c =>
                {
                    var d = Vector4.Transform(new Vector3(0, 0, c), camera.projectionMatrix);
                    return d.W != 0 ? d.Z / d.W : 0;
                }).ToArray();

            var splitData = Enumerable.Range(0, mNumShadowSplits).Select(i =>
                {
                    var n = splitDistances[i];
                    var f = splitDistances[i + 1];

                    var viewSplit = GeometryHelper.splitFrustum(n, f, viewProjInverse).ToArray();
                    var frustumCorners = viewSplit.Select(v => Vector3.Transform(v, ShadowView)).ToArray();
                    var cameraPosition = Vector3.Transform(viewInverse.Translation, ShadowView);

                    var viewMin = frustumCorners.Aggregate((v1, v2) => Vector3.Min(v1, v2));
                    var viewMax = frustumCorners.Aggregate((v1, v2) => Vector3.Max(v1, v2));

                    var arenaBB = GeometryHelper.transformBoundingBox(arena.CollisionData.geometry.boundingBox, ShadowView);

                    var minZ = -arenaBB.Max.Z;
                    var maxZ = -arenaBB.Min.Z;

                    var range = Math.Max(
                        1.0f / camera.projectionMatrix.M11 * -splitPlanes[i + 1] * 2.0f,
                        -splitPlanes[i + 1] - (-splitPlanes[i])
                    );

                    // range is slightly too small, so add in some padding
                    float padding = 5.0f;
                    var quantizationStep = (range + padding) / 512.0f;

                    var x = GeometryHelper.determineShadowMinMax1D(frustumCorners.Select(v => v.X), cameraPosition.X, range);
                    var y = GeometryHelper.determineShadowMinMax1D(frustumCorners.Select(v => v.Y), cameraPosition.Y, range);

                    var projectionMin = new Vector3(x[0], y[0], minZ);
                    var projectionMax = new Vector3(x[1], y[1], maxZ);

                    // Add in padding
                    {
                        range += padding;
                        projectionMin.X -= padding / 2.0f;
                        projectionMin.Y -= padding / 2.0f;
                    }

                    // quantize
                    if (mSnapShadowMaps)
                    {
                        // compute range
                        var qx = (float)Math.IEEERemainder(projectionMin.X, quantizationStep);
                        var qy = (float)Math.IEEERemainder(projectionMin.Y, quantizationStep);

                        projectionMin.X = projectionMin.X - qx;
                        projectionMin.Y = projectionMin.Y - qy;

                        projectionMax.X = projectionMin.X + range;
                        projectionMax.Y = projectionMin.Y + range;
                    }

                    // compute offset into texture atlas
                    int tileX = i % 2;
                    int tileY = i / 2;

                    var tileTransform = Matrix.Identity;
                    tileTransform.M11 = 0.5f;
                    tileTransform.M22 = 0.5f;
                    tileTransform.Translation = new Vector3(tileX * 0.5f, tileY * 0.5f, 0);

                    // [x min, x max, y min, y max]
                    float tileBorder = 3.0f / 512.0f;
                    var tileBounds = new Vector4(
                        0.5f * tileX + tileBorder,
                        0.5f * tileX + 0.5f - tileBorder,
                        0.5f * tileY + tileBorder,
                        0.5f * tileY + 0.5f - tileBorder
                    );

                    var textureMatrix = Matrix.Identity;
                    textureMatrix.M11 = 0.5f;
                    textureMatrix.M22 = -0.5f;
                    textureMatrix.Translation = new Vector3(0.5f, 0.5f, 0.0f);

                    return new
                    {
                        Distance = f,
                        ViewFrustum = viewSplit,
                        Projection = Matrix.CreateOrthographicOffCenter(projectionMin.X, projectionMax.X, projectionMin.Y, projectionMax.Y, projectionMin.Z, projectionMax.Z),
                        TileTransform = textureMatrix * tileTransform,
                        TileBounds = tileBounds,
                    };
                }).ToArray();

            ViewFrustumSplits = splitData.Select(s => s.ViewFrustum).ToArray();
            ShadowSplitProjections = splitData.Select(s => ShadowView * s.Projection).ToArray();
            ShadowSplitProjectionsWithTiling = splitData.Select(s => ShadowView * s.Projection * s.TileTransform).ToArray();
            ShadowSplitTileBounds = splitData.Select(s => s.TileBounds).ToArray();
        }

        public void  loadContent(ContentManager contentManager, GraphicsDeviceManager graphicsManager, ModelGeometry collisionGeometry)
        {
            mGraphicsDevice = graphicsManager.GraphicsDevice;

            mInstanceVertexDeclaration = new VertexDeclaration(new[]
            {
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
                new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
                new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5)
            });

            mCollidingFacesVertexDeclaration = new VertexDeclaration(new[]
            {
                // ideally we'd use Byte4 for Color0 but it's so much easier to fill vertex buffers with Vector3. There's only very little data anyway
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0 ),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0 ),
                new VertexElement(24, VertexElementFormat.Vector3, VertexElementUsage.Color, 0 ) 
            });

            mCollidingFacesVertices = new VertexBuffer(mGraphicsDevice, mCollidingFacesVertexDeclaration, collisionGeometry.faces.Length * 3, BufferUsage.WriteOnly);
            mDebugVertices = new VertexBuffer(mGraphicsDevice, VertexPositionColorTexture.VertexDeclaration, 1024, BufferUsage.WriteOnly);

            // load collisions shader and configure material properties
            mCollisionsShader = contentManager.Load<Effect>("Effects/CollisionsShader");
            mCollisionsShader.Parameters["MaterialColor"].SetValue(new Vector3(0.39f, 0.8f, 1f));

            mDebugShader = contentManager.Load<Effect>("Effects/DebugShader");

            mAlphaBlendState = new BlendState();
            mAlphaBlendState.ColorSourceBlend = Blend.SourceAlpha;
            mAlphaBlendState.AlphaSourceBlend = Blend.SourceAlpha;
            mAlphaBlendState.ColorDestinationBlend = Blend.InverseSourceAlpha;
            mAlphaBlendState.AlphaDestinationBlend = Blend.InverseSourceAlpha;

            mShadowMap = new RenderTarget2D(mGraphicsDevice, 1024, 1024, false, SurfaceFormat.Single, DepthFormat.Depth24);
            mRandomTexture3D = new Texture3D(mGraphicsDevice, 32, 32, 32, false, SurfaceFormat.Rg32);
            mRandomTexture2D = new Texture2D(mGraphicsDevice, 128, 128, false, SurfaceFormat.Rg32);

            Random random = new Random();

            Func<int, IEnumerable<UInt16> > randomRotations = (count) =>
                {
                    return Enumerable
                       .Range(0,count)
                        .Select(i => (float)(random.NextDouble() * Math.PI * 2))
                        .SelectMany(r => new[]{ Math.Cos(r), Math.Sin(r) })
                        .Select( v => (UInt16)((v*0.5+0.5) * UInt16.MaxValue));
                };

            mRandomTexture3D.SetData(randomRotations(mRandomTexture3D.Width * mRandomTexture3D.Height * mRandomTexture3D.Depth).ToArray());
            mRandomTexture2D.SetData(randomRotations(mRandomTexture2D.Width * mRandomTexture2D.Height).ToArray());
         }

        public void SetCurrentPass(RenderPass pass)
        {
            if (pass != mCurrentPass)
            {
                switch (pass)
                {
                    case RenderPass.ShadowPass:
                        mGraphicsDevice.SetRenderTarget(mShadowMap);
                        mGraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1.0f, 0);
 
                        break;

                    case RenderPass.ColourPass:
#if VSM
                        mGraphicsDevice.SetRenderTarget(mBlurTarget);
                        
                        mGraphicsDevice.DepthStencilState = DepthStencilState.None;
                        mShadowMapShader.CurrentTechnique = mShadowMapShader.Techniques["Blur"];
                        mShadowMapShader.Parameters["BlurStep"].SetValue(new Vector2(1.0f / mShadowMap.Width, 0.0f));
                        mShadowMapShader.Parameters["ShadowMap"].SetValue(mShadowMap);
                        mShadowMapShader.Techniques["Blur"].Passes[0].Apply();
                        renderFullscreenQuad();
                       

                        mGraphicsDevice.SetRenderTarget(mShadowMap);
                        mShadowMapShader.Parameters["BlurStep"].SetValue(new Vector2(0.0f, 1.0f / mShadowMap.Height));
                        mShadowMapShader.Parameters["ShadowMap"].SetValue(mBlurTarget);
                        mShadowMapShader.Techniques["Blur"].Passes[0].Apply();
                        renderFullscreenQuad();
#endif
                        mGraphicsDevice.SetRenderTarget(null);
                        mGraphicsDevice.DepthStencilState = DepthStencilState.Default;
                        mGraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);

                        break;

                }
                mCurrentPass = pass;
            }
        }


        public void render(Model model, Camera camera)
        {
            render(model, Matrix.Identity, Matrix.Identity, camera);
        }

        public void render(Model model, Matrix rotation, Matrix translation, Camera camera)
        {
            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);

            // Draw the model. A model can have multiple meshes, so loop.
            foreach (ModelMesh mesh in model.Meshes)
            {
                // This is where the mesh orientation is set, as well as our camera and projection.
                var world = rotation * transforms[mesh.ParentBone.Index] * translation;

                foreach( var shadowedEffect in mesh.Effects.Where( e => e.Parameters.Any(p => p.Name == "ShadowMap")))
                {
                    shadowedEffect.Parameters["ShadowMap"].SetValue(mShadowMap);
                    shadowedEffect.Parameters["ShadowTransform"].SetValue(ShadowSplitProjectionsWithTiling);
                    shadowedEffect.Parameters["TileBounds"].SetValue(ShadowSplitTileBounds);
                    shadowedEffect.Parameters["SplitColors"].SetValue(ShadowSplitColors.Select(c=>c.ToVector4()).ToArray());
                }

                foreach (Effect effect in mesh.Effects)
                {
                    effect.Parameters["World"].SetValue(world);
                    effect.Parameters["View"].SetValue(camera.viewMatrix);
                    effect.Parameters["Projection"].SetValue(camera.projectionMatrix);

                    if(effect.Parameters["LightPosition"] != null)
                        effect.Parameters["LightPosition"].SetValue(mLightPosition);
                    if (effect.Parameters["LightDirection"] != null)
                        effect.Parameters["LightDirection"].SetValue(Vector3.Normalize(mLightPosition));
                }

                // Draw the mesh, using the effects set above.
                mesh.Draw();
            }
        }

        public void renderShadow(Model arena, Model sphere, int numInstances, Camera camera)
        {

            Matrix[] transforms = new Matrix[arena.Bones.Count];
            arena.CopyAbsoluteBoneTransformsTo(transforms);

            mGraphicsDevice.SetRenderTarget(mShadowMap);

            for (int i = 0; i < mNumShadowSplits; ++i)
            {
                {
                    int x = i % 2;
                    int y = i / 2;
                    var viewPort = new Viewport(x * 512, y * 512, 512, 512);

                    mGraphicsDevice.Viewport = viewPort;
                }   

                // Draw the arena model first.
                foreach (ModelMesh mesh in arena.Meshes)
                {
                    foreach (var effect in mesh.Effects)
                    {
                        effect.Parameters["ViewProjection"].SetValue(ShadowSplitProjections[i]);
                        effect.Parameters["World"].SetValue(transforms[mesh.ParentBone.Index]);
                        effect.Parameters["DepthBias"].SetValue(new Vector2(ShadowDepthBias[i, 0], ShadowDepthBias[i, 1]));
                    }

                    mesh.Draw();
                }

                // now render the spheres
                if (numInstances>0)
                {
                    foreach (ModelMesh mesh in sphere.Meshes)
                    {
                        foreach (var part in mesh.MeshParts)
                        {
                            part.Effect.CurrentTechnique = part.Effect.Techniques["ShadowInstanced"];
                            part.Effect.Parameters["ViewProjection"].SetValue(ShadowSplitProjections[i]);
                            part.Effect.Parameters["World"].SetValue(transforms[mesh.ParentBone.Index]);
                            part.Effect.Parameters["DepthBias"].SetValue(new Vector2(ShadowDepthBias[i, 0], ShadowDepthBias[i, 1]));
                            part.Effect.CurrentTechnique.Passes[0].Apply();
                           
                            // set vertex buffer
                            mGraphicsDevice.SetVertexBuffers(new[] 
                            {
                                part.VertexBuffer,
                                new VertexBufferBinding(mInstanceDataStream, 0, 1 )
                            });

                            // set index buffer and draw
                            mGraphicsDevice.Indices = part.IndexBuffer;
                            mGraphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, part.VertexOffset, 0, part.NumVertices, part.StartIndex, part.PrimitiveCount, numInstances);
                        }
                    }
                }   
            }
        }

        public void setInstancingData(Matrix[] model2worldTransformations, int numInstances)
        {
            if (numInstances > 0)
            {
                // Make sure our instance data vertex buffer is big enough. (4x4 float matrix)
                int instanceDataSize = 16 * sizeof(float) * numInstances;

                if ((mInstanceDataStream == null) ||
                    (mInstanceDataStream.VertexCount < numInstances))
                {
                    if (mInstanceDataStream != null)
                        mInstanceDataStream.Dispose();

                    mInstanceDataStream = new DynamicVertexBuffer(mGraphicsDevice, mInstanceVertexDeclaration, numInstances, BufferUsage.WriteOnly);
                }

                // Upload transform matrices to the instance data vertex buffer.
                mInstanceDataStream.SetData(model2worldTransformations, 0, numInstances, SetDataOptions.Discard);
            }
        }

        /// <summary>
        /// Instanced rendering. Draws instancedModel numInstances times. This demo uses hardware
        /// instancing: a secondary vertex stream is created, where the transform matrices of the
        /// individual instances are passed down to the shader. Note that in order to be efficient,
        /// the model should contain as little meshes and meshparts as possible.
        /// </summary>
        /// <param name="instancedModel">The model to be drawn</param>
        /// <param name="camera">The camera</param>
        /// <param name="numInstances">Number of instances to draw.</param>
        public virtual void renderInstanced(Model model, Camera camera, int numInstances)
        {
            if (numInstances > 0)
            {
                // Draw the model. A model can have multiple meshes, so loop.
                Matrix[] transforms = new Matrix[model.Bones.Count];
                model.CopyAbsoluteBoneTransformsTo(transforms);

                // loop through meshes
                foreach (ModelMesh mesh in model.Meshes)
                {
                    foreach (var shadowedEffect in mesh.Effects.Where(e => e.Parameters.Any(p => p.Name == "ShadowMap")))
                    {
                        shadowedEffect.Parameters["ShadowMap"].SetValue(mShadowMap);
                        shadowedEffect.Parameters["ShadowTransform"].SetValue(ShadowSplitProjectionsWithTiling);
                        shadowedEffect.Parameters["TileBounds"].SetValue(ShadowSplitTileBounds);
                    }

                    // get bone matrix
                    Matrix boneMatrix = transforms[mesh.ParentBone.Index];
                    foreach (ModelMeshPart part in mesh.MeshParts)
                    {
                        part.Effect.Parameters["World"].SetValue(boneMatrix);
                        part.Effect.Parameters["View"].SetValue(camera.viewMatrix);
                        part.Effect.Parameters["Projection"].SetValue(camera.projectionMatrix);
                        part.Effect.Parameters["LightDirection"].SetValue(Vector3.Normalize(mLightPosition));
                        part.Effect.CurrentTechnique.Passes[0].Apply();

                        // set vertex buffer
                        mGraphicsDevice.SetVertexBuffers(new[] 
                        {
                            part.VertexBuffer,
                            new VertexBufferBinding(mInstanceDataStream, 0, 1 )
                        });

                        // draw primitives 
                        mGraphicsDevice.Indices = part.IndexBuffer;
                        mGraphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, part.VertexOffset, 0, part.NumVertices, part.StartIndex, part.PrimitiveCount, numInstances);
                    }
                }
            }
        }

        public void fillShadowMap()
        { 
            var data = new float[mShadowMap.Width*mShadowMap.Height];
            for (int y = 0; y < mShadowMap.Height; ++y)
            {
                for (int x = 0; x < mShadowMap.Width; ++x)
                {
                    data[x+y*mShadowMap.Width] = (x+y) % 2;
                }
            }

            mShadowMap.SetData<float>(data);
        }


        public void renderCollisionGeometry(ModelGeometry collisionGeometry, Camera camera)
        {
            foreach (var mesh in collisionGeometry.visualizationData.Meshes)
            {
                foreach (var effect in mesh.Effects.Cast<BasicEffect>())
                {
                    effect.View = camera.viewMatrix;
                    effect.Projection = camera.projectionMatrix; 
                    effect.LightingEnabled = true;
                }

                mesh.Draw();
            }
        }

        public void renderCube( Vector3[] corners, Color color, Matrix cameraMatrix )
        {
            mDebugShader.CurrentTechnique = mDebugShader.Techniques["Color"];
            mDebugShader.Parameters["WorldViewProjection"].SetValue(cameraMatrix);
            mDebugShader.CurrentTechnique.Passes[0].Apply();

            renderCubePrimitives(corners, color);
        }

        public void renderCubePrimitives(Vector3[] corners, Color color)
        {
            var triangles = new[]
            {
                // front
                corners[0], corners[1], corners[2],
                corners[2], corners[3], corners[0],

                // right
                corners[6], corners[2], corners[1],
                corners[1], corners[5], corners[6],
 
                 // bottom
                corners[3], corners[2], corners[6],
                corners[6], corners[7], corners[3],

                 // back
                corners[6], corners[5], corners[4],
                corners[4], corners[7], corners[6],

                 // left
                 corners[0], corners[3], corners[7],
                corners[7], corners[4], corners[0],

                // top
                corners[5], corners[1], corners[0],
                corners[0], corners[4], corners[5],
             };

            // fill vertex buffer
            var data = triangles.Select(v => new VertexPositionColorTexture(v, color, Vector2.Zero));
            mDebugVertices.SetData<VertexPositionColorTexture>(data.ToArray());
            mGraphicsDevice.SetVertexBuffer(mDebugVertices);

            // draw
            mGraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 12);

            // cleanup
            mGraphicsDevice.SetVertexBuffer(null);
        }

        public void renderFrustum(Matrix frustumMatrix, Color color, Camera camera, float yOffset = 0)
        {
            renderFrustum(frustumMatrix, color, camera.viewMatrix * camera.projectionMatrix, yOffset);
        }

        public void renderFrustum(Matrix frustumMatrix, Color color, Matrix cameraMatrix, float yOffset)
        {
            // determine frustum corners
            var boundingFrustum = new BoundingFrustum(frustumMatrix);
            renderFrustum(boundingFrustum.GetCorners(), color, cameraMatrix, yOffset);
        }

        public void renderFrustum(Vector3[] frustumCorners, Color color, Matrix cameraMatrix, float yOffset)
        {
            // apply y-offset to frustum corners
            var corners = frustumCorners
                .Select(c => c + Vector3.UnitY * yOffset)
                .ToArray();

            // store render state
            var oldRasterizerState = mGraphicsDevice.RasterizerState;
            var oldBlendstate = mGraphicsDevice.BlendState;

            // set up shader
            mDebugShader.CurrentTechnique = mDebugShader.Techniques["Color"];
            mDebugShader.Parameters["WorldViewProjection"].SetValue(cameraMatrix);
            mDebugShader.CurrentTechnique.Passes[0].Apply();

            // turn off back face culling
            mGraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None };

            // render alpha blended frustum faces
            {
                mGraphicsDevice.BlendState = BlendState.NonPremultiplied;
                renderCubePrimitives(corners, color);
            }

            // render opaque frustum edges
            {
                mGraphicsDevice.BlendState = BlendState.Opaque;
                mGraphicsDevice.RasterizerState = new RasterizerState { FillMode = FillMode.WireFrame, CullMode = CullMode.None };
                renderCubePrimitives(corners, color);
            }

            // reset rasterizer state
            mGraphicsDevice.RasterizerState = oldRasterizerState;
            mGraphicsDevice.BlendState = oldBlendstate;
        }

        public void renderAxisAlignedCube( Vector3 min, Vector3 max, Color color, Matrix viewProjection )
        {
            var corners = new[]
            {
                new Vector3(min.X, max.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z),
                new Vector3(max.X, min.Y, max.Z),
                new Vector3(min.X, min.Y, max.Z),

                new Vector3(min.X, max.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z),
                new Vector3(max.X, min.Y, min.Z),
                new Vector3(min.X, min.Y, min.Z),
            };

            renderCube(corners, color, viewProjection);
        }

        public void renderShadowMapOverlay(Camera camera, Model arenaShadowModel, ShadowMapOverlayMode overlayMode)
        {
            if (overlayMode == ShadowMapOverlayMode.None)
                return;

            var OverlaySize = 0.9f;
            var Offset = (1.0f - OverlaySize) / 2.0f;

            // overlay rectangle in screen space
            var overlayRectangle = new[] 
            { 
               Offset* mGraphicsDevice.Viewport.Width,
               Offset * mGraphicsDevice.Viewport.Height,
               OverlaySize * mGraphicsDevice.Viewport.Width,
               OverlaySize * mGraphicsDevice.Viewport.Height
            }.Select(p => (int)Math.Floor(p)).ToArray();

            // viewport for main overlay and shadow splits
            var overlayViewport = new Viewport(overlayRectangle[0], overlayRectangle[1], overlayRectangle[2], overlayRectangle[3]);
            var splitViewports = Enumerable.Range(0, mNumShadowSplits).Select( i =>
            {
                int x = i % 2;
                int y = i / 2;

                return new Viewport(
                    overlayViewport.X + x * overlayViewport.Width / 2,
                    overlayViewport.Y + y * overlayViewport.Height / 2,
                    overlayViewport.Width / 2,
                    overlayViewport.Height / 2);
            }).ToArray();
        
            // store state
            var previousViewport = mGraphicsDevice.Viewport;
            var previousRasterizerState = mGraphicsDevice.RasterizerState;
            var previousBlendState = mGraphicsDevice.BlendState;
            var previousDepthState = mGraphicsDevice.DepthStencilState;

            // clear overlay viewport first
            {
                var depthAlwaysPass = new DepthStencilState { DepthBufferFunction = CompareFunction.Always };
                mGraphicsDevice.DepthStencilState = depthAlwaysPass;

                mDebugShader.CurrentTechnique = mDebugShader.Techniques["Color"];
                mDebugShader.Parameters["WorldViewProjection"].SetValue(Matrix.Identity);
                mDebugShader.CurrentTechnique.Passes[0].Apply();

                mDebugVertices.SetData(GeometryHelper.fullscreenQuad(Color.White, 1.0f).ToArray());
                mGraphicsDevice.SetVertexBuffer(mDebugVertices);
                mGraphicsDevice.Viewport = overlayViewport;
                mGraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
                mGraphicsDevice.SetVertexBuffer(null);
            }

            // render frustums
            if (overlayMode == ShadowMapOverlayMode.ShadowFrustums)
            {
                var scaleMatrix = Matrix.Identity;
                scaleMatrix.M11 = 0.75f;
                scaleMatrix.M22 = 0.75f;

                // render arena in lightspace
                {
                    mDebugShader.CurrentTechnique = mDebugShader.Techniques["ShadowModel"];
                    mDebugShader.Parameters["WorldViewProjection"].SetValue(mShadowView * mShadowProjection * scaleMatrix);
                    mDebugShader.CurrentTechnique.Passes[0].Apply();
                    mGraphicsDevice.DepthStencilState = DepthStencilState.Default;

                    foreach (var part in arenaShadowModel.Meshes.SelectMany(m => m.MeshParts))
                    {
                        mGraphicsDevice.SetVertexBuffer(part.VertexBuffer);
                        mGraphicsDevice.Indices = part.IndexBuffer;
                        mGraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, part.VertexOffset, 0, part.NumVertices, part.StartIndex, part.PrimitiveCount);
                    }
                }

                // now render the split frustum
                {
                    // sets z coordinate to 0.5
                    var fixedDepth = Matrix.Identity;
                    fixedDepth.M33 = 0.0f;
                    fixedDepth.M43 = 0.5f;

                    mGraphicsDevice.BlendState = BlendState.NonPremultiplied;
                    mGraphicsDevice.DepthStencilState = DepthStencilState.None;

                    for (int i = 0; i < mNumShadowSplits; ++i)
                    {
                        renderFrustum(ShadowSplitProjections[i], ShadowSplitColors[i], mShadowView * mShadowProjection * scaleMatrix, 0);
                    }

                    renderFrustum(camera.viewMatrix * camera.projectionMatrix, ViewFrustumColor, mShadowView * mShadowProjection * scaleMatrix * fixedDepth, 0);
                }
            }
            else  // render shadow map as overlay
            {
                mDebugShader.CurrentTechnique = mDebugShader.Techniques["ShadowTexture"];
                mDebugShader.Parameters["WorldViewProjection"].SetValue(Matrix.Identity);
                mDebugShader.Parameters["TextureScale"].SetValue(new float[] { 0, 0.25f });
                mDebugShader.Parameters["DebugTexture"].SetValue(mShadowMap);
                mDebugShader.CurrentTechnique.Passes[0].Apply();

                mDebugVertices.SetData(GeometryHelper.fullscreenQuad(Color.White, 0.5f).ToArray());
                mGraphicsDevice.SetVertexBuffer(mDebugVertices);
                mGraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
                mGraphicsDevice.SetVertexBuffer(null);
            }

            // render viewer camera frustum on top of shadow map overlay
            if (overlayMode == ShadowMapOverlayMode.ShadowMapAndViewFrustum)
            {
                // for each shadow projection
                for (int i = 0; i < mNumShadowSplits; ++i)
                {
                    // sets z coordinate to 0.5
                    var fixedDepth = Matrix.Identity;
                    fixedDepth.M33 = 0.0f;
                    fixedDepth.M43 = 0.5f;

                    mGraphicsDevice.Viewport = splitViewports[i];
                    renderFrustum(camera.viewMatrix * camera.projectionMatrix, ShadowSplitColors[i], ShadowSplitProjections[i] * fixedDepth, 0);
                }
            }

            mGraphicsDevice.Viewport = previousViewport;
            mGraphicsDevice.RasterizerState = previousRasterizerState;
            mGraphicsDevice.DepthStencilState = previousDepthState;
            mGraphicsDevice.BlendState = previousBlendState;
        }

        public void renderCollidingFaces(ModelGeometry collisionGeometry, IDictionary<Face, uint> collidingFaces, Camera camera, uint min, uint max)
        {
            if (collidingFaces.Count > 0)
            {
                // fill vertex buffer with collision geometry and set on device
                {
                    float range = Math.Max(max - min, 0.0001f);
                    var data = collidingFaces.SelectMany(face =>
                        {
                            var colour = new Vector3((face.Value - min)/range);
                            return new[]
                            {
                                collisionGeometry.vertices[face.Key.v1],
                                collisionGeometry.normals[face.Key.n1],
                                colour,
                                
                                collisionGeometry.vertices[face.Key.v2],
                                collisionGeometry.normals[face.Key.n2],
                                colour,
                                
                                collisionGeometry.vertices[face.Key.v3],
                                collisionGeometry.normals[face.Key.n3],
                                colour
                            };
                        });

                    mCollidingFacesVertices.SetData<Vector3>(data.ToArray());
                    mGraphicsDevice.SetVertexBuffer(mCollidingFacesVertices);
                }

                // enable alpha blending
                var previousBlendState = mGraphicsDevice.BlendState;
                mGraphicsDevice.BlendState = mAlphaBlendState;

                // draw
                mCollisionsShader.Parameters["WorldViewProjection"].SetValue(camera.viewMatrix * camera.projectionMatrix);
                mCollisionsShader.CurrentTechnique.Passes[0].Apply();
                mGraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, collidingFaces.Count);

                // restore previous blend mode
                mGraphicsDevice.BlendState = previousBlendState;
            } 
        }
    }
}
