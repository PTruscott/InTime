using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace InTime
{
    public class BPMCounter
    {
        private TextBlock text;
        private List<double> bpms;
        private int maxBpms = 1;
        private int bpm;
        private int timeSinceBeat = 0;

        public BPMCounter(TextBlock text)
        {
            this.text = text;
            text.Text = "N/A BPM";
            bpm = 0;
            bpms = new List<double>();
        }

        public void Update(double bpm)
        {
            bpms.Add(bpm);
            if (bpms.Count > maxBpms)
            {
                bpms.RemoveAt(0);
            }
            double b = 0;
            foreach (double i in bpms)
            {
                b += i;
            }
            b /= bpms.Count;
            this.bpm = (int)Math.Round(b);
            text.Text = bpm + " BPM";
        }

        public int getBPM() {
            return bpm;
        }

        public Boolean shouldTick() {
            //Console.WriteLine(timeSinceBeat);
            timeSinceBeat++;
            if (bpm == 0) bpm = 1;
            if (timeSinceBeat >= ((60.0/bpm) *30)) {
                timeSinceBeat = 0;
                return true;
            }
            return false;
        }
    }
}


