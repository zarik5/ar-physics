using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework.WpfInterop;
using XColor = Microsoft.Xna.Framework.Color;

namespace ArPhysics2
{
    public class GraphicsControl : WpfGame
    {
        //constants
        public const float NEAR_PLANE = .1f;
        public const float FAR_PLANE = 100f;
        public const float FOV = 52 * MathHelper.Pi / 180;
        //public const string FOOTBALL_STR = "football";
        public const float TRANSP_COLOR_A = .6f;
        public const float DISABLED_COLOR_A = .3f;
        static readonly (Color, (XColor color, float alpha))[] COLORS = new[]
        {
            (Color.RED, (XColor.Red, 1f)),
            (Color.ORANGE, (XColor.Orange, 1f)),
            (Color.YELLOW, (XColor.Yellow, 1f)),
            (Color.GREEN, (XColor.Green, 1f)),
            (Color.BLUE, (XColor.Blue, 1f)),
            (Color.PURPLE, (XColor.Purple, 1f)),
            (Color.PINK, (XColor.HotPink, 1f)),
            (Color.BLACK, (XColor.Black, 1f)),
            (Color.GRAY, (XColor.Gray, 1f)),
            (Color.TRANSPARENT, (XColor.Black, 0f)),
            (Color.TRANSPARENT_RED, (XColor.Red, TRANSP_COLOR_A)),
            (Color.TRANSPARENT_GREEN, (XColor.Green, TRANSP_COLOR_A)),
            (Color.TRANSPARENT_BLUE, (XColor.Blue, TRANSP_COLOR_A)),
            (Color.TRANSPARENT_GRAY, (XColor.Gray, TRANSP_COLOR_A)),
            (Color.TRANSPARENT_LIGHT_BLUE, (XColor.LightBlue, TRANSP_COLOR_A)),
            (Color.DISABLED_GRAY, (XColor.Gray, DISABLED_COLOR_A)),
        };

        const float COORD = 1f;
        static readonly VertexPositionTexture[] screenQuad = new[] // clockwise, should be in front of any geometry
        {
            new VertexPositionTexture(new Vector3(-COORD, COORD, .1f), new Vector2(0, 0)),
            new VertexPositionTexture(new Vector3(COORD, COORD, .1f), new Vector2(1, 0)),
            new VertexPositionTexture(new Vector3(-COORD, -COORD, .1f), new Vector2(0, 1)),
            new VertexPositionTexture(new Vector3(COORD, -COORD, .1f), new Vector2(1, 1)),
            new VertexPositionTexture(new Vector3(-COORD, -COORD, .1f), new Vector2(0, 1)),
            new VertexPositionTexture(new Vector3(COORD, COORD, .1f), new Vector2(1, 0)),
        };

        public const string CUBE_STR = "cube";
        public const string SPHERE_STR = "sphere";
        static readonly (Shape, string)[] SHAPE_MODELS = new[]
        {
            (Shape.BOX, CUBE_STR),
            (Shape.CUBE, CUBE_STR),
            (Shape.SPHERE, SPHERE_STR)
        };

        // delegates and events
        public Func<KinectFrameData> GetKinectBuffers { get; set; }
        public Action<GameTime> UpdateComponents { get; set; }
        public Action<SceneDrawMode> DrawScene { get; set; }
        public Action<Effect,
            IReadOnlyDictionary<Shape, Model>,
            IReadOnlyDictionary<Color, BasicEffect>,
            IReadOnlyDictionary<Color, BasicEffect>> GraphicsInitialized;


        // public properties

        public GameMode GameMode { get; set; }
        
        public new GraphicsDevice GraphicsDevice
        {
            get
            {
                if (gDeviceMgr != null)
                    return gDeviceMgr.GraphicsDevice;
                else
                    return null;
            }
        }
        
        Effect sandboxEffect, depthMapEffect, hiwDepthEffect, segmentationEffect;
        Viewport viewport;
        RenderTarget2D depthRdrTgt;
        IGraphicsDeviceService gDeviceMgr;

        protected override void Initialize()
        {
            base.Initialize();
            gDeviceMgr = new WpfGraphicsDeviceService(this);
            
            var models = SHAPE_MODELS.MapToDictionary(Content.Load<Model>);

            //create effects
            var normalColorEffects = COLORS.MapToDictionary(c => CreateEffect(c.color, c.alpha));
            var flatColorEffects = COLORS.MapToDictionary(
                c => CreateEffect(c.color, c.alpha, enableLighting: false));
            
            sandboxEffect = Content.Load<Effect>("sandbox");
            depthMapEffect = Content.Load<Effect>("depth_map");
            hiwDepthEffect = Content.Load<Effect>("hiw_depth");
            segmentationEffect = Content.Load<Effect>("segmentation");
            
            Focus(); // todo check if needed
            GraphicsInitialized(depthMapEffect, models, normalColorEffects, flatColorEffects);
        }

        BasicEffect CreateEffect(XColor color, float alpha = 1f, bool enableLighting = true)
        {
            var effect = new BasicEffect(GraphicsDevice) { DiffuseColor = color.ToVector3(), Alpha = alpha };
            if (enableLighting)
                effect.EnableDefaultLighting();
            return effect;
        }

        public void UpdateViewport(Viewport vp, Texture2D colorMapper, Texture2D depthMapper)
        {
            viewport = vp;
            GraphicsDevice.Viewport = vp;
            depthRdrTgt = new RenderTarget2D(GraphicsDevice, vp.Width, vp.Height, false,
                SurfaceFormat.HalfSingle, DepthFormat.Depth24Stencil8);

            sandboxEffect.Parameters["virtDepthTex"].SetValue(depthRdrTgt);
            sandboxEffect.Parameters["colorMapper"].SetValue(colorMapper);
            sandboxEffect.Parameters["depthMapper"].SetValue(depthMapper);
            hiwDepthEffect.Parameters["depthMapper"].SetValue(depthMapper);
            segmentationEffect.Parameters["depthMapper"].SetValue(depthMapper);
        }

        public void UpdatePlane(Microsoft.Kinect.Vector4 planeEq) // for segmentation effect
        {
            //segmentationEffect.Parameters["plane"].SetValue(planeEq);
        }


        protected override void Update(GameTime gameTime)
        {
            UpdateComponents(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime time)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.Clear(XColor.CornflowerBlue);

            var kframes = GetKinectBuffers();
            if (GameMode == GameMode.SANDBOX)
            {
                var defaultRenderTarget = GraphicsDevice.GetRenderTargets()[0].RenderTarget;

                GraphicsDevice.SetRenderTarget(depthRdrTgt);
                GraphicsDevice.Clear(XColor.White); // set default depth to max
                GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                DrawScene(SceneDrawMode.VIRTUAL_DEPTH);

                GraphicsDevice.SetRenderTarget((RenderTarget2D)defaultRenderTarget);
                DrawScene(SceneDrawMode.SANDBOX_OPAQUE);

                lock (kframes.ColorTexture)
                    sandboxEffect.Parameters["realColorTex"].SetValue(kframes.ColorTexture);
                lock (kframes.DepthTexture)
                    sandboxEffect.Parameters["realDepthTex"].SetValue(kframes.DepthTexture);
                DrawEffect(sandboxEffect);
            }
            else if (GameMode == GameMode.HIW_DEPTH)
            {
                lock (kframes.DepthTexture)
                    hiwDepthEffect.Parameters["depthTex"].SetValue(kframes.DepthTexture);
                DrawEffect(hiwDepthEffect);
            }
            else if (GameMode == GameMode.HIW_SEGMENT)
            {
                lock (kframes.DepthTexture)
                    hiwDepthEffect.Parameters["depthTex"].SetValue(kframes.DepthTexture);
                //hiwDepthEffect.Parameters["planeEq"].SetValue();
                DrawEffect(hiwDepthEffect);
            }
            else if (GameMode == GameMode.HIW_PHYSICS)
            {
                GraphicsDevice.Clear(XColor.CornflowerBlue);

                DrawScene(SceneDrawMode.PHYSICS_OPAQUE);
                DrawScene(SceneDrawMode.PHYSICS_TRANSPARENT);
            }

            base.Draw(time);
        }

        void DrawEffect(Effect effect)
        {
            effect.Techniques[0].Passes[0].Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, screenQuad, 0, 2);
        }

    }
}
