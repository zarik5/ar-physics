using ArPhysics2.Properties;
using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace ArPhysics2
{
    public partial class MainWindow : Window
    {
        static readonly Shape[] SELECTABLE_SHAPES = new[] { Shape.RANDOM, Shape.CUBE, Shape.SPHERE, Shape.BOX }; // NONE is placeholder for random
        static readonly Color[] SELECTABLE_COLORS = new[] { Color.RANDOM, Color.RED, Color.ORANGE, Color.YELLOW, Color.GREEN, Color.BLUE, Color.PURPLE };
        static readonly GameMode[] HIW_MODES = new[] { GameMode.HIW_PHYSICS, GameMode.HIW_DEPTH };
        static readonly GameMode[] ACTIVE_GAME_MODES = new[] { GameMode.SANDBOX, GameMode.HIW_PHYSICS, GameMode.HIW_DEPTH };

        // Main logic:
        KinectManager kinectMgr;
        VirtualScene scene;
        InteractionManager interaction;

        GameMode gameMode = GameMode.NONE;

        bool floorModeEnabled = true;
        bool forcePlaneMode = false;
        bool menuShown = true;

        bool menuGrabbing = false;
        ulong menuInterUserId = 0;
        JointType menuInterHand = JointType.HandLeft;

        int hiwModeIdx = 0;
        int selectedShapeIdx = 0;
        int selectedColorIdx = 0;

        bool validMappers = false;

        public MainWindow()
        {

            InitializeComponent();
            kinectMgr = new KinectManager();
            scene = new VirtualScene();
            interaction = new InteractionManager();

            ApplyUserProps();


            //set events and callbacks
            graphics.GraphicsInitialized = GraphicsInitialized;
            graphics.GetKinectBuffers = () => kinectMgr.CurrentFrames;
            graphics.UpdateComponents = (time) =>
            {
                var bodies = kinectMgr.CurrentFrames.Bodies;
                if (gameMode != GameMode.NONE)
                {
                    if (menuGrabbing)
                    {
                        var topMargin = GetInterHandYViewboxSpace() - viewBoxContent.ActualHeight;
                        menu.Margin = new Thickness(0, topMargin, 0, -topMargin);
                    }
                    scene.Update(time, kinectMgr.CurrentFrames.DepthSpheresCenters, bodies);
                }
                interaction.Update(bodies);
            };
            graphics.DrawScene = scene.Draw;
            menu.sandboxBtn.Click += ButtonClick;
            menu.HowItWorksBtn.Click += ButtonClick;
            menuAnim.Completed += MenuAnimCompleted;

            gameView.ShapeSelected = () =>
            {
                selectedShapeIdx = (selectedShapeIdx + 1) % SELECTABLE_SHAPES.Length;
                gameView.SetShapeIcon(SELECTABLE_SHAPES[selectedShapeIdx]);
            };
            gameView.ColorSelected = () =>
            {
                selectedColorIdx = (selectedColorIdx + 1) % SELECTABLE_COLORS.Length;
                gameView.SetColor(SELECTABLE_COLORS[selectedColorIdx]);
            };
            howItWorksView.BackSelected = () =>
            {
                hiwModeIdx = (hiwModeIdx - 1).Mod(HIW_MODES.Length);
                SetGameMode(HIW_MODES[hiwModeIdx]);
            };
            howItWorksView.NextSelected = () =>
            {
                hiwModeIdx = (hiwModeIdx + 1).Mod(HIW_MODES.Length);
                SetGameMode(HIW_MODES[hiwModeIdx]);
            };
        }

        void ApplyUserProps()
        {
            var props = Settings.Default;
            interaction.AutoReset = props.AutoGameModeChange;
        }

        void GraphicsInitialized(Effect depthEffect,
            IReadOnlyDictionary<Shape, Model> models,
            IReadOnlyDictionary<Color, BasicEffect> normalColorEffects,
            IReadOnlyDictionary<Color, BasicEffect> flatColorEffects)
        {
            kinectMgr.Initialize(graphics.GraphicsDevice);
            scene.Initialize(depthEffect, kinectMgr.DepthSpheresNumber, kinectMgr.DepthSpheresSpacingAt1M,
                models, normalColorEffects, flatColorEffects);
            interaction.Initialize(ProcessInteraction);

            SetGameMode(GameMode.NONE);
            UpdateViewport();
        }

        double GetInterHandYViewboxSpace()
        {
            var handScreenSpace = interaction.CameraPointToScreen(
                kinectMgr.CurrentFrames.Bodies[menuInterUserId].Joints[menuInterHand].Position);
            return handScreenSpace.Y != float.NaN ?
                handScreenSpace.Y * viewBoxContent.ActualHeight / windowContent.ActualHeight : 0;
        }

        void ProcessInteraction(ulong userId, InteractionType interType, JointType hand)
        {
            var handStr = hand == JointType.HandLeft ? " LEFT  " : (hand == JointType.HandRight ? " RIGHT " : "       ");
            Debug.WriteLine(userId + handStr + interType.ToString());
            switch (interType)
            {
                case InteractionType.GRAB:
                    scene.SetGrabState(userId, hand, enabled: true);
                    break;
                case InteractionType.RELEASE:
                    scene.SetGrabState(userId, hand, enabled: false);
                    break;
                case InteractionType.BEGIN_DRAW:
                    scene.SetDrawingState(userId, hand, enabled: true, SELECTABLE_COLORS[selectedColorIdx]);
                    break;
                case InteractionType.END_DRAW:
                    scene.SetDrawingState(userId, hand, enabled: false);
                    break;
                case InteractionType.BEGIN_CREATING_OBJECT:
                    var shape = gameMode == GameMode.SANDBOX ? SELECTABLE_SHAPES[selectedShapeIdx] : Shape.RANDOM;
                    var color = gameMode == GameMode.SANDBOX ? SELECTABLE_COLORS[selectedColorIdx] : Color.RANDOM;
                    scene.BeginCreatingObject(userId, shape, color);
                    break;
                case InteractionType.FINISH_CREATING_OBJECT:
                    scene.FinishCreatingObject(userId);
                    break;
                case InteractionType.PEEK_MENU:
                    menuInterUserId = userId;
                    menuInterHand = hand;
                    PeekMenu();
                    break;
                case InteractionType.CANCEL_PEEK_MENU:
                    CancelPeekMenu();
                    break;
                case InteractionType.GRAB_MENU:
                    menuInterHand = hand;
                    menuGrabbing = true;
                    break;
                case InteractionType.RELEASE_MENU_SHOW:
                    menuGrabbing = false;
                    ShowMenu();
                    break;
                case InteractionType.RELEASE_MENU_CANCEL:
                    menuGrabbing = false;
                    HideMenu();
                    break;
                case InteractionType.BEGIN_REACH_MARGIN:
                    if (gameMode == GameMode.SANDBOX)
                        gameView.Button_MouseDown(hand == JointType.HandLeft
                            ? gameView.shapeBtn : gameView.colorBtn, null);
                    else
                        howItWorksView.Button_MouseDown(hand == JointType.HandLeft
                            ? howItWorksView.backBtn : howItWorksView.nextBtn, null);
                    break;
                case InteractionType.END_REACH_MARGIN:
                    if (gameMode == GameMode.SANDBOX)
                        gameView.Button_MouseUp(hand == JointType.HandLeft
                            ? gameView.shapeBtn : gameView.colorBtn, null);
                    else
                        howItWorksView.Button_MouseUp(hand == JointType.HandLeft
                            ? howItWorksView.backBtn : howItWorksView.nextBtn, null);
                    break;
                case InteractionType.IDLE_TIMEOUT:
                    if (menuShown)
                    {
                        SetGameMode(ACTIVE_GAME_MODES.Rnd());
                        HideMenu();
                    }
                    else
                    {
                        ShowMenu();
                    }
                    break;
                default:
                    break;
            }
        }

        void Calibrate()
        {
            kinectMgr.Calibrate((planeHeight, planeMargin, viewMatrix) =>
            {
                if (planeHeight == 0 && planeMargin == 0)
                {
                    // show calibration failed
                }
                else
                {
                    if (forcePlaneMode && floorModeEnabled && planeHeight < 1)
                    {
                        // show "remove table"
                    }
                    else if (forcePlaneMode && !floorModeEnabled && planeHeight > 1)
                    {
                        // show add table
                    }
                }
                scene.UpdatePlane(planeHeight, planeMargin);//, forcePlaneMode ? !floorModeEnabled : (planeHeight < 1));
                scene.View = viewMatrix;
            });
        }

        void SetGameMode(GameMode mode)
        {
            gameMode = mode;
            selectedColorIdx = 0;
            selectedShapeIdx = 0;
            gameView.SetShapeIcon(SELECTABLE_SHAPES[selectedShapeIdx]);
            gameView.SetColor(SELECTABLE_COLORS[selectedColorIdx]);

            gameView.Visibility = mode == GameMode.SANDBOX ? Visibility.Visible : Visibility.Hidden;
            howItWorksView.Visibility = mode == GameMode.SANDBOX ? Visibility.Hidden : Visibility.Visible;

            graphics.GameMode = mode;
            kinectMgr.SetGameMode(mode);
            scene.SetGameMode(mode);
            interaction.SetGameMode(mode);
            howItWorksView.SetGameMode(mode);
            menuGrabbing = false;
            
            UpdateViewport();
        }

        void UpdateViewport()
        {
            //graphics control would create a wrong viewport because it is inside a Viewbox.
            double ctrlRatio = viewBoxContent.ActualWidth / viewBoxContent.ActualHeight;
            double winRatio = windowContent.ActualWidth / windowContent.ActualHeight;
            double vpWidth, vpHeight;
            if (ctrlRatio > winRatio)
            {
                vpWidth = windowContent.ActualWidth;
                vpHeight = windowContent.ActualWidth / ctrlRatio;
            }
            else
            {
                vpWidth = windowContent.ActualHeight * ctrlRatio;
                vpHeight = windowContent.ActualHeight;
            }
            Viewport vp = new Viewport(0, 0, (int)vpWidth, (int)vpHeight);
            Matrix proj = Matrix.CreatePerspectiveFieldOfView(GraphicsControl.FOV,
                 (float)ctrlRatio, GraphicsControl.NEAR_PLANE, GraphicsControl.FAR_PLANE);

            if (!validMappers)
            {
                var (colorMapperTex, depthMapperTex, depthMapperPts, success) =
                    kinectMgr.CreateMapperTextures(proj, vp);
                validMappers = success;

                graphics.UpdateViewport(vp, colorMapperTex, depthMapperTex);
            }

            graphics.Width = vpWidth;
            graphics.Height = vpHeight;

            scene.Projection = proj;
            //scene.UpdateDepthMapper(depthMapperPts);

            double viewBoxToScreen = vpWidth / viewBoxContent.ActualWidth;
            double gameSpaceMargin =
                (vpWidth - gameView.grid.ColumnDefinitions[1].ActualWidth * viewBoxToScreen) / 2;

            interaction.UpdateViewport(vp, proj,
                new Rectangle((int)gameSpaceMargin, 0, (int)(vpWidth - gameSpaceMargin * 2), (int)vpHeight));
        }


        #region Window logic

        ThicknessAnimation menuAnim = new ThicknessAnimation();
        DoubleAnimation visibility = new DoubleAnimation()
        {
            Duration = new Duration(new TimeSpan(0, 0, 0, 0, milliseconds: 500))
        };

        void ButtonClick(object sender, RoutedEventArgs e)
        {
            SetGameMode(sender == menu.sandboxBtn ? GameMode.SANDBOX : GameMode.HIW_PHYSICS);
            HideMenu();
            Calibrate();
        }

        void PeekMenu()
        {
            BeginMenuAnim(topMargin: GetInterHandYViewboxSpace() - viewBoxContent.ActualHeight,
                ease: new QuinticEase(), durationMs: 700);
        }

        void CancelPeekMenu()
        {
            BeginMenuAnim(topMargin: -viewBoxContent.ActualHeight, ease: new QuinticEase(), durationMs: 700);
        }

        void HideMenu()
        {
            menuShown = false;
            hiwModeIdx = 0;
            BeginMenuAnim(topMargin: -viewBoxContent.ActualHeight, ease: new BounceEase(), durationMs: 1500);
        }

        void ShowMenu()
        {
            menuShown = true;
            BeginMenuAnim(topMargin: 0, ease: new BounceEase(), durationMs: 1500);
            menu.kinectRegion.IsEnabled = true;
            menu.kinectRegion.Visibility = Visibility.Visible;
            menu.peekSuggestion.Visibility = Visibility.Hidden;
        }

        void BeginMenuAnim(double topMargin, IEasingFunction ease, int durationMs)
        {
            menuAnim.From = menu.Margin;
            menuAnim.To = new Thickness(0, topMargin, 0, -topMargin);
            menuAnim.Duration = new Duration(new TimeSpan(0, 0, 0, 0, durationMs));
            menuAnim.EasingFunction = ease;
            menu.BeginAnimation(MarginProperty, menuAnim);
        }

        private void MenuAnimCompleted(object sender, EventArgs e)
        {
            //consolidate margin value and allow manual setting:
            var finalMargin = menu.Margin;
            menu.BeginAnimation(MarginProperty, null);
            menu.Margin = finalMargin;

            if (menuShown)
            {
                SetGameMode(GameMode.NONE);
                interaction.Reset();

                //glitch fix: menu not dropping completely on certain conditions(multi - user interaction)
                if (menu.Margin != new Thickness(0))
                    ShowMenu();
            }
            else
            {
                menu.kinectRegion.IsEnabled = false;
                menu.kinectRegion.Visibility = Visibility.Hidden;
                menu.peekSuggestion.Visibility = Visibility.Visible;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var props = Settings.Default;
            if (e.Key == Key.Escape)
            {
                ShowMenu();
            }
            else if (e.Key == Key.R)
            {
                UpdateViewport();
            }
            else if (e.Key == Key.A)
            {
                props.AutoGameModeChange = true;
                interaction.AutoReset = true;
            }
            else if (e.Key == Key.M)
            {
                props.AutoGameModeChange = false;
                interaction.AutoReset = false;
            }
            else if (e.Key == Key.C)
            {
                Calibrate();
            }
            props.Save();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            kinectMgr.Dispose();
            scene.Dispose();
            kinectMgr.Dispose();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (graphics.GraphicsDevice != null)
            {
                UpdateViewport();
            }
        }
        #endregion
    }
}