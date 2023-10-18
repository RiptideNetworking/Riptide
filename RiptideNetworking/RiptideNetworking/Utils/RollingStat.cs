// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Linq;

namespace Riptide.Utils
{
    /// <summary>Represents a rolling series of numbers.</summary>
    public class RollingStat
    {
        /// <summary>The position in the array of the latest item.</summary>
        private int index;
        /// <summary>How many of the array's slots are in use.</summary>
        private int slotsFilled;
        /// <inheritdoc cref="Mean"/>
        private double mean;
        /// <summary>The sum of the mean subtracted from each value in the array.</summary>
        private double sumOfSquares;
        /// <summary>The array used to store the values.</summary>
        private readonly double[] array;

        /// <summary>The mean of the stat's values.</summary>
        public double Mean => mean;
        /// <summary>The variance of the stat's values.</summary>
        public double Variance => slotsFilled > 1 ? sumOfSquares / (slotsFilled - 1) : 0;
        /// <summary>The standard deviation of the stat's values.</summary>
        public double StandardDev
        {
            get
            {
                double variance = Variance;
                if (variance >= double.Epsilon)
                {
                    double root = Math.Sqrt(variance);
                    return double.IsNaN(root) ? 0 : root;
                }

                return 0;
            }
        }

        /// <summary>Initializes the stat.</summary>
        /// <param name="sampleSize">The number of values to store.</param>
        public RollingStat(int sampleSize)
        {
            index = 0;
            slotsFilled = 0;
            mean = 0;
            sumOfSquares = 0;
            array = new double[sampleSize];
        }
        
        /// <summary>Adds a new value to the stat.</summary>
        /// <param name="value">The value to add.</param>
        public void Add(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return;

            index %= array.Length;
            double oldMean = mean;
            double oldValue = array[index];
            array[index] = value;
            index++;

            if (slotsFilled == array.Length)
            {
                double delta = value - oldValue;
                mean += delta / slotsFilled;
                sumOfSquares += delta * (value - mean + (oldValue - oldMean));
            }
            else
            {
                slotsFilled++;
                double delta = value - oldMean;
                mean += delta / slotsFilled;
                sumOfSquares += delta * (value - mean);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (slotsFilled == array.Length)
                return string.Join(",", array);

            return string.Join(",", array.Take(slotsFilled));
        }
    }
}
