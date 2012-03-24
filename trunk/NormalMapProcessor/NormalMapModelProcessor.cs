#region File Description
// Normal mapping model processor: Based on the xna normal mapping sample
//-----------------------------------------------------------------------------
// NormalMappingModelProcessor.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;
using System.IO;
using System.ComponentModel;

namespace NormalMappingEffectPipeline
{
    /// <summary>
    /// The NormalMapModelProcessor is used to change the material/effect applied
    /// to a model. After going through this processor, the output model will be set
    /// up to be rendered with NormalMapping.fx.
    /// </summary>
    [ContentProcessor(DisplayName="Normal Mapping Model Processor")]
    public class NormalMappingModelProcessor : ModelProcessor
    {
        // The name under which we add the normal maps to the model
        public const string NormalMapKey = "NormalMap";
        String directory;

        public override bool GenerateTangentFrames
        {
            get { return true; }
            set {}
        }

        // Generates the tangent frames for all meshes. Note: for the normal generation, the 
        // texture channel is needed. Unfortunatly the arena model doesn't seem to be consistent
        // in the used texture channels. We therefore have to find the correct channel by analysing
        // all the used channel indices for all geometry batches in each mesh.
        private void GenerateTangents(NodeContent input, ContentProcessorContext context)
        {
            MeshContent mesh = input as MeshContent;
            int channel = -1;

            // find the index of the texture channel (sometimes 0, sometimes 1)
            if (mesh != null)
            {
                // loop through all geometry batches 
                foreach (GeometryContent geometryBatch in mesh.Geometry)
                {
                    // check the index of the texture channel
                    foreach (VertexChannel vertexChannel in geometryBatch.Vertices.Channels)
                    {
                        // is this a texture channel
                        if (vertexChannel.Name.Contains("Texture"))
                        {
                            // extract index (last letter, convert it to int)
                            char c = vertexChannel.Name[vertexChannel.Name.Length - 1];
                            int curChannel = (int)(c - '0');

                            
                            if (channel == -1)
                            {
                                // first time we see a texture channel for this mesh: store index
                                channel = curChannel; 
                            }
                            else if( channel != curChannel )
                            {
                                // we have already seen a texture channel for this mesh, but with a
                                // different index => signal error 
                                channel = -2;
                            }
                            
                        }
                    }
                }

                // have we found a valid texture channel?
                if (channel >= 0)
                {
                    // compute tangent frames
                    MeshHelper.CalculateTangentFrames(mesh,
                        VertexChannelNames.TextureCoordinate(channel),
                        VertexChannelNames.Tangent(0),
                        VertexChannelNames.Binormal(0));
                }
            }

            // recurse to all children
            foreach (NodeContent child in input.Children)
            {
                GenerateTangents(child, context);
            }
        }

        // Normals
        private void GenerateNormals(NodeContent input, ContentProcessorContext context)
        {
            MeshContent mesh = input as MeshContent;

            if (mesh != null)
            {
                MeshHelper.CalculateNormals(mesh, false);
            }

            foreach (NodeContent child in input.Children)
            {
                GenerateNormals(child, context);
            }
        }

        public override ModelContent Process(NodeContent input, ContentProcessorContext context)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
            directory = Path.GetDirectoryName(input.Identity.SourceFilename);

            GenerateNormals(input, context);
            GenerateTangents(input, context);

            LookUpNormalMapAndAddToTextures(input);
            return base.Process(input, context);
        }



        /// <summary>
        /// Adds for each texture the corresponding normal map to the material textures list
        /// the normal map name is generated from the texture name:
        /// [texture_name].tga => [texture_name]_normal.tga
        /// </summary>
        private void LookUpNormalMapAndAddToTextures(NodeContent node)
        {
            MeshContent mesh = node as MeshContent;
            if (mesh != null)
            {
                // for all geometry contents in the mesh
                foreach (GeometryContent geometry in mesh.Geometry)
                {
                    // does the geometry content contain a texture, and haven't we already processed it?
                    if (geometry.Material.Textures.Count > 0 && 
                        !geometry.Material.Textures.ContainsKey( NormalMapKey ) )
                    {
                        // extract the texture name 
                        BasicMaterialContent basicMaterial = (BasicMaterialContent)geometry.Material;

                        // replace .tga with _normal.tga
                        string normalMapPath = basicMaterial.Texture.Filename.Replace(".tga", "_normal.tga");

                        // add the texture to the textures list
                        geometry.Material.Textures.Add( 
                            NormalMapKey,
                            new ExternalReference<TextureContent>(normalMapPath)
                        );
                       
                    }
                }
            }

            // recurse to all children
            foreach (NodeContent child in node.Children)
            {
                LookUpNormalMapAndAddToTextures(child);
            }
        }


        protected override MaterialContent ConvertMaterial(MaterialContent material,
            ContentProcessorContext context)
        {
            EffectMaterialContent normalMappingMaterial = new EffectMaterialContent();
            normalMappingMaterial.Effect = new ExternalReference<EffectContent>
                ("Effects/ArenaShader.fx" );

            // copy the textures in the original material to the new normal mapping
            // material. this way the diffuse texture is preserved. The
            // PreprocessSceneHierarchy function has already added the normal map
            // texture to the Textures collection, so that will be copied as well.
            foreach (KeyValuePair<String, ExternalReference<TextureContent>> texture
                in material.Textures)
            {
                normalMappingMaterial.Textures.Add(texture.Key, texture.Value);
            }

            // and convert the material using the NormalMappingMaterialProcessor,
            // who has something special in store for the normal map.
            return context.Convert<MaterialContent, MaterialContent>
                (normalMappingMaterial, typeof(NormalMappingMaterialProcessor).Name);
        }
    }
}