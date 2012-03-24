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
using System.Globalization;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;


namespace Bounce
{
    /// <summary>
    /// Basic class for displaying a simple "user interface". The values of the set-only properties
    /// numCollisions, numTrinaglesChecked, timeCollisions, timePhysics, framesPerSecond and
    /// numSpheres are converted into messages at each update call, and draw() draws the messages
    /// to the screen. 
    /// </summary>
    public class Overlay
    {
        SpriteBatch mUI;
        SpriteFont mUIFont;
        SpriteFont mIntroFont;

        /// <summary>
        /// Number of collisions that occured 
        /// </summary>
        private int mNumCollisions;
        public int numCollisions
        {
            set { mNumCollisions = value; }
        }

        /// <summary>
        /// Total number of triangles checked for collision
        /// </summary>
        private int mNumTrianglesChecked;
	    public int numTrianglesChecked
	    {
		    set { mNumTrianglesChecked = value;}
	    }

	
        /// <summary>
        /// Total time needed for collision detection
        /// </summary>
        private double mTimeCollisions;
        public double timeCollisions
        {
            set { mTimeCollisions = value; }
        }

        /// <summary>
        /// Total time needed by physics simulation
        /// </summary>
        private double mTimePhysics;
        public double timePhysics
        {
            set { mTimePhysics = value; }
        }

        /// <summary>
        /// Current fps
        /// </summary>
        private double mFramesPerSecond;
        public double framesPerSecond
        {
            set { mFramesPerSecond = value; }
        }

        /// <summary>
        /// Current number of simulated spheres
        /// </summary>
        private int mNumSpheres;
        public int numSpheres
        {
            set { mNumSpheres = value; }
        }


        /// <summary>
        /// Rendering option: normalmapping
        /// </summary>
        private bool mNormalMappingEnabled;
        public bool normalMappingEnabled
        {
            get { return mNormalMappingEnabled; }
            set
            {
                mNormalMappingEnabled = value;
                mNormalMappingEnabledMessage = mNormalMappingEnabled ?
                    "Arena normal mapping: Enabled. (Press N to disable)" :
                    "Arena normal mapping: Disabled. (Press N to enable)";
            }
        }

        /// <summary>
        /// Rendering option: use textures
        /// </summary>
        private bool mTexturesEnabled;
        public bool texturesEnabled
        {
            get { return mTexturesEnabled; }
            set 
            { 
                mTexturesEnabled = value; 
                mTexturesEnabledMessage = mTexturesEnabled ?
                    "Arena textures: Enabled. (Press T to disable)":
                    "Arena textures: Disabled. (Press T to enable)";
            }
        }

        /// <summary>
        /// Rendering option: show colliding triangles
        /// </summary>
        private bool mDrawCollidingTriangles;
        public bool drawCollidingTriangles
        {
            get { return mDrawCollidingTriangles; }
            set
            {
                mDrawCollidingTriangles = value;
                mDrawCollidingTrianglesMessage = mDrawCollidingTriangles ?
                    "Draw colliding triangles: Enabled. (Press C to disable)" :
                     "Draw colliding triangles: Disabled. (Press C to enable";
            }
        }

        /// <summary>
        /// Rendering option: show colliding triangles
        /// </summary>
        private bool mDrawCollisionGeometry;
        public bool drawCollisionGeometry
        {
            get { return mDrawCollisionGeometry; }
            set
            {
                mDrawCollisionGeometry = value;
                mDrawCollisionGeometryMessage = mDrawCollisionGeometry ?
                    "Draw collision geometry: Enabled. (Press G to disable)":
                    "Draw collision geometry: Disabled. (Press G to enable";
            }
        }

        private Vector2 mTextTopLeft;
        private float mLineSpacing;

        public Overlay()
        {
            mTextTopLeft = new Vector2(10, 20);
           
        }

        private const float mIntroDisplayInterval = 4000f;  // time interval to show intro messages (in ms)
        private float mIntroTimer = 0;      // timer for intro messages

        private String mTexturesEnabledMessage;
        private String mNormalMappingEnabledMessage;
        private String mDrawCollidingTrianglesMessage;
        private String mDrawCollisionGeometryMessage;

        private String mCollisionsDisplay;
        private String mPhysicsDisplay;
        private String mRenderingDisplay;
        private String[] mIntroMessage;
        private uint mCurIntroMessage = 0;

        private NumberFormatInfo mNumberFormat;

        public void loadContent( ContentManager contentManager, GraphicsDeviceManager graphicsManager )
        {
            // load fonts
            mUIFont = contentManager.Load<SpriteFont>("UI_Font");
            mIntroFont = contentManager.Load<SpriteFont>("Intro");

            // create sprite batch
            mUI = new SpriteBatch(graphicsManager.GraphicsDevice);

            // space between the lines in the top left of the screen
            mLineSpacing = mUIFont.LineSpacing + 5;

            // prepare the intro messages
            mIntroMessage = new String[4];
            mIntroMessage[0] = "Welcome to Bounce!";
            mIntroMessage[1] = "You can navigate around by using\nthe W, A, S, D keys and the mouse.";
            mIntroMessage[2] = "Press the left mouse button to release a sphere.";
            mIntroMessage[3] = "Have Fun ;)";

            // Format used for displaying numbers in UI
            mNumberFormat = (NumberFormatInfo)NumberFormatInfo.GetInstance(null).Clone();
            mNumberFormat.NumberGroupSeparator = ".";
            mNumberFormat.NumberDecimalSeparator = ",";
        }

        public void update(GameTime time)
        {
            // prepare output strings
            mCollisionsDisplay = String.Format(
                mNumberFormat,
                "{0:n0} triangles checked for collision, in {1:0.00} ms: {2} triangles colliding",
                mNumTrianglesChecked, mTimeCollisions, mNumCollisions
            );

            mPhysicsDisplay = String.Format(
                mNumberFormat,
                "{0} spheres simulated in {1:0.00} ms",
                mNumSpheres,
                mTimePhysics
            );

            mRenderingDisplay = String.Format(
                mNumberFormat,
                "Simulation running at {0:0.00} FPS",
                mFramesPerSecond
            );

            // update timer for intro messages
            mIntroTimer += (float)time.ElapsedGameTime.TotalMilliseconds;
            if (mIntroTimer > mIntroDisplayInterval) {
                mIntroTimer = 0;
                mCurIntroMessage = (uint)Math.Min( mCurIntroMessage+1, mIntroMessage.Length );

            }

            mCurIntroMessage = (uint)mIntroMessage.Length;
        }

        public void draw()
        {

            mUI.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);

            // are we already past intro message stage?
            if( mCurIntroMessage >= mIntroMessage.Length-1 )
            {
                // Find the center of the string
                Vector2 FontOrigin = mTextTopLeft;

                var lines = new[]
                    {
                        mPhysicsDisplay, 
                        mCollisionsDisplay, 
                        mRenderingDisplay, 
                        mTexturesEnabledMessage, 
                        mNormalMappingEnabledMessage,
                        mDrawCollidingTrianglesMessage, 
                        mDrawCollisionGeometryMessage,
                        "Press L to switch light positions"
                    };

                for( int line=0; line<lines.Length; ++line)
                {
                    mUI.DrawString(mUIFont, lines[line], FontOrigin + line * mLineSpacing * Vector2.UnitY, Color.Beige);
                }
            }
            
            // still into showing intro messages
            if( mCurIntroMessage < mIntroMessage.Length )
            {
                Vector2 center = mIntroFont.MeasureString(mIntroMessage[mCurIntroMessage]) / 2;
                Vector2 pos = new Vector2(mUI.GraphicsDevice.Viewport.Width, mUI.GraphicsDevice.Viewport.Height) / 2;
                mUI.DrawString(mIntroFont, mIntroMessage[mCurIntroMessage], pos, Color.Beige, 0, center, 1.0f, SpriteEffects.None, 1.0f);
            }

            mUI.End();

        }
    }
}
