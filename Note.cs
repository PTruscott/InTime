using System;

namespace InTime
{
    public class Note : IComparable<Note>
    {
        private int instrument;
        private int note;
        private int duration;

        public Note(int note, int instrument, int duration)
        {
            this.note = note;
            this.instrument = instrument;
            this.duration = duration;
        }

        public Note(int note, int instrument)
        {
            this.instrument = instrument;
            this.note = note;
            duration = 0;
        }

        public int getInstrument()
        {
            return instrument;
        }

        public int getNote()
        {
            return note;
        }

        public int getDuration()
        {
            return duration;
        }

        public Note endNote()
        {
            return new Note(note, instrument);
        }

        public int CompareTo(Note other)
        {
            if (duration < other.getDuration()) return -1;
            if (duration == other.getDuration()) return 0;
            return 1;
        }
    }

    public class NoteTuple : IComparable<NoteTuple>
    {
        private Note note;
        private int timeEnd;

        public NoteTuple(int timeEnd, Note note)
        {
            this.note = note;
            this.timeEnd = timeEnd;
        }

        public int getTimeEnd()
        {
            return timeEnd;
        }

        public Note getNote()
        {
            return note;
        }

        public int CompareTo(NoteTuple other)
        {
            if (timeEnd < other.getTimeEnd()) return -1;
            return 1;   
        }
    }
}