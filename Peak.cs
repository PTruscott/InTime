namespace InTime
{
    public class Peak
    {
        private double lDitch;
        private double rDitch;
        private double size;
        private int timeStamp;
        private double height;

        public Peak(double lDitch, double rDitch, double height, int timeStamp) {
            size = height-(lDitch+rDitch)/2;
            this.lDitch = lDitch;
            this.rDitch = rDitch;
            this.timeStamp = timeStamp;
            this.height = height;
        }

        public double getSize() {
            return size;
        }

        public double getRDitch() {
            return rDitch;
        }

        public int getTimeStamp() {
            return timeStamp;
        }

        public void timeStep() {
            timeStamp--;
        }

        public void setRDitch(double rDitch) {
            this.rDitch = rDitch;
            size = height - (lDitch + rDitch) / 2;
        }
    }
}
