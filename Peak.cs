namespace InTime
{
    class Peak
    {
        private float size;
        private float timeStamp;

        public Peak(float size, float timeStamp) {
            this.size = size;
            this.timeStamp = timeStamp;
        }

        public float getSize() {
            return size;
        }

        public float getTimeStampe() {
            return timeStamp;
        }

        public void timeStep() {
            timeStamp--;
        }

        public void setSize(float size) {
            this.size = size;
        }
    }
}
