using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using TRead = BounceModel.BounceModel;
namespace BounceModel
{
    public class BounceModelReader : ContentTypeReader<TRead>
    {
        protected override TRead Read(ContentReader input, TRead existingInstance)
        {
            CollidableModel.CollidableModel collidableModel = input.ReadObject<CollidableModel.CollidableModel>();
            Model shadowModel = input.ReadObject<Model>();

            return new BounceModel(collidableModel, shadowModel);
        }
    }
}
