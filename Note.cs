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

        public int GetInstrument()
        {
            return instrument;
        }

        public int GetNote()
        {
            return note;
        }

        public int GetDuration()
        {
            return duration;
        }

        public Note EndNote()
        {
            return new Note(note, instrument);
        }

        public int CompareTo(Note other)
        {
            if (duration < other.GetDuration()) return -1;
            if (duration == other.GetDuration()) return 0;
            return 1;
        }

        public override string ToString()
        {
            return "note: " + note + " duration: " + duration+ " instrument: " + instrument;
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

        public int GetTimeEnd()
        {
            return timeEnd;
        }

        public Note getNote()
        {
            return note;
        }

        public int CompareTo(NoteTuple other)
        {
            if (timeEnd < other.GetTimeEnd()) return -1;
            return 1;   
        }
    }
}