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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;
using System.Diagnostics;

using CollidableModel;
using BounceModel;
using System.Linq;

namespace Bounce
{
    /// <summary>
    /// This is the main class for the game. It holds the instances of the sphere simulator,
    /// the arena, the bsp tree, renderer, GUI (Overlay) and player. It contains the main 
    /// game loop, and provides keyboard and mouse input.
    /// </summary>
    public class BounceGame : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager mGraphicsManager;
        Arena mArena;

        const uint MaximumSimulatedSpheres = 1000;

        /// <summary>
        /// Current keyboard state. Fetched at the most recent update() call.
        /// </summary>
        private KeyboardState mKeyboardInput;
        public KeyboardState keyboardInput
        {
            get { return mKeyboardInput; }
        }

        /// <summary>
        /// The keyboard state of the previous game loop iteration
        /// </summary>
        private KeyboardState mPreviousKeyboardInput;
        public KeyboardState previousKeyboardInput
        {
            get { return mPreviousKeyboardInput; }
        }

        /// <summary>
        /// Current mouse state. Fetched at the most recent update() call.
        /// </summary>
        private MouseState mMouseInput;
        public MouseState mouseInput
        {
            get { return mMouseInput; }
        }

        /// <summary>
        /// The mouse state of the previous game loop iteration
        /// </summary>
        private MouseState mPreviousMouseInput;
        public MouseState previousMouseInput
        {
            get { return mPreviousMouseInput; }
        }

        /// <summary>
        /// Mouse movement between the last update calls. Note: Not equal to 
        /// mouseInput - previousMouseInput, as the mouse position is set to the origin
        /// after each update call.
        /// </summary>
        private Vector2 mMouseMovement;
        public Vector2 mouseMovement
        {
            get { return mMouseMovement; }
        }


        /// <summary>
        /// The player
        /// </summary>
        private Player mPlayer;
        public Player player
        {
            get { return mPlayer; }
        }

        private Renderer mRenderer;
        public Renderer renderer
        {
            get { return mRenderer; }
        }

        private Vector3[] mLightPositions;      // predefined hardcoded light positions
        private uint mCurrentLightPosition = 0; // currently used light position
        private float mLightRotationY = 0.01f;          // current rotation angle of the light around y axis

        BouncingSphereSimlulator mSimulator;    // the sphere simulator
        Overlay mOverlay;                       // GUI overlay

        BounceModel.BounceModel mSphere;                     // sphere model (drawn using instancing)
        Matrix[] mSpherePositionBuffer;         // cache for model matrices of all simulated spheres

        Model mSkyBox;

        // temporary storage for colliding triangles. for each triangle, the number of remaining frames 
        // where it will be shown is stored.
        private Dictionary<Face, uint> mCollidingFacesBuffer = new Dictionary<Face,uint>(); 
        private const uint mNumTicksCollidingFaceIsVisible = 80;   // number of ticks colliding triangles are highlighted


        // average the timings over several frames
        const uint mNumFramesForAverage = 100;
        double mAverageCollisionTime = 0;
        double mAveragePhysicsTime = 0;
        uint mNumTicksInCurrentAverage = 0;

        // display settings
        public const uint DisplayWidth = 1280;
        public const uint DisplayHeight = 720;

        public bool mBlockInput = false;

        public enum VirtualCameraMode
        {
            None,
            ViewFrustum,
            ShadowSplits
        };

        Camera mVirtualCamera = new VirtualCamera();
        VirtualCameraMode mVirtualCameraMode = VirtualCameraMode.None;

        Renderer.ShadowMapOverlayMode mShadowMapOverlay = Renderer.ShadowMapOverlayMode.None;

        public BounceGame()
        {
            mGraphicsManager = new GraphicsDeviceManager(this);

            // define USE_PERFHUD to enable the use of perfhud on nvidia graphics cards 
#if USE_PERFHUD
            mGraphicsManager.PreparingDeviceSettings +=
                       new EventHandler<PreparingDeviceSettingsEventArgs>(graphics_PreparingDeviceSettings);
#endif 

            Content.RootDirectory = "Content";

            mPlayer = new Player();
            mArena = new Arena();
            mRenderer = new Renderer();

            // default window size
            mGraphicsManager.PreferredBackBufferWidth = 1280;
            mGraphicsManager.PreferredBackBufferHeight = 720;
            mGraphicsManager.PreferMultiSampling = true;

            // predefined light positions
            mLightPositions = new Vector3[3];
            mLightPositions[0] = new Vector3(843, 1986, -55);
            mLightPositions[1] = new Vector3(1865, 121, -21);
            mLightPositions[2] = new Vector3(0, 20, 00);

            mLightRotationY = (float)Math.PI / 180.0f * 25.0f;

            // init player position and orientation to sensitive defaults
            mPlayer.position = new Vector3(0, 10, 0);
            mPlayer.rotationX = -0.0610865131f;
            mPlayer.rotationY = -1.6057024f;

            mSpherePositionBuffer = new Matrix[MaximumSimulatedSpheres];

            mOverlay = new Overlay();
            mOverlay.showShadowSplits = mArena.renderShadowSplitIndex;
            mOverlay.virtualCameraMode = mVirtualCameraMode;
            mOverlay.shadowOverlayMode = mShadowMapOverlay;

        }


        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            mArena.loadContent(Content, mGraphicsManager);
            mRenderer.loadContent(Content, mGraphicsManager, mArena.CollisionData.geometry);

            mArena.initShaders();

            // load bouncing sphere model and apply instancing shader
            {
                Effect instancingShader = Content.Load<Effect>("Effects/InstancingShader");
                mSphere = Content.Load<BounceModel.BounceModel>("Models/soccer_ball_232_tris");

                foreach (ModelMeshPart part in mSphere.model.Meshes.SelectMany(m => m.MeshParts))
                {
                    BasicEffect basicEffect = part.Effect as BasicEffect;
                    part.Effect = instancingShader.Clone();
                    part.Effect.Parameters["Texture"].SetValue(basicEffect.Texture);
                    part.Effect.Parameters["LightColor"].SetValue(new Vector4(1f, 1f, 1f, 1.0f));
                    part.Effect.Parameters["AmbientLightColor"].SetValue(new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
                    part.Effect.Parameters["MaterialColor"].SetValue(new Vector4(0.65f, 0.0f, 0.0f, 1.0f));
                    part.Effect.Parameters["Shininess"].SetValue(0.1f);
                    part.Effect.Parameters["PoissonKernel"].SetValue(mRenderer.poissonKernel);
                    part.Effect.Parameters["RandomTexture3D"].SetValue(mRenderer.randomTexture3D);
                    part.Effect.Parameters["RandomTexture2D"].SetValue(mRenderer.randomTexture3D);
                    part.Effect.Parameters["SpecularPower"].SetValue(4.0f);
                    part.Effect.CurrentTechnique = part.Effect.Techniques["Light"];
                }


                foreach (var eff in mSphere.shadowModel.Meshes.SelectMany(m => m.Effects))
                {
                    eff.CurrentTechnique = eff.Techniques["ShadowInstanced"];
                }
            }

            // load skybox and apply skybox shader
            {
                mSkyBox = Content.Load<Model>("Models/sky_box");
                Effect envSphereShader = Content.Load<Effect>("Effects/SkyBoxShader");

                foreach (var part in mSkyBox.Meshes.SelectMany(m => m.MeshParts))
                {
                    BasicEffect e = (BasicEffect)part.Effect;
                    part.Effect = envSphereShader.Clone();
                    part.Effect.Parameters["Texture"].SetValue(e.Texture);

                }
            }

            // create the simulator
            mSimulator = new BouncingSphereSimlulator(mArena.CollisionData);

            mOverlay.loadContent(Content, mGraphicsManager);

            mArena.noTextures = false;
            mArena.normalMapping = true;

            mOverlay.normalMappingEnabled = mArena.normalMapping;
            mOverlay.texturesEnabled = !mArena.noTextures;
            mOverlay.drawCollidingTriangles = true;
            mOverlay.drawCollisionGeometry = false;

            mPlayer.camera.aspectRatio = mGraphicsManager.GraphicsDevice.Viewport.AspectRatio;
        }

        protected override void Initialize()
        {
            base.Initialize();

            Mouse.SetPosition(
                GraphicsDevice.PresentationParameters.BackBufferWidth / 2,
                GraphicsDevice.PresentationParameters.BackBufferHeight / 2
                );

            // init averaging parameters
            mAverageCollisionTime = 0;
            mAveragePhysicsTime = 0;
            mNumTicksInCurrentAverage = 0;

        }


        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // get input first
            gatherInput();

            if (keyboardInput.IsKeyDown(Keys.B) && !previousKeyboardInput.IsKeyDown(Keys.B))
            {
                mBlockInput = !mBlockInput;
            }

            if (mBlockInput)
                return;

            // move player and handle player arena collisions
            Vector3 prevPlayerPosition = mPlayer.position;
            mPlayer.update(gameTime);

            // make sure player can't move inside geometry
            correctPlayerPosition(mPlayer, prevPlayerPosition);


            // check for mouse pressed: user can create a new sphere            
            if (mouseInput.LeftButton == ButtonState.Pressed &&
                previousMouseInput.LeftButton == ButtonState.Released &&
                mSimulator.numSpheres < MaximumSimulatedSpheres)
            {
                mSimulator.addSphere(mPlayer.position, mPlayer.shootDirection() * 20f);
            }

            // check for other keys pressed on keyboard
            if (keyboardInput.IsKeyDown(Keys.T) && !previousKeyboardInput.IsKeyDown(Keys.T))
            {
                mArena.renderShadowSplitIndex = !mArena.renderShadowSplitIndex;
                mOverlay.showShadowSplits = mArena.renderShadowSplitIndex;
               
            }
            if (keyboardInput.IsKeyDown(Keys.R) && !previousKeyboardInput.IsKeyDown(Keys.R))
            {
                mRenderer.mSnapShadowMaps = !mRenderer.mSnapShadowMaps;
            }
            if (keyboardInput.IsKeyDown(Keys.X) && !previousKeyboardInput.IsKeyDown(Keys.X))
            {
                int virtualCameraMode = ((int)mVirtualCameraMode + 1) % Enum.GetValues(typeof(VirtualCameraMode)).Length;
                mVirtualCameraMode = (VirtualCameraMode)virtualCameraMode;
                mOverlay.virtualCameraMode = mVirtualCameraMode;
            }

            if (keyboardInput.IsKeyDown(Keys.E) && !previousKeyboardInput.IsKeyDown(Keys.E))
            {
                 int overlayMode = ((int)mShadowMapOverlay + 1) % Enum.GetValues(typeof(Renderer.ShadowMapOverlayMode)).Length;
                 mShadowMapOverlay = (Renderer.ShadowMapOverlayMode)overlayMode;
                 mOverlay.shadowOverlayMode = mShadowMapOverlay;
            }
            

            // update spheres simulation
            mSimulator.update(gameTime);
            if(keyboardInput.IsKeyDown(Keys.V))
                mVirtualCamera.update(gameTime, Vector3.Zero, Vector3.Forward, Vector3.Up);

            // update list of colliding faces
            foreach (Face f in mSimulator.collidingFaces.Keys)
            {
                if (mCollidingFacesBuffer.ContainsKey(f))
                    mCollidingFacesBuffer[f] = mNumTicksCollidingFaceIsVisible;
                else
                    mCollidingFacesBuffer.Add(f, mNumTicksCollidingFaceIsVisible);
            }

            // subtract 1 from remaining frames counter for each face. remove faces with counter 0
            Face[] tmpBuffer = mCollidingFacesBuffer.Keys.ToArray();

            foreach (Face f in tmpBuffer)
            {
                if (mCollidingFacesBuffer[f] <= 1)
                    mCollidingFacesBuffer.Remove(f);
                else
                    mCollidingFacesBuffer[f]--;
            }

            // check for Quit key (Q) or (ESC) pressed
            if (keyboardInput.IsKeyDown(Keys.Q)|| keyboardInput.IsKeyDown( Keys.Escape ))
                Exit();

            // rotate light
            //mLightRotationY += 0.0002f;
            Vector3 curLightPos = Vector3.Transform(mLightPositions[mCurrentLightPosition], Matrix.CreateRotationY(mLightRotationY));
            mRenderer.setLightPosition(curLightPos, mArena);

            // update average timing data
            mAverageCollisionTime += mSimulator.timeCollisions;
            mAveragePhysicsTime += mSimulator.timePhysics;
            mNumTicksInCurrentAverage++;

            if( mNumTicksInCurrentAverage == mNumFramesForAverage )
            {
                mOverlay.timeCollisions = mAverageCollisionTime / mNumFramesForAverage;
                mOverlay.timePhysics = mAveragePhysicsTime / mNumFramesForAverage;

                mAverageCollisionTime = 0;
                mAveragePhysicsTime = 0;
                mNumTicksInCurrentAverage = 0;
            }

            // set data for UI
            mOverlay.numSpheres = mSimulator.numSpheres;
            mOverlay.numCollisions = mSimulator.collidingFaces.Count;
            mOverlay.numTrianglesChecked = (mSimulator.numCollisionTests + 1) * mArena.CollisionData.geometry.faces.Length; // also count player level collisions
            mOverlay.debugText = mRenderer.mSnapShadowMaps;

            // update UI
            mOverlay.update(gameTime);

            base.Update(gameTime);
        }

        // reads keyboard and mouse input, sets mouse position to the origin (center of the window)
        private void gatherInput()
        {
           
                // store previous input
                mPreviousKeyboardInput = mKeyboardInput;
                mPreviousMouseInput = mMouseInput;

                // get new input
                mKeyboardInput = Keyboard.GetState();
                mMouseInput = Mouse.GetState();
                mMouseMovement = new Vector2(
                    mMouseInput.X - GraphicsDevice.PresentationParameters.BackBufferWidth / 2,
                    mMouseInput.Y - GraphicsDevice.PresentationParameters.BackBufferHeight / 2
                    );

                // set mouse pos to center of window, in order to avoid getting stuck at edges of screen
                if (!mBlockInput)
                {
                    Mouse.SetPosition(
                        GraphicsDevice.PresentationParameters.BackBufferWidth / 2,
                        GraphicsDevice.PresentationParameters.BackBufferHeight / 2
                        );
                }
         
        }

        // checks for player - level collisions, moves player out of colliding state if necessary
        private void correctPlayerPosition(Player player, Vector3 playerLastPosition)
        {
            // query collisions
            LinkedList<Vector3> arenaCollisions = new LinkedList<Vector3>();
            LinkedList<Face> arenaCollidingFaces = new LinkedList<Face>();
            mArena.CollisionData.collisions(player.boundingSphere, arenaCollidingFaces, arenaCollisions);

            if (arenaCollisions.Count == 0) return;

            // get average face normal of colliding faces, and average collision point
            Vector3 avgNormal = Vector3.Zero;
            Vector3 avgCollPt = Vector3.Zero;

            Vector3 closestNormal = Vector3.Zero;
            float closestDistance = float.MaxValue;

            LinkedListNode<Vector3> curNode = arenaCollisions.First;
            foreach (Face f in arenaCollidingFaces)
            {
                avgNormal += f.faceNormal;
                avgCollPt += curNode.Value;

                float d = (player.position - curNode.Value).LengthSquared();
                if (d < closestDistance)
                {
                    closestDistance = d;
                    closestNormal = f.faceNormal;
                }

                curNode = curNode.Next;
            }

            // this can happen at very thin walls for example (two normals pointing in the opposite direction)
            if (avgNormal == Vector3.Zero)
            {
                avgNormal = closestNormal;
            }

            avgNormal.Normalize();
            avgCollPt /= arenaCollisions.Count;


            // collision direction
            Vector3 avgColVec = player.position - avgCollPt;
            Vector3 r = Vector3.Normalize(-avgColVec);
            r *= player.boundingSphere.Radius;

            // move player out of colliding state
            player.position += (-r - avgColVec);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            // set up initial graphics states
            mGraphicsManager.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            mGraphicsManager.GraphicsDevice.BlendState = BlendState.Opaque;

            // set instancing data on renderer
            int numSpheres = 0;
             mSimulator.getSpheres(ref mSpherePositionBuffer, ref numSpheres);
            mRenderer.setInstancingData(mSpherePositionBuffer, numSpheres);  
            
            // determine shadow frustums
            var shadowCamera = mVirtualCameraMode != VirtualCameraMode.None ? mVirtualCamera : mPlayer.camera;
            mRenderer.setShadowTransforms(shadowCamera, mArena);

            // render shadow maps first, then scene
            DrawShadows(gameTime, shadowCamera, numSpheres);
            DrawScene(gameTime, mPlayer.camera, numSpheres);

            if (mShadowMapOverlay != Renderer.ShadowMapOverlayMode.None)
            {
                mRenderer.renderShadowMapOverlay(shadowCamera, mArena.shadowModel, mShadowMapOverlay);
            }

            // render overlay
            mOverlay.framesPerSecond = 1000.0 / gameTime.ElapsedGameTime.TotalMilliseconds;
            mOverlay.draw();

            base.Draw(gameTime);
        }

        public void DrawShadows(GameTime gameTime, Camera camera, int numSpheres)
        {
            mRenderer.SetCurrentPass(Renderer.RenderPass.ShadowPass);
            mRenderer.renderShadow(mArena.shadowModel, mSphere.shadowModel, numSpheres, camera);
        }

        public void DrawScene(GameTime gameTime, Camera camera, int numSpheres)
        {
            mRenderer.SetCurrentPass(Renderer.RenderPass.ColourPass);

            // render environment sphere 
            {
                foreach (var effect in mSkyBox.Meshes.SelectMany(m => m.Effects))
                {
                    effect.CurrentTechnique = effect.Techniques[mOverlay.drawCollisionGeometry ? "Black" : "TextureNoLight"];
                }

                mRenderer.render(mSkyBox, Matrix.CreateRotationY(mLightRotationY), Matrix.CreateTranslation(mPlayer.position), camera);
            }

            // render arena 
            if (!mOverlay.drawCollisionGeometry)
            {
                mRenderer.render(mArena.model, camera);
            }
            else
            {
                mRenderer.renderCollisionGeometry(mArena.CollisionData.geometry, camera);
            }

            // render spheres
            mRenderer.renderInstanced(mSphere.model, camera, numSpheres);

            // render colliding faces
       //     if (mOverlay.drawCollidingTriangles)
       //         mRenderer.renderCollidingFaces(mArena.CollisionData.geometry, mCollidingFacesBuffer, camera, 0, mNumTicksCollidingFaceIsVisible);

            // render virtual camera
            switch (mVirtualCameraMode)
            {
                case VirtualCameraMode.ShadowSplits:
                    for (int i = 0; i < mRenderer.ShadowSplitProjections.Count(); ++i)
                    {
                        mRenderer.renderFrustum(mRenderer.ShadowSplitProjections[i], mRenderer.ShadowSplitColors[i], camera, 0.25f * i);
                    }
                    break;
                case VirtualCameraMode.ViewFrustum:
                    for (int i = 0; i < mRenderer.ViewFrustumSplits.Count(); ++i)
                    {
                        mRenderer.renderFrustum(mRenderer.ViewFrustumSplits[i], mRenderer.ShadowSplitColors[i], camera.viewMatrix * camera.projectionMatrix, 0.0f);
                    }
                    break;
            }
        }


        // selects the nvidia perfhud adapter, if present. define USE_PERFHUD if you want 
        // this function to be called (see constructor BounceGame() )
        void graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            foreach (GraphicsAdapter curAdapter in GraphicsAdapter.Adapters)
            {
                if (curAdapter.Description.Contains("PerfHUD"))
                {
                    e.GraphicsDeviceInformation.Adapter = curAdapter;
                    Microsoft.Xna.Framework.Graphics.GraphicsAdapter.UseReferenceDevice = true;
                    break;
                }
            }
        }
    }
}
