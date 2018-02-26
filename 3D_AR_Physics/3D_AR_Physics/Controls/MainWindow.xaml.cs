using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Diagnostics;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.Interaction;
using XMatrix = Microsoft.Xna.Framework.Matrix;

namespace AR_Physics
{
	public enum DrawMode
	{
		Sandbox,
		DebugDepth,
		DebugDetect,
		DebugPhysics
	}

	public partial class MainWindow : Window
	{
		public KinectSensorChooser sensor = new KinectSensorChooser();
		public InteractionStream Interaction;
		public TimeSpan Elapsed;     //tempo trascorso dall'ultimo ciclo della pipeline
		public GraphicsControl GCtrl;//componente grafico
		public DrawMode Mode = DrawMode.Sandbox;
		public bool IsRunning;
		public bool IsPeeked;
		public bool IsInMenu;
		public bool GotSkeleton, GotDepth;
		public bool showStats;
		public bool floorMode;// false->tavolo  true->pavimento
		public Physics World;          //scena virtuale
		public XMatrix View;
		public XMatrix Projection = new XMatrix();
		public DebugEffect DebugEff;

		DispatcherTimer timer;
		ThicknessAnimation bounceAnim = new ThicknessAnimation() { EasingFunction = new BounceEase() };
		ThicknessAnimation peekAnim = new ThicknessAnimation()
		{
			EasingFunction = new QuinticEase(),
			Duration = new Duration(new TimeSpan(0, 0, 0, 0, 500))
		};
		DoubleAnimation visibility = new DoubleAnimation() { Duration = new Duration(new TimeSpan(0, 0, 0, 0, 500)) };
		ThicknessAnimation pressAnim = new ThicknessAnimation();
		public bool mustCalib;
		public MouseButtonEventArgs MouseEvents;
		public bool Creating;

		public MainWindow()//Inizializzazione
		{
			InitializeComponent();
			sensor.KinectChanged += new EventHandler<KinectChangedEventArgs>(sensor_KinectChanged);
			sensorChooserUi.KinectSensorChooser = sensor;
			sensor.Start();
			GCtrl = new GraphicsControl(this);
			DebugEff = new DebugEffect(this);
			World = new Physics(this);
			World.Initilize();

			#region Layout
			MouseEvents = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left);
			MouseEvents.RoutedEvent = Mouse.MouseDownEvent;
			Top = 0;
			Left = 0;
			Width = SystemParameters.PrimaryScreenWidth;
			Height = SystemParameters.PrimaryScreenHeight;
			menu.Width = Width;
			menu.Height = Height;
			menu.Margin = new Thickness(0);
			message.Margin = new Thickness(0);
			gameView.Margin = new Thickness(0);
			howMadeView.Margin = new Thickness(0);
			message.Width = Width;
			message.Height = Height;
			message.Visibility = Visibility.Hidden;
			howMadeView.Visibility = Visibility.Hidden;
			Show();
			GCtrl.Show();
			gameView.mainGrid.ColumnDefinitions[1].Width = new GridLength(Height * 16 / 15);//  5/4 * 480 : 640 -> 600 : 640 -> 15/16
			howMadeView.mainGrid.ColumnDefinitions[1].Width = new GridLength(Height * 16 / 15);
			GCtrl.Size = new System.Drawing.Size((int)gameView.mainGrid.ColumnDefinitions[1].Width.Value, (int)Height * 4 / 5);
			GCtrl.Location = new System.Drawing.Point((int)(Width - gameView.mainGrid.ColumnDefinitions[1].Width.Value) / 2, 0);

			Focus();
			timer = new DispatcherTimer(new TimeSpan(0, 0, 0, 0, 50), DispatcherPriority.Normal, timer_Tick, Dispatcher.CurrentDispatcher);
			bounceAnim.Completed += new EventHandler(bounceAnim_Completed);
			peekAnim.Completed += new EventHandler(peekAnim_Completed);
			visibility.Completed += new EventHandler(visibility_Completed);
			menu.buttonHowMade.Click += new RoutedEventHandler(buttonHowMade_Click);
			menu.buttonSandbox.Click += new RoutedEventHandler(buttonSandbox_Click);
			message.messageButton.Click += new RoutedEventHandler(messageButton_Click);
			menu.planeSwitch.Click += new RoutedEventHandler(planeSwitch_Click);
			#endregion
		}

		void buttonSandbox_Click(object sender, RoutedEventArgs e)
		{
			mustCalib = true;
			Mode = DrawMode.Sandbox;
			MenuUp();
		}

		private void planeSwitch_Click(object sender, RoutedEventArgs e)
		{
			if (floorMode)
			{
				menu.planeText.Text = "Modalità tavolo";
			}
			else
			{
				menu.planeText.Text = "Modalità pavimento";
			}
			floorMode = !floorMode;
		}

		void visibility_Completed(object sender, EventArgs e)
		{
			message.Opacity = message.Opacity;
			message.ApplyAnimationClock(Message.OpacityProperty, null);
			if (message.Opacity == 0)
			{
				IsRunning = true;
				mustCalib = true;
				message.Visibility = Visibility.Hidden;
				message.kinectRegion.KinectSensor = null;
			}
		}

		void MsgFadeIn()
		{
			message.kinectRegion.KinectSensor = sensor.Kinect;
			IsRunning = false;
			mustCalib = false;
			visibility.From = 0;
			visibility.To = 1;
			message.BeginAnimation(Message.OpacityProperty, visibility);
			message.Visibility = Visibility.Visible;
		}

		void MsgFadeOut()
		{
			visibility.From = 1;
			visibility.To = 0;
			message.BeginAnimation(Message.OpacityProperty, visibility);
		}

		void messageButton_Click(object sender, RoutedEventArgs e)
		{
			MsgFadeOut();
		}


		void peekAnim_Completed(object sender, EventArgs e)
		{
			menu.Margin = menu.Margin;
			menu.ApplyAnimationClock(Menu.MarginProperty, null);
		}

		void bounceAnim_Completed(object sender, EventArgs e)
		{
			menu.Margin = menu.Margin;
			if (menu.Margin == new Thickness())
			{
				World.Reset();
			}
			else
			{
				menu.kinectRegion.KinectSensor = null;
				IsRunning = true;
			}
			menu.ApplyAnimationClock(Menu.MarginProperty, null);
		}

		public void PeekIn()
		{
			peekAnim.From = new Thickness(0, -Height, 0, Height);
			peekAnim.To = new Thickness(0, Height / 24 - Height, 0, Height - Height / 24);             //  30 : (480 * 5 / 4) = x : Height

			menu.BeginAnimation(Menu.MarginProperty, peekAnim);
		}

		public void PeekOut()
		{
			peekAnim.From = menu.Margin;
			peekAnim.To = new Thickness(0, -Height, 0, Height);
			menu.BeginAnimation(Menu.MarginProperty, peekAnim);
		}

		void buttonHowMade_Click(object sender, RoutedEventArgs e)
		{
			mustCalib = true;
			Mode = DrawMode.DebugDepth;
			MenuUp();
		}

		public void MenuDown()
		{
			menu.kinectRegion.KinectSensor = sensor.Kinect;
			bounceAnim.From = menu.Margin;
			bounceAnim.To = new Thickness(0);
			menu.BeginAnimation(Menu.MarginProperty, bounceAnim);
			IsRunning = false;
		}

		public void DebugChanged()
		{
			if (Mode == DrawMode.DebugDepth)
			{
				howMadeView.description.Text = "Questa è una depth map. Le sfumature tendenti al bianco segnalano punti vicino al sensore, quelle tendenti al nero, punti più lontani.";
			}
			else if (Mode == DrawMode.DebugDetect)
			{
				howMadeView.description.Text = "Con degli algoritmi è possibile trovare il piano del tavolo (rosso) e gli utenti (blu). I punti analizzati rimanenti sono colorati in verde.";
			}
			else if (Mode == DrawMode.DebugPhysics)
			{
				howMadeView.description.Text = "Per far interagire realtà virtuale con quella reale vengono sostituiti alcuni punti di quest'ultima con delle sfere cinematiche.";
			}
		}

		public void MenuUp()
		{
			if (sensor.Kinect != null)
			{
				bounceAnim.From = menu.Margin;
				bounceAnim.To = new Thickness(0, -Height, 0, Height);
				menu.BeginAnimation(Menu.MarginProperty, bounceAnim);
				if (!IsRunning)
				{
					IsRunning = true;
					GCtrl.Step();
					IsRunning = false;
					if (Mode == DrawMode.Sandbox)
					{
						gameView.Visibility = Visibility.Visible;
						howMadeView.Visibility = Visibility.Hidden;
					}
					else
					{
						gameView.Visibility = Visibility.Hidden;
						howMadeView.Visibility = Visibility.Visible;
						DebugChanged();
					}
				}
			}
		}

		void timer_Tick(object sender, EventArgs e)
		{
			if (IsRunning)
			{
				GCtrl.Invalidate();
			}
		}

		public void SetMenuLevel(int level)
		{
			level = (int)(level * Height / 600 - Height);
			menu.Margin = new Thickness(0, level, 0, -level);
		}

		void sensor_KinectChanged(object sender, KinectChangedEventArgs e)//Inizializzazione del sensore Kinect e collegamento al delegato per l'esecuzione della pipeline
		{
			if (e.NewSensor != null)
			{
				e.NewSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
				e.NewSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
				e.NewSensor.SkeletonStream.Enable(new TransformSmoothParameters
				{
					JitterRadius = 0.3f,
					MaxDeviationRadius = 0.4f,
					Smoothing = 0.1f,
					Correction = 0f
				});
				e.NewSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
				e.NewSensor.DepthFrameReady += new EventHandler<DepthImageFrameReadyEventArgs>(NewSensor_DepthFrameReady);
				e.NewSensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(NewSensor_SkeletonFrameReady);
				menu.kinectRegion.KinectSensor = e.NewSensor;
				message.kinectRegion.KinectSensor = e.NewSensor;
				gameView.red.Margin = new Thickness(20);

				Interaction = new InteractionStream(e.NewSensor, new DummyInteractionClient());
				GCtrl.F = e.NewSensor.DepthStream.NominalFocalLengthInPixels;
				View = XMatrix.Identity;
				Projection = XMatrix.CreatePerspectiveFieldOfView(e.NewSensor.DepthStream.NominalVerticalFieldOfView / 2 * (float)Math.PI / 180, 640f / 480, 0.1f, 40f);// massimo 5m di distanza
				//e.NewSensor.ElevationAngle = -9;
			}
		}

		void NewSensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
		{
			//Ottiene i dati dello scheletro e li elabora
			if (IsRunning && !GotSkeleton)
			{
				using (SkeletonFrame skeletonF = e.OpenSkeletonFrame())
				{
					if (skeletonF != null)
					{
						skeletonF.CopySkeletonDataTo(GCtrl.Skeletons);
						if (mustCalib)
						{
							if (floorMode)
							{
								GCtrl.uTracked = true;
							}
							else
							{
								if (GCtrl.uTracked == false)
								{
									GCtrl.uTracked = false;
									float nearestP = 10000; // 10 m -> valore di reset elevato
									for (int i = 0; i < 6; i++)
									{
										if (GCtrl.Skeletons[i] != null)
										{
											if (GCtrl.Skeletons[i].TrackingState == SkeletonTrackingState.Tracked && GCtrl.Skeletons[i].Position.Z < nearestP)
											{
												GCtrl.UserN = i;
												GCtrl.uTracked = true;
												break;
											}
										}
									}
									if (!GCtrl.uTracked)
									{
										message.msgDesc.Text = "Calibrazione non riuscita:\nNessun utente rilevato\nPremi per riprovare";
										MsgFadeIn();
									}
								}
							}

						}
						Interaction.ProcessSkeleton(GCtrl.Skeletons, GCtrl.Accel, skeletonF.Timestamp);
						GotSkeleton = true;
					}
				}
			}
		}

		void NewSensor_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
		{
			//Ottiene la depth-map reale e la elabora
			if (IsRunning && !GotDepth)
			{
				using (DepthImageFrame depthF = e.OpenDepthImageFrame())
				{
					if (depthF != null)
					{
						depthF.CopyDepthImagePixelDataTo(GCtrl.realD);
						Interaction.ProcessDepth(depthF.GetRawPixelData(), depthF.Timestamp);
						if (mustCalib)
						{
							if (GCtrl.uTracked)
							{
								string result = GCtrl.Calibrate();
								if (result != null)
								{
									message.msgDesc.Text = "Calibrazione non riuscita:\n" + result + "\nPremi per riprovare";
									MsgFadeIn();
								}
								mustCalib = false;
							}
						}
						GotDepth = true;
					}
				}
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			GCtrl.Dispose();
		}

		private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				if (IsRunning)
				{
					MenuDown();
				}
			}
			else if (e.Key == Key.F5)
			{
				showStats = !showStats;
			}
			else if (showStats && e.Key == Key.C)
			{
				mustCalib = true;
			}
		}

		private void Window_GotFocus(object sender, RoutedEventArgs e)
		{
			GCtrl.Focus();
			this.Focus();
		}
	}

	public class DummyInteractionClient : IInteractionClient
	{
		InteractionInfo result = new InteractionInfo() { IsGripTarget = true, PressTargetControlId = 1 };
		public InteractionInfo GetInteractionInfoAtLocation(int skeletonTrackingId, InteractionHandType handType, double x, double y)
		{
			return result;
		}
	}
}
