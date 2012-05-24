using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace BounceModel
{
    public class BounceModel
    {
        private CollidableModel.CollidableModel mCollidableModel;

        public Model model
        {
            get { return mCollidableModel.model; }
        }

        public CollidableModel.BspTree collisionData
        {
            get { return mCollidableModel.collisionData; }
        }
        
        public Model mShadowModel;
        public Model shadowModel
        {
            get { return mShadowModel; }
        }

        public BounceModel(CollidableModel.CollidableModel modelData, Model shadowData)
        {
            mCollidableModel = modelData;
            mShadowModel = shadowData;
        }
    }
}