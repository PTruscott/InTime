using System;
using System.Collections.Generic;
using System.Linq;

namespace InTime
{
    internal abstract class Node
    {
        // a tuple of probability, delay, Node
        protected double[] probabilities;
        protected double[] minprobabilities;
        protected double[] maxprobabilities;

        protected Random rand = new Random();
        protected int noteType;

        public Node GetNextNode()
        {
            var num = rand.NextDouble();
            double total = 0;
            for (int i = 0; i < probabilities.Length; i++)
            {
                total += probabilities[i];
                if (num < total)
                {
                    //Console.WriteLine("Note chances: " + num + ", " + total + ", " + i);
                    switch (i)
                    {
                        case 0:
                            return new HiHat();
                        case 1:
                            return new Kick();
                        case 2:
                            return new Rest();
                        default:
                            Console.WriteLine("Something happened! " + num + ", " + total + ", " + i);
                            return new Kick();
                    }
                }
            }
            return new Kick();
        }

        public virtual int GetNote()
        {
            return noteType;
        }

        public void UpdateProbabilites(double speed)
        {
            speed = Math.Max(0, (Math.Min(1, speed)));
            var prob = from i in Enumerable.Range(0, maxprobabilities.Length) select minprobabilities[i] + (maxprobabilities[i] - minprobabilities[i]) * speed;
            probabilities = prob.ToArray();
            //foreach (double p in probabilities) Console.Write(p+", ");
            //Console.WriteLine();
        }
    }

    internal class HiHat : Node
    {
        public HiHat()
        {
            noteType = 46;
            minprobabilities = new double[] { 0.2, 0, 0.8 }; // good starting nums
            maxprobabilities = new double[] { 0.8, 0.1, 0.1 };

            probabilities = new double[] { 0.8, 0.1, 0.1 };
        }
    }

    internal class Kick : Node
    {
        public Kick()
        {
            noteType = 36;

            minprobabilities = new double[] { 0.1, 0.2, 0.7 }; // good starting nums
            maxprobabilities = new double[] { 0.6, 0.4, 0 };

            probabilities = new double[] { 0.8, 0.2, 0 };
        }
    }

    internal class Rest : Node
    {
        public Rest()
        {
            minprobabilities = new double[] { 0.25, 0.2, 0.55 }; // good starting nums
            maxprobabilities = new double[] { 0.8, 0.2, 0 };

            probabilities = new double[] { 0.8, 0.2, 0 };
        }
    }
}