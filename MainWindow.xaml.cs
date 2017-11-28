namespace InTime
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Windows.Controls;
    using LiveCharts;
    using LiveCharts.Defaults;
    using LiveCharts.Wpf;
    using System.ComponentModel;
    using System;
    using FFTLibrary;


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 8;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Red, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// The graphs to display the positions of the limbs
        /// </summary>
        public SeriesCollection LeftArmCollection { get; set; }
        public SeriesCollection RightArmCollection { get; set; }
        public SeriesCollection LeftLegCollection { get; set; }
        public SeriesCollection RightLegCollection { get; set; }

        private int NumberOfPoints = 300;
        private int RefreshRate = 30;

        private System.Collections.Generic.List<Peak>[] Peaks;
        private System.Collections.Generic.List<double>[] Points;
        private BPMCounter[] bpms;

        MediaPlayer[] players;
        int MediaPlayers = 10;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            Points = new System.Collections.Generic.List<double>[4];
            Peaks = new System.Collections.Generic.List<Peak>[4];
            for (int i = 0; i < Points.Length; i++)
            {
                Peaks[i] = new System.Collections.Generic.List<Peak>();
                Points[i] = new System.Collections.Generic.List<double>();
                for (int j = 0; j < NumberOfPoints; j++)
                {
                    Points[i].Add(0);
                }
            }
            InitGraphs();

            InitializeComponent();

            DataContext = this;

            bpms = new BPMCounter[] { new BPMCounter(rightText), new BPMCounter(leftText) };

            players = new MediaPlayer[MediaPlayers];

            for (int i = 0; i < MediaPlayers; i++) {
                players[i] = new MediaPlayer();
            }
        }
        private void InitGraphs()
        {
            LeftArmCollection = new SeriesCollection
            {
                new LineSeries { Values = new ChartValues<ObservableValue> { new ObservableValue(0) } }
            };

            LeftLegCollection = new SeriesCollection
            {
                new LineSeries { Values = new ChartValues<ObservableValue> { new ObservableValue(0) } }
            };

            RightArmCollection = new SeriesCollection
            {
                new LineSeries { Values = new ChartValues<ObservableValue> { new ObservableValue(0) } }
            };

            RightLegCollection = new SeriesCollection
            {
                new LineSeries { Values = new ChartValues<ObservableValue> { new ObservableValue(0) } }
            };

            for (int i = 0; i < NumberOfPoints/5 - 1; i++)
            {
                try
                {
                    RightArmCollection[0].Values.Add(new ObservableValue(0));
                    RightLegCollection[0].Values.Add(new ObservableValue(0));
                    LeftArmCollection[0].Values.Add(new ObservableValue(0));
                    LeftLegCollection[0].Values.Add(new ObservableValue(0));
                }
                catch (InvalidCastException)
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            imageSource = new DrawingImage(drawingGroup);

            // Display the drawing using our image control
            Image.Source = imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }
            }

            if (null != sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                sensor.SkeletonFrameReady += SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    sensor.Start();
                }
                catch (IOException)
                {
                    sensor = null;
                }
            }

            if (sensor == null)
            {
                statusText.Text = "No ready kinect found!";
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, CancelEventArgs e)
        {
            if (null != sensor)
            {
                sensor.Stop();
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        int counter = 0;

        private void Update(Skeleton skeleton)
        {
            for (int i = 0; i < 2; i++)
            {
                Boolean remove = false;
                foreach (Peak p in Peaks[i])
                {
                    p.timeStep();
                    if (p.getTimeStamp() < 0)
                    {
                        remove = true;
                        //Console.WriteLine(p.getTimeStamp());
                    }
                }
                if (remove)
                {
                    Peaks[i].RemoveAt(0);
                }
            }


            CalculateDist(skeleton);
            /*
            counter++;

            if (counter > 300)
            {
                Complex[] nums = Complex.DFT(Points[0].ToArray());
                PrintComplex(nums);
                counter = 0;
            }
            */

            UpdateGraphs();

            UpdatePeaks();

            CalculateBPM();
        }

        private void CalculateBPM() {
            for (int i = 0; i < 2; i++) {
                double pCounter = 0;
                foreach (Peak p in Peaks[i]) {
                    pCounter++;
                }
                double bpm = NumberOfPoints / RefreshRate; //number of seconds
                bpm = pCounter / bpm; //number of peaks a second
                bpm *= 60; //number of peaks a minute
                bpms[i].update(bpm);
            }

            if (IsCloseTo(bpms[0].getBPM(), bpms[1].getBPM())) {
                bpmCounterLabel.Text = "" + (bpms[0].getBPM() + bpms[1].getBPM()) / 2;
            }
            else {
                bpmCounterLabel.Text = "" + Math.Max(bpms[0].getBPM(), bpms[1].getBPM());
            }
        }

        private Boolean IsCloseTo(int num1, int num2) {
            int range = 10;
            return (num2 > num1 - range && num2 < num1 + range);
        }

        private void CalculateDist(Skeleton skeleton)
        {
            Points[0].Add(CalulateJointDist(JointType.WristRight, skeleton));
            Points[1].Add(CalulateJointDist(JointType.WristLeft, skeleton));
            Points[2].Add(CalulateJointDist(JointType.AnkleRight, skeleton));
            Points[3].Add(CalulateJointDist(JointType.AnkleLeft, skeleton));
            for (int i = 0; i < Points.Length; i++)
            {
                Points[i].RemoveAt(0);
            }
        }

        private void UpdatePeaks()
        {
            for (int i = 0; i < 2; i++)
            {
                double[] Values = Points[i].ToArray();
                //check if there is a peak
                //if on a downward slope, must have previously been a peak
                double point1 = Values[NumberOfPoints - 1];
                double point2 = Values[NumberOfPoints - 2];
                double point3 = Values[NumberOfPoints - 3];

                if (point1 < point2)
                {
                    //if the third value is less than the second the second must be a peak
                    //wooo magic number to avoid tremours
                    if (point3 < point2 && ((Peaks[i].Count == 0 && point2 > 0.1) || point2 > ((Peak)Peaks[i][Peaks[i].Count - 1]).getRDitch() + 0.2))
                    {
                        //first peak, so lDitch is 0
                        if (Peaks[i].Count == 0)
                        {
                            Peaks[i].Add(new Peak(0, point1, point2, NumberOfPoints));
                        }
                        else
                        {
                            //the lditch of the new peak is the rditch of the previous
                            Peaks[i].Add(new Peak(((Peak)Peaks[i][Peaks[i].Count - 1]).getRDitch(), point1, point2, NumberOfPoints));
                        }
                        /*
                        var p1 = new MediaPlayer();
                        p1.Open(new Uri(@"C:\Users\Peran\Coding\InTime\Images\hi-hat.wav"));
                        p1.Play(); 
                        Console.WriteLine("Left arm peak! Height: " + ((Peak)Peaks[i][Peaks[i].Count - 1]).getSize());
                        */
                    }
                    //if not, the last peak is more pronouced
                    else
                    {
                        if (Peaks[i].Count > 0)
                        {
                            ((Peak)Peaks[i][Peaks[i].Count - 1]).setRDitch(point1);
                        }
                    }
                }
            }
        }

        private void UpdateGraphs()
        {
            int lastItem = NumberOfPoints - 1;
            RightArmCollection[0].Values.Add(new ObservableValue(Points[0][lastItem]));
            RightArmCollection[0].Values.RemoveAt(0);
            LeftArmCollection[0].Values.Add(new ObservableValue(Points[1][lastItem]));
            LeftArmCollection[0].Values.RemoveAt(0);
            RightLegCollection[0].Values.Add(new ObservableValue(Points[2][lastItem]));
            RightLegCollection[0].Values.RemoveAt(0);
            LeftLegCollection[0].Values.Add(new ObservableValue(Points[3][lastItem]));
            LeftLegCollection[0].Values.RemoveAt(0);
        }

        private double CalulateJointDist(JointType jointType, Skeleton skeleton)
        {
            Joint joint0 = skeleton.Joints[jointType];
            Joint joint1 = skeleton.Joints[JointType.Spine];

            double dist = Math.Abs(joint0.Position.X - joint1.Position.X) + Math.Abs(joint0.Position.Y - joint1.Position.Y) + Math.Abs(joint0.Position.Z - joint1.Position.Z);
            return dist;
        }
        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            Update(skel);
                            DrawBonesAndJoints(skel, dc);
                            break;
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            centerPointBrush,
                            null,
                            SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            //Console.WriteLine(jointType0 + ": (" + joint0.Position.X + ", " + joint0.Position.Y + ", " + joint0.Position.Z + ")");

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, SkeletonPointToScreen(joint0.Position), SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            double[] array = new double[] { 0, 0, 0, 0, 1, 0, 0, 0, 0 };
            Complex[] nums1 = Complex.DFT(array);
            PrintComplex(nums1);
            AForge.Math.Complex[] nums = new AForge.Math.Complex[] { new AForge.Math.Complex(0, 0), new AForge.Math.Complex(0, 0), new AForge.Math.Complex(0, 0), new AForge.Math.Complex(2, 0), new AForge.Math.Complex(1, 0) };
            AForge.Math.FourierTransform.DFT(nums, AForge.Math.FourierTransform.Direction.Forward);
            PrintComplex(nums);

            //PrintComplex(nums);
            PlayHihat();

            if (sensor != null)
            {
                if (checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

        private void PrintComplex(Complex[] nums)
        {
            String s = "[";

            for (int i = 0; i < nums.Length; i++)
            {
                if (i != 0) s += ", ";
                var num = Math.Sqrt(nums[i].re * nums[i].re + nums[i].im * nums[i].im);
                if (num < 0.01) num = 0;
                s += num;

                LeftLegCollection[0].Values.RemoveAt(0);
                LeftLegCollection[0].Values.Add(new ObservableValue(num));
            }

            s += "]";
            Console.WriteLine(s);
        }

        private void PrintComplex(AForge.Math.Complex[] nums)
        {
            String s = "[";

            for (int i = 0; i < nums.Length; i++)
            {
                if (i != 0) s += ", ";
                var num = Math.Sqrt(nums[i].Re * nums[i].Re + nums[i].Im * nums[i].Im);
                if (num < 0.01) num = 0;
                s += num;

                LeftLegCollection[0].Values.RemoveAt(0);
                LeftLegCollection[0].Values.Add(new ObservableValue(num));
            }

            s += "]";
            Console.WriteLine(s);

        }


        public void PlayHihat()
        {
            if (MediaPlayers > players.Length - 1) {
                MediaPlayers = 0;
            }
            //players[MediaPlayers] = new MediaPlayer();
            players[MediaPlayers].Open(new Uri(@"C:\Users\Peran\Coding\InTime\Images\hi-hat.wav"));
            players[MediaPlayers].Play();
            MediaPlayers++;
        }
        public void PlayKick()
        {
            if (MediaPlayers > players.Length - 1)
            {
                MediaPlayers = 0;
            }
            players[MediaPlayers].Open(new Uri(@"C:\Users\Peran\Coding\InTime\Images\kick.wav"));
            players[MediaPlayers].Play();
            MediaPlayers++;
        }

    }

}