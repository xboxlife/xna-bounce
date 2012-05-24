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
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

using CollidableModel;

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
        private CollidableModel.CollidableModel mData;

        public Model model
        {
            get { return mData.model; }
        }

        public BspTree CollisionData
        {
            get { return mData.collisionData; }
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


        /// <summary>
        /// Disable textures for rendering the arena?
        /// </summary>
        private bool mNoTextures;
        public bool noTextures
        {
            set { mNoTextures = value; updateTechnique(); }
            get { return mNoTextures; }
        }

        public Arena() {
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
            Effect shader = contentManager.Load<Effect>("Effects/ArenaShader");
            mData = contentManager.Load<CollidableModel.CollidableModel>("Models/arena");

            // set lighting parameters for each shader
            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    part.Effect.Parameters["LightPosition"].SetValue(new Vector3(100, 130, 0));
                   
                    part.Effect.Parameters["LightColor"].SetValue(new Vector4(0.8f, 0.8f, 0.8f, 1.0f));
                    part.Effect.Parameters["AmbientLightColor"].SetValue(new Vector4(0.2f, 0.2f, 0.2f, 1.0f));

                    part.Effect.Parameters["Shininess"].SetValue(0.01f);
                    part.Effect.Parameters["SpecularPower"].SetValue(8.0f);

                    part.Effect.CurrentTechnique = part.Effect.Techniques["LightTexturesNormalmaps"];
                }
            }

        }

        /// <summary>
        /// Updates the shaders technique according to the properties normaMapping and
        /// noTextures
        /// </summary>
        private void updateTechnique()
        {
            string technique;

            if (mNormalMapping)
            {
                technique = mNoTextures ? "LightNormalmaps" : "LightTexturesNormalmaps";
            }
            else
            {
                technique = mNoTextures ? "Light" : "LightTextures";
            }

            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    part.Effect.CurrentTechnique = part.Effect.Techniques[technique];
                }
            }
        }    
    }
}
