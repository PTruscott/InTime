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
    using System.Runtime.InteropServices;
    using System.Collections.Generic;
    using System.Linq;

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

        /// <summary>{ get; set; }
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
        private BPMTicker bpm;

        /// <summary>
        /// list of the peaks of the left arm to calculate bpm with if needed
        /// </summary>
        private List<Peak> leftArmPeaks;

        /// <summary>
        /// If bpm is being calculated, it stops notes playing
        /// </summary>
        private bool isCalculatingBPM;

        /// <summary>
        /// The heap of currently playing notes, stored as NoteTuples
        /// Used to end notes playing
        /// </summary>
        private Heap<NoteTuple> playingNotes;


        /// <summary>
        /// Variables for calculating if a leg had been stomped before
        /// </summary>
        bool leftLegWasRaised = false;
        bool rightLegWasRaised = false;

        enum ArmPos { Lowered, Normal, Raised };
        ArmPos prevArmPos = ArmPos.Normal;

        /// <summary>
        /// The length of the arm
        /// </summary>
        double armLength;

        /// <summary>
        /// a series of variables to record loops
        /// </summary>
        int loopLength = 16;
        int numberOfBeats;
        int beatCounter = 0;
        int currentTick;
        bool isRecording = false;
        bool shouldRecord = false;
        double energy = 0;

        //specifies the channel on which is currently playing
        private int currentInstrument = 91;
        //a recorded list of the ints
        private List<Note[]> recordedNotes;
        private int currentNoteValue = 60;
        private Node currentDrum = new Kick();
        private bool playKick = true;

        /// <summary>
        /// A list of variables to lock to scales
        /// </summary>
        private int numOctaves = 8;
        private int maxNotes = 103;
        private int[] cm = {0, 2, 3, 5, 7, 8, 10};
        private int[] c = {0, 2, 4, 5, 7, 9, 11};
        private int[] ca = {0, 4, 7};
        private int[] mp = { 0, 3, 5, 7, 10 };

        private int[] currentScale;
        private bool useScale = true;

        /// <summary>   
        /// Used to be able to end playing notes
        /// </summary>
        int currentTime = Int32.MinValue;
        //private SortedDictionary<int, Note> playingNotes;

        private delegate void midiCallBack(int handle, int msg,
           int instance, int param1, int param2);

        [DllImport("winmm.dll")]
        private static extern int midiOutOpen(ref int handle,
           int deviceID, midiCallBack proc, int instance, int flags);

        [DllImport("winmm.dll")]
        private static extern int midiOutShortMsg(int handle,
           int message);

        int handle = 0;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            Points = new List<double>[2];
            recordedNotes = new List<Note[]>();
            leftArmPeaks = new List<Peak>();

            if (useScale)
            {
                currentScale = cm;
                maxNotes = currentScale.Length * numOctaves;
                currentNoteValue = (int)Math.Floor((Convert.ToDouble(numOctaves / 2))+2) * currentScale.Length; 
            }

            numberOfBeats = loopLength * 2;
            playingNotes = new MinHeap<NoteTuple>();

            //opens the midi out 
            midiOutOpen(ref handle, 0, null, 0, 0);

            //sets up four channels with four different instruments.  
            midiOutShortMsg(handle, 0x000056C1);
            midiOutShortMsg(handle, 0x000028C2);
            midiOutShortMsg(handle, 0x000004C3);
            midiOutShortMsg(handle, 0x00006CC4);

            for (int i = 0; i < Points.Length; i++)
            {
                Points[i] = new List<double>();
                for (int j = 0; j < NumberOfPoints; j++)
                {
                    Points[i].Add(0);
                }
            }
            InitGraphs();

            InitializeComponent();

            DataContext = this;

            labelAngleForIntrument.Text = "0";

            currentTick = loopLength;

            bpm = new BPMTicker(leftText);

            isCalculatingBPM = true;

            currentDrum = new Kick();
        }

        private void PlayNote(int handle, Note thisNote)
        {
            //don't want drumbeats to be recorded
            if (thisNote.GetInstrument() != 99)
            {
                playingNotes.Add(new NoteTuple(4 + currentTime, thisNote));
            }
            midiNote(handle, thisNote);
        }

        private void EndNote(int handle, Note thisNote)
        {
            midiNote(handle, thisNote);
        }

        private void midiNote(int handle, Note thisNote)
        {
            //midiOutOpen(ref handle, 0, null, 0, 0);
            //converts the user input to hex
            string velHex = 60.ToString("X");
            if (typeSlider.Value == 1)
            {
                var vel = (thisNote.GetDuration() * 6);
                switch (thisNote.GetInstrument())
                {
                    case 94:
                        vel *= 4;
                        break;
                    case 93: 
                        vel *= 3;
                        break;
                    case 92:
                        vel *= 2;
                        break;
                }

                velHex = vel.ToString("X");
            }
            string noteHex = thisNote.GetNote().ToString("X");
            string insHex = thisNote.GetInstrument().ToString();
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

            RightArmCollection = new SeriesCollection
            {
                new LineSeries { Values = new ChartValues<ObservableValue> { new ObservableValue(0) } }
            };

            for (int i = 0; i < NumberOfPoints / 5 - 1; i++)
            {
                try
                {
                    RightArmCollection[0].Values.Add(new ObservableValue(0));
                    LeftArmCollection[0].Values.Add(new ObservableValue(0));
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

        /// <summary>
        /// The main update loop, happens every frame
        /// </summary>
        /// <param name="skeleton">the skeleton data of the player</param>
        private void Update(Skeleton skeleton)
        {
            CalculateDist(skeleton);

            UpdateGraphs();

            ChangeInstrument(skeleton);

            armLength = CalculateArmLength(skeleton);

            foreach (Peak p in leftArmPeaks)
            {
                p.TimeStep();
            }

            //check if right leg stomped
            if (LegStomp(false, skeleton))
            {
                if (isCalculatingBPM) bpmCalculatingLabel.Text = "BPM";
                else bpmCalculatingLabel.Text = "BPM (Calculating...)";
                isCalculatingBPM = !isCalculatingBPM;
                //Console.WriteLine("Right leg stomp");
            }
            //check if left leg stomped
            else if (LegStomp(true, skeleton))
            {
                //Console.WriteLine("Left leg stomp");
                //if currently recording and new record message comes in then wipe and start again
                if (isRecording) recordedNotes.RemoveAt(recordedNotes.Count - 1);
                if (shouldRecord)
                {
                    shouldRecord = false;
                    recordingLabel.Text = "Not Recording";
                    var greyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6e6e6e"));
                    recordingLabel.Foreground = greyBrush;
                    recordingCounter.Foreground = greyBrush;
                }
                else
                {
                    recordedNotes.Add(new Note[numberOfBeats]);
                    shouldRecord = true;
                    isRecording = false;
                    recordingLabel.Text = "Will Record";
                    recordingLabel.Foreground = new SolidColorBrush(Colors.Orange);
                    recordingCounter.Foreground = new SolidColorBrush(Colors.Orange);
                }
            }

            if (isCalculatingBPM)
            {
                UpdatePeaks();
                CalculateBPM();
            }
            else
            {
                int noteValue = currentNoteValue;

                if (typeSlider.Value == 0)
                {
                    var wasArmMoved = WasArmMoved(skeleton);

                    if (wasArmMoved != ArmPos.Normal)
                    {
                        //Console.WriteLine("Arm moved!");
                        prevArmPos = ArmPos.Normal;

                        if (wasArmMoved == ArmPos.Raised) currentNoteValue++;
                        else currentNoteValue--;
                        currentNoteValue = Math.Max(24, Math.Min(currentNoteValue, 127));
                        noteValue = currentNoteValue;
                       
                        if (useScale)
                        {
                            var newNoteVal = Math.Floor(Convert.ToDouble(currentNoteValue) / currentScale.Length) * currentScale.Length;
                            //Console.WriteLine("New Note Val: " + newNoteVal);
                            var octave = newNoteVal / currentScale.Length;
                            //Console.WriteLine("Octave: " + octave);
                            var leftOver = currentNoteValue - newNoteVal;
                            //Console.WriteLine("Leftover: " + leftOver);
                            var tempNote = (int)(12 * octave + currentScale[(int)leftOver]);
                            //Console.WriteLine("Old note val: " + noteValue);
                            noteValue = Math.Max(24, tempNote);
                        }
                        //Console.WriteLine(currentNoteValue);
                        //Console.WriteLine("Note value: " + noteValue);
                    }
                }

                var shouldTick = bpm.ShouldTick();

                if (shouldTick > 0)
                {
                    //remove playing notes
                    while (playingNotes.Count > 0 && playingNotes.Min().GetTimeEnd() < currentTime)
                    {
                        Note endingNote = playingNotes.ExtractDominating().getNote();
                        endingNote = endingNote.EndNote();
                        EndNote(handle, endingNote);
                    }

                    int duration = 3;

                    var dist = skeleton.Joints[JointType.ShoulderLeft].Position.X - skeleton.Joints[JointType.WristLeft].Position.X;
                    
                    //play a note
                    if (typeSlider.Value == 1)
                    {
                        double armHeight = CalculateJointHeight(skeleton.Joints[JointType.WristLeft], skeleton.Joints[JointType.ShoulderLeft], armLength);
                        duration = (int)((dist / Convert.ToDouble(armLength)) * 12);
                        if (useScale)
                        {
                            var newNoteVal = Math.Floor(armHeight / currentScale.Length) * currentScale.Length;
                            //Console.WriteLine("New Note Val: " + newNoteVal);
                            var octave = newNoteVal / currentScale.Length;
                            //Console.WriteLine("Octave: " + octave);
                            var leftOver = armHeight - newNoteVal;
                            //Console.WriteLine("Leftover: " + leftOver);
                            noteValue = (int)(12 * octave + currentScale[(int)leftOver]);
                            //Console.WriteLine("Old note val: " + noteValue);
                            noteValue = Math.Max(0, noteValue);
                        }
                        else
                        {
                            noteValue = (int)armHeight;
                        }
                        noteValue += 24;
                        //Console.WriteLine("New Note: " + noteValue);
                    }
                    if (duration > 0)
                    {
                        Note thisNote = new Note(noteValue, currentInstrument, duration);
                        //Console.WriteLine("Playing note! " + currentTime + " Duration: "+duration);
                        PlayNote(handle, thisNote);
                        if (isRecording)
                        {
                            recordedNotes[recordedNotes.Count - 1][beatCounter] = thisNote;
                            //Console.WriteLine("Recording note at " + beatCounter + " " + recordedNotes[recordedNotes.Count - 1][beatCounter]);
                        }
                    }

                    var maxRecordings = recordedNotes.Count;

                    if (maxRecordings > 0)
                    {
                        if (isRecording) maxRecordings--;
                        for (int i = 0; i < maxRecordings; i++)
                        {
                            if (recordedNotes[i][beatCounter] != null)
                            {
                                PlayNote(handle, recordedNotes[i][beatCounter]);
                                //Console.WriteLine("Playing recorded note at: " + beatCounter + " out of " + recordedNotes[i].Count());
                            }
                        }
                    }
                    currentTime++;

                    if (shouldTick == 1)
                    {

                        //play drum beat, needs to take what type
                        if (currentDrum.GetType() != typeof(Rest))
                        {
                            currentDrum.UpdateProbabilites(energy);
                            if (currentDrum.GetType() == typeof(HiHat)) PlayNote(handle, new Note(currentDrum.GetNote(), 99, 90));
                            else PlayNote(handle, new Note(currentDrum.GetNote(), 99, 117));

                        }
                        currentDrum = currentDrum.GetNextNode();
                    }

                    // update recording.
                    // shouldTick == 2
                    else
                    {
                        if (currentTick > 1) currentTick--;
                        else
                        {
                            currentTick = loopLength;
                            if (shouldRecord)
                            {
                                shouldRecord = false;
                                isRecording = true;
                                recordingLabel.Text = "Recording";
                                recordingLabel.Foreground = new SolidColorBrush(Colors.Red);
                                recordingCounter.Foreground = new SolidColorBrush(Colors.Red);
                            }
                            else if (isRecording)
                            {
                                isRecording = false;
                                recordingLabel.Text = "Not Recording";
                                var greyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6e6e6e"));
                                recordingLabel.Foreground = greyBrush;
                                recordingCounter.Foreground = greyBrush;
                            }
                        }
                        recordingCounter.Text = currentTick.ToString();
                    }

                    beatCounter++;
                    if (beatCounter >= numberOfBeats)
                    {
                        beatCounter = 0;
                    }
                    if (beatCounter % 2 == 0)
                    {
                        currentDrum = new Kick();
                        if (playKick) PlayNote(handle, new Note(currentDrum.GetNote(), 99, 127));
                        else PlayNote(handle, new Note(38, 99, 127));
                        playKick = !playKick;
                        energy = GetEnergy();
                        currentDrum.UpdateProbabilites(energy);
                        currentDrum = currentDrum.GetNextNode();
                    }

                }
            }
        }


        private double GetEnergy()
        {
            double energy = 0;
            var Values = Points[1].ToArray();
            for (int i = Values.Length / 2; i < Values.Length; i++)
            {
                energy += Math.Abs(Values[i]-Values[i-1]);
            }
            energy -= 3;
            energy /= 5;
            energy = Math.Max(0, Math.Min(1, energy));
            return energy;
        }


        private void ChangeInstrument(Skeleton skeleton)
        {
            int forearmAngle = (int)(CalculateJointAngle(skeleton.Joints[JointType.ElbowRight], skeleton.Joints[JointType.WristRight]));
            //if shoulder angle <= arm angle
            if (CalculateJointAngle(skeleton.Joints[JointType.ShoulderCenter], skeleton.Joints[JointType.ShoulderRight]) >
                CalculateJointAngle(skeleton.Joints[JointType.ShoulderRight], skeleton.Joints[JointType.ElbowRight]))
            {
                //match forearm angle within a certain angle 
                if (forearmAngle > 340 || forearmAngle < 20)
                {
                    labelAngleForIntrument.Text = "0";
                    currentInstrument = 91;
                }
                else if (forearmAngle > 70 && forearmAngle < 110)
                {
                    labelAngleForIntrument.Text = "90";
                    currentInstrument = 92;
                }
                else if (forearmAngle > 160 && forearmAngle < 200)
                {
                    labelAngleForIntrument.Text = "180";
                    currentInstrument = 93;
                }
                else if (forearmAngle > 250 && forearmAngle < 310)
                {
                    labelAngleForIntrument.Text = "270";
                    currentInstrument = 94;
                }
            }
            //update current instrument
        }

        private double CalculateJointAngle(Joint j2, Joint j1)
        {
            float xDiff = j2.Position.X - j1.Position.X;
            float yDiff = j2.Position.Y - j1.Position.Y;
            return 360 - (((Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI) + 450) % 360);
        }

        private ArmPos WasArmMoved(Skeleton skeleton)
        {
            //0' is directly up
            var angle = CalculateJointAngle(skeleton.Joints[JointType.ElbowLeft], skeleton.Joints[JointType.WristLeft]);
            if (angle > 305)
            {
                prevArmPos = ArmPos.Raised;
                return ArmPos.Normal;
            }
            //slightly angled down
            if (angle < 235)
            {
                prevArmPos = ArmPos.Lowered;
                return ArmPos.Normal;
            }
            return prevArmPos;
        }

        private Boolean LegStomp(Boolean left, Skeleton skeleton)
        {
            if (left)
            {
                //if the left leg is raised
                if (LegRaised(true, skeleton))
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
                if (LegRaised(false, skeleton))
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

        private Boolean LegRaised(Boolean left, Skeleton skeleton)
        {
            double legLength = CalculateLegLength(skeleton);
            if (left)
            {
                if ((skeleton.Joints[JointType.HipLeft].Position.Y - skeleton.Joints[JointType.AnkleLeft].Position.Y) < legLength * 0.8) return true;
            }
            else
            {
                if ((skeleton.Joints[JointType.HipRight].Position.Y - skeleton.Joints[JointType.AnkleRight].Position.Y) < legLength * 0.8) return true;
            }
            return false;
        }

        private void CalculateBPM()
        {
            while (leftArmPeaks.Count > 5)
            {
                leftArmPeaks.RemoveAt(0);
            }
            int lastTime = -1;
            double dif = -1;
            foreach (Peak p in leftArmPeaks)
            {
                if (lastTime == -1)
                {
                    lastTime = p.GetTimeStamp();
                }
                else
                {
                    if (dif == -1)
                    {
                        dif = p.GetTimeStamp() - lastTime;
                    }
                    else
                    {
                        dif += p.GetTimeStamp() - lastTime;
                    }
                    lastTime = p.GetTimeStamp();
                }
            }
            dif /= (leftArmPeaks.Count - 1);
            double bpmNum = dif / RefreshRate;
            bpmNum = 60 / bpmNum;
            if ((int)bpmNum == 0) bpmNum = 1;
            bpm.Update((int)bpmNum);

            bpmCounterLabel.Text = ((int)(bpmNum)).ToString();
        }

        private Boolean IsCloseTo(int num1, int num2)
        {
            int range = 10;
            return (num2 > num1 - range && num2 < num1 + range);
        }

        private void CalculateDist(Skeleton skeleton)
        {
            if (isCalculatingBPM)
            {
                Points[0].Add(CalulateJointDist(JointType.WristRight, skeleton));
                Points[1].Add(CalulateJointDist(JointType.WristLeft, skeleton));
            }
            else
            {
                var ratio = 1.5 / 128;
                Points[0].Add(ratio * CalculateJointHeight(skeleton.Joints[JointType.WristRight], skeleton.Joints[JointType.ShoulderRight], armLength));
                Points[1].Add(ratio * CalculateJointHeight(skeleton.Joints[JointType.WristLeft], skeleton.Joints[JointType.ShoulderLeft], armLength));
            }
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
                if (point3 < point2 && ((leftArmPeaks.Count == 0 && point2 > 0.1) || point2 > leftArmPeaks[leftArmPeaks.Count - 1].GetRDitch() + 0.2))
                {
                    //first peak, so lDitch is 0
                    if (leftArmPeaks.Count == 0)
                    {
                        leftArmPeaks.Add(new Peak(0, point1, point2, NumberOfPoints));
                    }
                    else
                    {
                        //the lditch of the new peak is the rditch of the previous
                        leftArmPeaks.Add(new Peak(leftArmPeaks[leftArmPeaks.Count - 1].GetRDitch(), point1, point2, NumberOfPoints));
                    }
                }
                //if not, the last peak is more pronouced
                else
                {
                    if (leftArmPeaks.Count > 0)
                    {
                        (leftArmPeaks[leftArmPeaks.Count - 1]).SetRDitch(point1);
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
        }

        private double CalulateJointDist(JointType jointType, Skeleton skeleton)
        {
            Joint joint0 = skeleton.Joints[jointType];
            Joint joint1 = skeleton.Joints[JointType.ShoulderCenter];

            double dist = Math.Abs(joint0.Position.X - joint1.Position.X) + Math.Abs(joint0.Position.Y - joint1.Position.Y) + Math.Abs(joint0.Position.Z - joint1.Position.Z);
            return dist;
        }

        private double CalculateArmLength(Skeleton skeleton)
        {
            Joint j1 = skeleton.Joints[JointType.WristLeft];
            Joint j2 = skeleton.Joints[JointType.ElbowLeft];
            Joint j3 = skeleton.Joints[JointType.ShoulderLeft];

            return CalculateLimbLength(j1, j2, j3);
        }

        private double CalculateLegLength(Skeleton skeleton)
        {
            Joint j1 = skeleton.Joints[JointType.AnkleLeft];
            Joint j2 = skeleton.Joints[JointType.KneeLeft];
            Joint j3 = skeleton.Joints[JointType.HipLeft];

            return CalculateLimbLength(j1, j2, j3);
        }

        private double CalculateLimbLength(Joint j1, Joint j2, Joint j3)
        {
            double dist;

            dist = Math.Sqrt(Math.Pow((j1.Position.X - j2.Position.X), 2) + Math.Pow((j1.Position.Y - j2.Position.Y), 2) + Math.Pow((j1.Position.Z - j2.Position.Z), 2));
            dist += Math.Sqrt(Math.Pow((j2.Position.X - j3.Position.X), 2) + Math.Pow((j2.Position.Y - j3.Position.Y), 2) + Math.Pow((j2.Position.Z - j3.Position.Z), 2));

            return dist;
        }

        private int CalculateJointHeight(Joint joint, Joint reference, double armLength)
        {
            //Console.WriteLine("Arm lenght: " + armLength);
            armLength *= 0.9;
            var modifier = maxNotes/2 / armLength;
            //Console.WriteLine("Modifier: " + modifier);
            var thing = maxNotes/2 + modifier * (joint.Position.Y - reference.Position.Y);
            return (int)Math.Max(0, Math.Min(thing, maxNotes));
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
    }
}