using System;
using System.Collections.Generic;

namespace InTime
{
    internal abstract class Node
    {
        // a tuple of probability, delay, Node
        protected double[] probablilites;
        protected double[] startingProbablilites;
        protected double[] maxProbablilites;

        protected Random rand = new Random();
        protected int noteType;

        public Node GetNextNode()
        {
            var num = rand.NextDouble();
            double total = 0;
            for (int i = 0; i < probablilites.Length; i++)
            {
                total += probablilites[i];
                if (num < total)
                {
                    //Console.WriteLine("Note chances: " + num + ", " + total + ", " + i);
                    switch(i)
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

        }
    }

    internal class HiHat : Node
    {
        public HiHat()
        {
            noteType = 46;
            //probablilites = new double[] { 0.4, 0.1, 0.5 }; // good starting nums
            startingProbablilites = new double[] { 0.6, 0.3, 0.1 };
            probablilites = startingProbablilites;
            probablilites = new double[] { 0.8, 0.1, 0.1 };

        }
    }

    internal class Kick : Node
    {
        public Kick()
        {
            noteType = 36;
            //probablilites = new double[] { 0.3, 0.2, 0.5 }; // good starting nums
            startingProbablilites = new double[] { 0.65, 0.25, 0.1 };
            probablilites = startingProbablilites;

            probablilites = new double[] { 0.8, 0.2, 0 };

        }
    }

    internal class Rest : Node
    {
        public Rest()
        {
            startingProbablilites = new double[] { 0.45, 0.25, 0.3 }; // good starting nums
            maxProbablilites = new double[] { 0.75, 0.25, 0 };
            probablilites = startingProbablilites;

            probablilites = new double[] { 0.8, 0.2, 0 };
        }
    }
}