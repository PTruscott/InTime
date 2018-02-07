using System.Text;
using System;
using System.Runtime.InteropServices;

namespace InTime
{

    [StructLayout(LayoutKind.Sequential)]
    public struct MidiOutCaps
    {
        public UInt16 wMid;
        public UInt16 wPid;
        public UInt32 vDriverVersion;

        [MarshalAs(UnmanagedType.ByValTStr,
           SizeConst = 32)]
        public String szPname;

        public UInt16 wTechnology;
        public UInt16 wVoices;
        public UInt16 wNotes;
        public UInt16 wChannelMask;
        public UInt32 dwSupport;
    }

    internal class MIDINotes
    {
        // MCI INterface
        [DllImport("winmm.dll")]
        private static extern long mciSendString(string command,
           StringBuilder returnValue, int returnLength,
           IntPtr winHandle);

        // Midi API
        [DllImport("winmm.dll")]
        private static extern int midiOutGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern int midiOutGetDevCaps(Int32 uDeviceID,
           ref MidiOutCaps lpMidiOutCaps, UInt32 cbMidiOutCaps);

        [DllImport("winmm.dll")]
        private static extern int midiOutOpen(ref int handle,
           int deviceID, MidiCallBack proc, int instance, int flags);

        [DllImport("winmm.dll")]
        private static extern int midiOutShortMsg(int handle,
           int message);

        [DllImport("winmm.dll")]
        private static extern int midiOutClose(int handle);

        private delegate void MidiCallBack(int handle, int msg,
           int instance, int param1, int param2);


        static string Mci(string command)
        {
            StringBuilder reply = new StringBuilder(256);
            mciSendString(command, reply, 256, IntPtr.Zero);
            return reply.ToString();
        }

        public static void PlayNote(int handle, int vel, int note)
        {
            //midiOutOpen(ref handle, 0, null, 0, 0);
            //converts the user input to hex
            string velHex = vel.ToString("X");
            string noteHex = note.ToString("X");
            //builds into a hex string
            string s = string.Format("0x00{0}{1}91", velHex, noteHex);
            //converts to an integer
            int value = (int)new System.ComponentModel.Int32Converter().ConvertFromString(s);
            //plays the note
            midiOutShortMsg(handle, value);
            //System.Threading.Thread.Sleep(5000);
            //midiOutClose(handle);
        }

        public MIDINotes() {
            //int handle = 0;
            //var numDevs = midiOutGetNumDevs();
            //MidiOutCaps myCaps = new MidiOutCaps();
            //var r = midiOutGetDevCaps(0, ref myCaps,
            //   (UInt32)Marshal.SizeOf(myCaps));
           // r = 
            //res = r;
            //Console.WriteLine(res.GetType());
            //PlayNote(handle, 127, 60);
            //Console.ReadLine();
            //PlayNote(handle, 127, 57);
            //Console.ReadLine();
            //res = midiOutClose(handle);
        }

        /*
        static void Main()
        {
            int handle = 0;
            var numDevs = midiOutGetNumDevs();
            MidiOutCaps myCaps = new MidiOutCaps();
            var res = midiOutGetDevCaps(0, ref myCaps,
               (UInt32)Marshal.SizeOf(myCaps));
            res = midiOutOpen(ref handle, 0, null, 0, 0);
            PlayNote(handle, 127, 60);
            Console.ReadLine();
            PlayNote(handle, 127, 57);
            Console.ReadLine();
            res = midiOutClose(handle);
        }
        */
    }
}
