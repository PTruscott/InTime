namespace InTime
{
    public class Note
    {
        private int instrument;
        private int note;

        public Note(int note, int instrument)
        {
            this.note = note;
            this.instrument = instrument;
        }

        public int getInstrument()
        {
            return instrument;
        }

        public int getNote()
        {
            return note;
        }
    }
}