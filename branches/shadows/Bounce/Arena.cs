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


using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System.Linq;

using CollidableModel;
using BounceModel;

namespace Bounce
{
    /// <summary>
    /// Arena is a wrapper class for Model, for easy configuration of the arena model.
    /// Properties normalMapping and noTextures allow easy manipulation of the drawing,
    /// the underlying Model can be accessed by model. 
    /// </summary>
    public class Arena
    {
        /// <summary>
        /// encapsulated Model
        /// </summary>
        private BounceModel.BounceModel mData;
        public BounceModel.BounceModel Data
        {
            get { return mData; }
        }

        public Model model
        {
            get { return mData.model; }
        }

        public BspTree CollisionData
        {
            get { return mData.collisionData; }
        }

        public Model shadowModel
        {
            get { return mData.shadowModel; }
        }

        /// <summary>
        /// Enable normalmapping for rendering the arena?
        /// </summary>
        private bool mNormalMapping;
        public bool normalMapping
        {
            set { mNormalMapping = value; updateTechnique(); }
            get { return mNormalMapping; }
        }

        private BoundingSphere mBoundingSphere;
        public BoundingSphere BoundingSphere
        {
            get { return mBoundingSphere; }
        }

        /// <summary>
        /// Disable textures for rendering the arena?
        /// </summary>
        private bool mNoTextures;
        public bool noTextures
        {
            set { mNoTextures = value; updateTechnique(); }
            get { return mNoTextures; }
        }

        /// <summary>
        /// Render shadow split index
        /// </summary>
        private bool mRenderShadowSplitIndex;
        public bool renderShadowSplitIndex
        {
            set { mRenderShadowSplitIndex = value; updateTechnique(); }
            get { return mRenderShadowSplitIndex; }
        }

        public Arena() 
        {
            mNormalMapping = true;
            mNoTextures = false;
        }


        /// <summary>
        /// Loads the arena model, and sets the material parameters. For the sake of simplicity these
        /// parameters are hardcoded here, in a real application they should be read from a config file
        /// or from the model file itself.
        /// </summary>
        /// <param name="contentManager">Used to load the arena model "Models/arena"</param>
        /// <param name="graphicsManager">Used to load the arena shader "Effects/ArenaShader"</param>
        public void loadContent(ContentManager contentManager, GraphicsDeviceManager graphicsManager)
        {
            mData = contentManager.Load<BounceModel.BounceModel>("Models/arena");

            // compute bounding sphere
            {
                var vertices = CollisionData.geometry.vertices.Where(v => v.Y > -50).ToArray();
                var center = vertices.Aggregate((v1, v2) => v1 + v2);
                center /= vertices.Length;

                var radius = vertices.Max(v => (v - center).LengthSquared());
                radius = (float)Math.Sqrt(radius);
                mBoundingSphere = new BoundingSphere(center, radius);
            }

        }
        public void initShaders()
        {
            // set lighting parameters for each shader
            foreach (var part in model.Meshes.SelectMany(m => m.MeshParts))
            {
                part.Effect.Parameters["LightDirection"].SetValue(Vector3.Normalize(new Vector3(100, 130, 0)));

                part.Effect.Parameters["LightColor"].SetValue(new Vector4(0.8f, 0.8f, 0.8f, 1.0f));
                part.Effect.Parameters["AmbientLightColor"].SetValue(new Vector4(0.2f, 0.2f, 0.2f, 1.0f));

                part.Effect.Parameters["Shininess"].SetValue(0.01f);
                part.Effect.Parameters["SpecularPower"].SetValue(8.0f);

                part.Effect.Parameters["PoissonKernel"].SetValue(Program.Game.renderer.poissonKernel);
                part.Effect.Parameters["RandomTexture3D"].SetValue(Program.Game.renderer.randomTexture3D);
                part.Effect.Parameters["RandomTexture2D"].SetValue(Program.Game.renderer.randomTexture3D);
                part.Effect.CurrentTechnique = part.Effect.Techniques["LightTexturesNormalmaps"];
            }
            
        }

        /// <summary>
        /// Updates the shaders technique according to the properties normaMapping and
        /// noTextures
        /// </summary>
        private void updateTechnique()
        {
            string technique;

            if (mRenderShadowSplitIndex)
            {
                technique = "ShadowSplitIndex";
            }
            else
            {
                if (noTextures)
                    technique = "Light";
                else
                    technique ="LightTexturesNormalmaps";
            }
            foreach (var part in model.Meshes.SelectMany(m => m.MeshParts))
            {
                part.Effect.CurrentTechnique = part.Effect.Techniques[technique];               
            }
        }    
    }
}
