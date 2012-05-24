using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;

namespace BounceModelProcessor
{
    public class BounceModelContent
    {
        private CollidableModelProcessor.CollidableModelContent mCollidableModel;
        public CollidableModelProcessor.CollidableModelContent collidableModel
        {
            get { return mCollidableModel; }
        }

        public ModelContent mShadowModel;
        public ModelContent shadowModel
        {
            get { return mShadowModel; }
        }

        public BounceModelContent(CollidableModelProcessor.CollidableModelContent modelData, ModelContent shadowData)
        {
            mCollidableModel = modelData;
            mShadowModel = shadowData;
        }
    }
}
