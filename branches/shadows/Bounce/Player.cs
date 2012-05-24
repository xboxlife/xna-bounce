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
namespace Bounce
{
    /// <summary>
    /// Simple class for representing the player. Provides the mouse and keyboard controls, and 
    /// encapsulates a simple first person camera. Player position and camera orientation around
    /// X and Y axis can be set by the properties position, rotationX and rotationY. 
    /// </summary>
    public class Player
    {
        private Vector3 mPosition;
        public Vector3 position
        {
            get { return mPosition; }
            set { 
                mPosition = value;
                mBoundingSphere = new BoundingSphere(mPosition, mBoundingSphere.Radius);
            }
        }

        private Vector3 mLookAt;
        public Vector3 lookAt
        {
            get { return mLookAt; }
        }

        private BoundingSphere mBoundingSphere;
        public BoundingSphere boundingSphere
        {
            get { return mBoundingSphere; }
            set { mBoundingSphere = value; }
        }
	
        private float mRotationX = 0.0f;
        public float rotationX { set { mRotationX = value; } }

        private float mRotationY = 0.0f;
        public float rotationY { set { mRotationY = value; } }

        // mouse and keyboard sensitivity: input is scaled by these values
        private float mMouseSensitivity = 0.1f;
        private float mKeyboardSensitivity = 0.999f;

        private Camera mCamera;
        public Camera camera
        {
            get { return mCamera; }
        }

        public Player()
        {
            mCamera = new Camera();
            mBoundingSphere = new BoundingSphere(mPosition, 5);
        }

        public Vector3 shootDirection()
        {

            Vector3 forward = Vector3.Normalize(mLookAt - mPosition);
            return forward;
        }
	
        public void update( GameTime time ) {

            // process mouse input
            float dx = Program.Game.mouseMovement.X;
            float dy = Program.Game.mouseMovement.Y;

            mRotationX -= MathHelper.ToRadians(dy * mMouseSensitivity);
            mRotationY -= MathHelper.ToRadians(dx * mMouseSensitivity);

            Matrix rotate = Matrix.CreateRotationX(mRotationX);
            rotate *= Matrix.CreateRotationY(mRotationY);


            Vector3 forward = Vector3.Normalize(mLookAt - mPosition);
            Vector3 up = Vector3.Transform(Vector3.Up, rotate);

            if (Program.Game.keyboardInput.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.W))
                mPosition += forward * mKeyboardSensitivity;
            else if (Program.Game.keyboardInput.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.S))
                mPosition -= forward * mKeyboardSensitivity;

            Vector3 side = Vector3.TransformNormal(Vector3.Left, rotate);

            if( Program.Game.keyboardInput.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.A ) )
                mPosition += side * mKeyboardSensitivity;
            else if( Program.Game.keyboardInput.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D ) )
                mPosition -= side * mKeyboardSensitivity;

            if (Program.Game.keyboardInput.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Space))
                mPosition += up * mKeyboardSensitivity;


            // calculate new look at position and update camera
            mLookAt = mPosition + Vector3.Transform(Vector3.Forward, rotate);
            mCamera.update(time, mPosition, mLookAt, up);

            // update bounding sphere
            mBoundingSphere = new BoundingSphere(mPosition, mBoundingSphere.Radius);
        }
    }
}
