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

namespace Bounce
{
    /// <summary>
    /// Simple camera class.
    /// </summary>
    public class Camera
    {

        private Matrix mViewMatrix;
        public Matrix viewMatrix
        {
            get { return mViewMatrix; }
            set { mViewMatrix = value; }
        }

        private Matrix mProjectionMatrix;
        public Matrix projectionMatrix
        {
            get { return mProjectionMatrix; }
            set { mProjectionMatrix = value; }
        }

        private float mFieldOfView;
        public float fieldOfView
        {
            get { return mFieldOfView; }
            set { mFieldOfView = value; calcProjectionMatrix(); }
        }

        private float mAspectRatio;

        public float aspectRatio
        {
            get { return mAspectRatio; }
            set { mAspectRatio = value; calcProjectionMatrix(); }
        }

        private float mNearPlane;
        public float nearPlane
        {
            get { return mNearPlane; }
            set { mNearPlane = value; calcProjectionMatrix(); }
        }

        private float mFarPlane;
        public float farPlane
        {
            get { return mFarPlane; }
            set { mFarPlane = value; calcProjectionMatrix(); }
        }

        public Camera()
        {
            // initialize with reasonable values
            mFieldOfView = MathHelper.ToRadians(45.0f);
            mAspectRatio = 800.0f / 600.0f;
            mNearPlane = 0.25f;// 1.0f;
            mFarPlane = 10000.0f;


            // compute projection matrix
            calcProjectionMatrix();
        }

        private void calcProjectionMatrix()
        {
            mProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(mFieldOfView, mAspectRatio, mNearPlane, mFarPlane);
        }



        public virtual void update(GameTime time, Vector3 playerPos, Vector3 playerLookAt, Vector3 playerUp)
        {
            mViewMatrix = Matrix.CreateLookAt(playerPos, playerLookAt, playerUp);
        }
    }

    public class VirtualCamera : Camera
    {
        Matrix mView;
        float mRotationY = 0.0f;

        public VirtualCamera()
        {
            mView = new Matrix(
                0.999263f, 0.00147375371f, -0.038360253f, 0.0f,
                0.0000000126671047f, 0.999262869f, 0.03839077f, 0.0f,
                 0.0383885577f, -0.038362477f, 0.9985262f, 0.0f,
                -37.09832f, -76.10451f, -291.163971f, 1.0f
             );

            viewMatrix = mView;
        }

        public override void update(GameTime time, Vector3 playerPos, Vector3 playerLookAt, Vector3 playerUp)
        {
            mRotationY += time.ElapsedGameTime.Milliseconds * 0.001f;
            viewMatrix = Matrix.CreateRotationY(mRotationY) * mView;
        }
    }
}