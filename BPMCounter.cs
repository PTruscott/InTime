using System;
using System.Windows.Controls;

namespace InTime
{
    public class BPMTicker
    {
        private TextBlock text;
        private int bpm;
        private int timeSinceBeat = 0;
        private int timeSinceTick = 0;
        private double bpmTick;

        public BPMTicker(TextBlock text)
        {
            this.text = text;
            text.Text = "N/A BPM";
            bpm = 1;
        }

        public void Update(double bpm)
        {
            if (bpm < 1) bpm = 1;
            this.bpm = (int)Math.Round(bpm);
            text.Text = this.bpm + " BPM";
            bpmTick = (60.0 / this.bpm) * 30;
        }

        public int getBPM() {
            return bpm;
        }

        public int ShouldTick() {
            timeSinceBeat++;
            timeSinceTick++;
            
            if (timeSinceBeat >= bpmTick) {
                timeSinceBeat = 0;
                timeSinceTick = 0;
                return 2;
            }
            if (timeSinceTick >= bpmTick / 2) {
                timeSinceTick = 0;
                return 1;
            }
            return 0;
        }
    }
}


