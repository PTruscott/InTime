namespace InTime
{
    public class Peak
    {
        private double lDitch;
        private double rDitch;
        private double size;
        private int timeStamp;
        private double height;

        public Peak(double lDitch, double rDitch, double height, int timeStamp)
        {
            size = height - (lDitch + rDitch) / 2;
            this.lDitch = lDitch;
            this.rDitch = rDitch;
            this.timeStamp = timeStamp;
            this.height = height;
        }

        public double GetSize()
        {
            return size;
        }

        public double GetRDitch()
        {
            return rDitch;
        }

        public int GetTimeStamp()
        {
            return timeStamp;
        }

        public void TimeStep()
        {
            timeStamp--;
        }

        public void SetRDitch(double rDitch)
        {
            this.rDitch = rDitch;
            size = height - (lDitch + rDitch) / 2;
        }
    }
}
