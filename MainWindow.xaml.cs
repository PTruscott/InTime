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
    using System.Threading.Tasks;
    using System.Runtime.InteropServices;
    using System.Collections.Generic;

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

        /// <summary>
        /// the number of points the graphs display
        /// </summary>
        private int NumberOfPoints = 300;

        /// <summary>
        /// The refresh rate of the camera
        /// </summary>
        private int RefreshRate = 30;

        private List<double>[] Points;
        /// <summary>
        /// the bpm counter
        /// </summary>
        private BPMCounter bpm;

        MediaPlayer[] players;
        /// <summary>
        /// list of the peaks of the left arm to calculate bpm with if needed
        /// </summary>
        private List<Peak> leftArmPeaks;
        int MediaPlayers = 10;

        private Boolean isCalculatingBPM = false;

        /// <summary>
        /// Variables for calculating if a leg had been stomped before
        /// </summary>
        bool leftLegWasRaised = false;
        bool rightLegWasRaised = false;

        /// <summary>
        /// a series of variables to record loops
        /// </summary>
        int notesToRecord = 0;
        int maxRecordedNotes = 16;
        private System.Collections.Generic.List<System.Collections.Generic.List<int[]>> notes;
        int syncCount = 0;

        private delegate void MidiCallBack(int handle, int msg,
           int instance, int param1, int param2);

        [DllImport("winmm.dll")]
        private static extern int midiOutOpen(ref int handle,
           int deviceID, MidiCallBack proc, int instance, int flags);

        [DllImport("winmm.dll")]
        private static extern int midiOutShortMsg(int handle,
           int message);

        int handle = 0;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            midiOutOpen(ref handle, 0, null, 0, 0);
            Points = new System.Collections.Generic.List<double>[4];
            notes = new System.Collections.Generic.List<System.Collections.Generic.List<int[]>>();
            leftArmPeaks = new System.Collections.Generic.List<Peak>();
            for (int i = 0; i < Points.Length; i++)
            {
                Points[i] = new System.Collections.Generic.List<double>();
                for (int j = 0; j < NumberOfPoints; j++)
                {
                    Points[i].Add(0);
                }
            }
            InitGraphs();

            InitializeComponent();

            DataContext = this;

            bpm = new BPMCounter(leftText);

            players = new MediaPlayer[MediaPlayers];

            for (int i = 0; i < MediaPlayers; i++) {
                players[i] = new MediaPlayer();
            }
        }

        private void PlayNote(int handle, int vel, int note, int instrument) {
            //midiOutOpen(ref handle, 0, null, 0, 0);
            //converts the user input to hex
            string velHex = vel.ToString("X");
            string noteHex = note.ToString("X");
            string insHex = instrument.ToString();
            //builds into a hex string
            string s = string.Format("0x00{0}{1}{2}", velHex, noteHex, insHex);
            //converts to an integer
            int value = (int)new Int32Converter().ConvertFromString(s);
            //plays the note
            midiOutShortMsg(handle, value);
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

        private void Update(Skeleton skeleton)
        {
            CalculateDist(skeleton);

            UpdateGraphs();

            foreach (Peak p in leftArmPeaks)
            {
                p.timeStep();
            }

            if (LegStomp(true, skeleton))
            {
                if (isCalculatingBPM) bpmCalculatingLabel.Text = "BPM";
                else bpmCalculatingLabel.Text = "BPM (Calculating...)";
                isCalculatingBPM = !isCalculatingBPM;
                Console.WriteLine("left leg stomp");
            }
            else if (LegStomp(false, skeleton)) {
                Console.WriteLine("Right leg stomp");
                //if currently recording and new record message comes in then wipe and start again
                if (isRecording()) notes.RemoveAt(notes.Count - 1);
                notes.Add(new List<int[]>());
                notesToRecord = maxRecordedNotes+1;
            }

            if (isCalculatingBPM)
            {
                UpdatePeaks();

                CalculateBPM();
            }
            else
            {
                double armLength = CalculateArmLength(skeleton);

                int armHeight = CalculateJointHeight(skeleton.Joints[JointType.WristLeft], skeleton.Joints[JointType.ShoulderLeft], armLength);

                PlayNote(handle, 30, armHeight, 91);
                if (isRecording())
                {
                    notes[notes.Count - 1].Add(new int[2] { armHeight, 91 });
                }
                if (notes.Count > 0) {
                    if (notesToRecord == 0)
                    {
                        foreach (List<int[]> noteList in notes)
                        {
                            playRecordedNote(handle, noteList, syncCount);
                        }
                    }
                    else {
                        for (int i = 0; i < notes.Count - 2; i++) {
                            playRecordedNote(handle, notes[i], syncCount);
                        }
                    }
                   
                    syncCount++;
                    if (syncCount > notes[0].Count - 1) syncCount = 0;
                }

                if (bpm.shouldTick())
                {
                    PlayNote(handle, 30, 39, 99);
                    if (notesToRecord > 0) notesToRecord--;
                    if (isRecording())
                    {
                        rightText.Text = "Recording";
                    }
                    else {
                        rightText.Text = "Not recording";
                    }
                }
            }
        }

        private void playRecordedNote(int handle, List<int[]> noteList, int index) {
            Console.WriteLine("Notelist.count: " + noteList.Count + " synccount: " + index);
            PlayNote(handle, 30, noteList[index][0], noteList[index][1]);
        }

        private Boolean isRecording() {
            return notesToRecord > 0 && notesToRecord <= maxRecordedNotes;
        }

        private Boolean LegStomp(Boolean left, Skeleton skeleton) {
            if (left)
            {
                //if the left leg is raised
                if (legRaised(true, skeleton))
                {
                    //mark it as raised
                    leftLegWasRaised = true;
                    //can't be a stomp if it's still in the air
                    return false;
                }
                //if it was raised and is no longer
                else if (leftLegWasRaised)
                {
                    //mark it as no longer raised
                    leftLegWasRaised = false;
                    //if checking the left leg, then it has stomped
                    return true;
                }
            }
            else
            {
                //Console.WriteLine("gets past left leg");
                //if the right leg is raised
                if (legRaised(false, skeleton))
                {
                    //mark as raised
                    rightLegWasRaised = true;
                    //can't be a stomp if still raised
                    return false;
                }
                //if it was raised and is no longer
                else if (rightLegWasRaised)
                {
                    //Console.WriteLine("right leg stomped");
                    //mark it as no longer raised
                    rightLegWasRaised = false;
                    //if checking the right leg, then it has stomped
                    return true;
                }
            }
            return false;
        }

        private Boolean legRaised(Boolean left, Skeleton skeleton) {
            double legLength = CalculateLegLength(skeleton);
            if (left)
            {
                if ((skeleton.Joints[JointType.HipLeft].Position.Y - skeleton.Joints[JointType.AnkleLeft].Position.Y) < legLength * 0.75) return true;
            }
            else
            {
                if ((skeleton.Joints[JointType.HipRight].Position.Y - skeleton.Joints[JointType.AnkleRight].Position.Y) < legLength * 0.75) return true;
            }
            return false;
        }

        private void CalculateBPM() {
            while (leftArmPeaks.Count > 5)
            {
                leftArmPeaks.RemoveAt(0);
            }
            //Console.WriteLine("Peak Count: "+leftArmPeaks.Count);
            int lastTime = -1;
            double dif = -1;
            foreach (Peak p in leftArmPeaks)
            {
                if (lastTime == -1)
                {
                    lastTime = p.getTimeStamp();
                }
                else
                {
                    if (dif == -1)
                    {
                        dif = p.getTimeStamp() - lastTime;
                    }
                    else
                    {
                        dif += p.getTimeStamp() - lastTime;
                    }
                    lastTime = p.getTimeStamp();
                    //Console.WriteLine("Last Time: " + lastTime);
                }
            }
            //Console.WriteLine("Dif: " + dif);
            dif /= (leftArmPeaks.Count - 1);
            double bpmNum = dif / RefreshRate;
            bpmNum = 60/bpmNum;
            bpm.Update((int)bpmNum);
            //Console.WriteLine("BPM: "+bpm);
            bpmCounterLabel.Text = ((int)(bpmNum)).ToString();
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
            double[] Values = Points[1].ToArray();
            //check if there is a peak
            //if on a downward slope, must have previously been a peak
            double point1 = Values[NumberOfPoints - 1];
            double point2 = Values[NumberOfPoints - 2];
            double point3 = Values[NumberOfPoints - 3];

            if (point1 < point2)
            {
                //if the third value is less than the second the second must be a peak
                //wooo magic number to avoid tremours
                if (point3 < point2 && ((leftArmPeaks.Count == 0 && point2 > 0.1) || point2 > leftArmPeaks[leftArmPeaks.Count - 1].getRDitch() + 0.2))
                {
                    //first peak, so lDitch is 0
                    if (leftArmPeaks.Count == 0)
                    {
                        leftArmPeaks.Add(new Peak(0, point1, point2, NumberOfPoints));
                    }
                    else
                    {
                        //the lditch of the new peak is the rditch of the previous
                        leftArmPeaks.Add(new Peak(leftArmPeaks[leftArmPeaks.Count - 1].getRDitch(), point1, point2, NumberOfPoints));
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
                    if (leftArmPeaks.Count > 0)
                    {
                        (leftArmPeaks[leftArmPeaks.Count - 1]).setRDitch(point1);
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

        private double CalculateArmLength(Skeleton skeleton) {
            Joint j1 = skeleton.Joints[JointType.WristLeft];
            Joint j2 = skeleton.Joints[JointType.ElbowLeft];
            Joint j3 = skeleton.Joints[JointType.ShoulderLeft];

            return CalculateLimbLength(j1, j2, j3);
        }

        private double CalculateLegLength(Skeleton skeleton) {
            Joint j1 = skeleton.Joints[JointType.AnkleLeft];
            Joint j2 = skeleton.Joints[JointType.KneeLeft];
            Joint j3 = skeleton.Joints[JointType.HipLeft];

            return CalculateLimbLength(j1, j2, j3);
        }

        private double CalculateLimbLength(Joint j1, Joint j2, Joint j3) {
            double dist;
        
            dist = Math.Sqrt(Math.Pow((j1.Position.X - j2.Position.X), 2) + Math.Pow((j1.Position.Y - j2.Position.Y), 2) + Math.Pow((j1.Position.Z - j2.Position.Z), 2));
            dist += Math.Sqrt(Math.Pow((j2.Position.X - j3.Position.X), 2) + Math.Pow((j2.Position.Y - j3.Position.Y), 2) + Math.Pow((j2.Position.Z - j3.Position.Z), 2));

            return dist;
        }

        private int CalculateJointHeight(Joint joint, Joint reference, double armLength) {
            //Console.WriteLine("Arm lenght: " + armLength);
            armLength *= 0.9;
            var modifier = 64 / armLength;
            //Console.WriteLine("Modifier: " + modifier);
            var thing = 64 + modifier*(joint.Position.Y - reference.Position.Y);
            if (thing > 128) return 128;
            if (thing < 0) return 0;
            return (int)(thing);
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