﻿using System;
using System.Windows.Controls;

namespace InTime
{
    public class BPMTicker
    {
        private TextBlock text;
        private int bpm;
        private int timeSinceBeat = 0;

        public BPMTicker(TextBlock text)
        {
            this.text = text;
            text.Text = "N/A BPM";
            bpm = 0;
        }

        public void Update(double bpm)
        {
            if (bpm == 0) bpm = 1;
            this.bpm = (int)Math.Round(bpm);
            text.Text = bpm + " BPM";
        }

        public int getBPM() {
            return bpm;
        }

        public int shouldTick() {
            timeSinceBeat++;
            var bpmTick = (60.0 / bpm) * 30;
            if (timeSinceBeat >= bpmTick) {
                timeSinceBeat = 0;
                return 2;
            }
            if (timeSinceBeat >= bpmTick / 8) {
                return 1;
            }
            return 0;
        }
    }
}


