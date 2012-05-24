using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;

// TODO: replace this with the type you want to write out.
using TWrite = BounceModelProcessor.BounceModelContent;

namespace BounceModelProcessor
{
    [ContentTypeWriter]
    public class BounceModelContentWriter : ContentTypeWriter<TWrite>
    {
        protected override void Write(ContentWriter output, TWrite value)
        {
            output.WriteObject(value.collidableModel);
            output.WriteObject(value.shadowModel);
        }

        public override string GetRuntimeReader(TargetPlatform targetPlatform)
        {
            return "BounceModel.BounceModelReader, BounceModel_" + targetPlatform + ", " +
                 "Version=1.0.0.0, Culture=neutral";
        }
    }
}
