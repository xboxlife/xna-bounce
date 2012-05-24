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
// Copyright 2011. All rights reserved.                                                       //
// ========================================================================================== //


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
            set { mLightPosition = value; }
        }

        private GraphicsDevice mGraphicsDevice;


        DynamicVertexBuffer mInstanceDataStream; // secondary vertex buffer used for hardware instancing
        VertexDeclaration mInstanceVertexDeclaration;

        VertexBuffer mCollidingFacesVertices;    // Vertex buffer for colliding faces
        VertexDeclaration mCollidingFacesVertexDeclaration;

        Effect mCollisionsShader;   // shader used for drawing colliding triangles

        BlendState mAlphaBlendState;

        public Renderer() {
        }

        public void loadContent(ContentManager contentManager, GraphicsDeviceManager graphicsManager, ModelGeometry collisionGeometry)
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

            // load collisions shader and configure material properties
            mCollisionsShader = contentManager.Load<Effect>("Effects/CollisionsShader");
            mCollisionsShader.Parameters["MaterialColor"].SetValue(new Vector3(0.39f, 0.8f, 1f));

            mAlphaBlendState = new BlendState();
            mAlphaBlendState.ColorSourceBlend = Blend.SourceAlpha;
            mAlphaBlendState.AlphaSourceBlend = Blend.SourceAlpha;
            mAlphaBlendState.ColorDestinationBlend = Blend.InverseSourceAlpha;
            mAlphaBlendState.AlphaDestinationBlend = Blend.InverseSourceAlpha;   
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
                foreach (Effect effect in mesh.Effects)
                {
                    effect.Parameters["World"].SetValue(rotation * transforms[mesh.ParentBone.Index] * translation);
                    effect.Parameters["View"].SetValue(camera.viewMatrix);
                    effect.Parameters["Projection"].SetValue(camera.projectionMatrix);
                    effect.Parameters["LightPosition"].SetValue(mLightPosition);
                }
                // Draw the mesh, using the effects set above.
                mesh.Draw();
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
        /// <param name="model2worldTransformations">The instance transform matrices </param>
        /// <param name="numInstances">Number of instances to draw. Note: model2worldTransformations must be at least this long</param>
        public virtual void renderInstanced(Model model, Camera camera, Matrix[] model2worldTransformations, int numInstances)
        {
            if (numInstances <= 0) return;

            // Make sure our instance data vertex buffer is big enough. (4x4 float matrix)
            int instanceDataSize = 16 * sizeof(float) * numInstances;

            if ((mInstanceDataStream == null) ||
                (mInstanceDataStream.VertexCount < numInstances))
            {
                if (mInstanceDataStream != null)
                    mInstanceDataStream.Dispose();

                mInstanceDataStream = new DynamicVertexBuffer(mGraphicsDevice, mInstanceVertexDeclaration, numInstances, BufferUsage.WriteOnly);
            }

            // set vertex buffer stream
            // Upload transform matrices to the instance data vertex buffer.
            mInstanceDataStream.SetData(model2worldTransformations, 0,
                                        numInstances,
                                        SetDataOptions.Discard);

            // Draw the model. A model can have multiple meshes, so loop.
            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);

            // loop through meshes
            foreach (ModelMesh mesh in model.Meshes)
            {
                // get bone matrix
                Matrix boneMatrix = transforms[mesh.ParentBone.Index];

                foreach (ModelMeshPart part in mesh.MeshParts)
                {  
                    mGraphicsDevice.Indices = part.IndexBuffer;

                    part.Effect.Parameters["World"].SetValue( boneMatrix );
                    part.Effect.Parameters["View"].SetValue(camera.viewMatrix);
                    part.Effect.Parameters["Projection"].SetValue(camera.projectionMatrix);
                    part.Effect.Parameters["LightPosition"].SetValue(mLightPosition);

                    part.Effect.CurrentTechnique.Passes[0].Apply();

                    // set vertex buffer
                    mGraphicsDevice.SetVertexBuffers( new[] 
                    {
                        part.VertexBuffer,
                        new VertexBufferBinding(mInstanceDataStream, 0, 1 )
                    });
                    
                  
                    // draw primitives
                    mGraphicsDevice.DrawInstancedPrimitives( PrimitiveType.TriangleList, part.VertexOffset, 0, part.NumVertices, part.StartIndex, part.PrimitiveCount, numInstances );
                }
            }

            return;
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
