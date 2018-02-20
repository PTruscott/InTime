namespace InTime
{
    public class Note
    {
        private int expiryTime;
        private int instrument;
        private int note;

        public Note(int vel, int currentTime, int note, int instrument)
        {
            expiryTime = currentTime + vel;
            this.note = note;
            this.instrument = instrument;
        }

        public int getInstrument()
        {
            return instrument;
        }

        public int getExpiryTime()
        {
            return expiryTime;
        }

        public int getNote()
        {
            return note;
        }
    }
}