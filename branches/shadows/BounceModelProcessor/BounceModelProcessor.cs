using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

using System.ComponentModel;
using System.Runtime.InteropServices;

using TInput = Microsoft.Xna.Framework.Content.Pipeline.Graphics.NodeContent;
using TOutput = BounceModelProcessor.BounceModelContent;

namespace BounceModelProcessor
{
    [ContentProcessor(DisplayName = "BounceModel")]
    public class BounceModelProcessor : ContentProcessor<TInput, TOutput>
    {
        private string mShadowShader = "Effects/ShadowMapShader.fx";
        [DisplayName("Shadow map shader")]
        [DefaultValue("Effects/ShadowMapShader.fx")]
        [Description("The shader used for rendering the shadow model into the shadow map")]
        public string ShadowShader
        {
            get { return mShadowShader; }
            set { mShadowShader = value; }
        }

        private string mGeometryProcessor = "ModelProcessor";
        [DisplayName("ModelProcessor")]
        [DefaultValue("ModelProcessor")]
        [Description("The processor used to convert the model to XNA format. Uses the standard Xna model processor 'ModelProcessor' by default")]
        public string GeometryProcessor
        {
            get { return mGeometryProcessor; }
            set { mGeometryProcessor = value; }
        }

        public override TOutput Process(TInput input, ContentProcessorContext context)
        {
            
            // generate collidable model first
            OpaqueDataDictionary processorParameters = new OpaqueDataDictionary();
            processorParameters.Add("ModelProcessor", GeometryProcessor);

            context.Logger.LogImportantMessage("BounceModelProcessor: Creating collidable model");
            var collidableModel = context.Convert<NodeContent, CollidableModelProcessor.CollidableModelContent>(input, "CollidableModelProcessor", processorParameters);
            context.Logger.LogImportantMessage("BounceModelProcessor: Done");

            // now generate shadow model
            context.Logger.LogImportantMessage("BounceModelProcessor: Creating shadow model");
            var shadowModel = generateShadowModel(collidableModel.collisionData.geometry, context);
            context.Logger.LogImportantMessage("BounceModelProcessor: Done");

            return new TOutput(collidableModel, shadowModel);
        }

        private ModelContent generateShadowModel(CollidableModelProcessor.ModelGeometryContent modelGeometry, ContentProcessorContext context)
        { 
             // set up geometry batch first.
            // NOTE: geometry.Vertices indexes into parent MeshContent.Positions while geometry.Indices indexes into geometry.Vertices
            var geometry = new GeometryContent();
            geometry.Vertices.AddRange(Enumerable.Range(0, modelGeometry.vertices.Count()));
            geometry.Indices.AddRange(modelGeometry.faces.SelectMany(f => 
                {
                    return new[]
                    { 
                        f.v1, 
                        f.v2, 
                        f.v3 
                    };
                }));

            geometry.Material = new EffectMaterialContent
            {
                Effect = new ExternalReference<EffectContent>(mShadowShader),
            };
    
            // now set up mesh 
            var mesh = new MeshContent();
            modelGeometry.vertices.ForEach(v => mesh.Positions.Add(v));
            mesh.Geometry.Add(geometry);
            mesh.Name = "ShadowModel";
            
            // create hierarchy
            NodeContent root = new NodeContent();
            root.Children.Add(mesh);

            // build
            return context.Convert<NodeContent, ModelContent>(root, "ModelProcessor");
        }
    }
}