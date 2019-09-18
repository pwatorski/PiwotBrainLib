﻿using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using System.Threading.Tasks;

namespace PiwotBrainLib
{
    class Learner
    {
        protected Brain brain;
        protected Example[] exampleBlock;

        protected Matrix<double>[] synapsGradient;
        protected Matrix<double>[] biasGradient;

        protected Matrix<double>[] synapsGradientMomentum;
        protected Matrix<double>[] biasGradientMomentum;
        public int BlocksDone { get; protected set; } = 0;
        public long ExamplesDone { get; protected set; } = 0;

        protected Vector<double> errors;
        int lastErrorPosition = 0;
        public double MeanSquaredError { get; protected set; } = 10000000;

        protected int errorMemoryLenght = 10;
        public int ErrorMemoryLenght
        {
            get
            {
                return errorMemoryLenght;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("momentum", "Momentum cannot be lower than zero");
                momentum = value;
            }
        }

        protected double momentum = 0.1;
        public double Momentum
        {
            get
            {
                return momentum;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("momentum", "Momentum cannot be lower than zero");
                momentum = value;
            }
        }

        protected double accuracy = 10;
        public double Accuracy
        {
            get
            {
                return accuracy;
            }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("accuracy", "Accuracy must be greater than zero");
                accuracy = value;
            }
        }

        public Brain Brain
        {
            get
            {
                return brain;
            }
            set
            {
                brain = value;
                synapsGradientMomentum = brain.GetSynapsGradientFrame();
                biasGradientMomentum = brain.GetBiasGradientFrame();
            }
        }

        protected int exampleBlockSize = 5;
        public int ExampleBlockSize
        {
            get
            {
                return exampleBlockSize;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException("exampleBlockSize");
                }
                exampleBlockSize = value;
                exampleBlock = new Example[exampleBlockSize];
                for (int i = 0; i < exampleBlockSize; i++)
                {
                    exampleBlock[i] = new Example();
                }
            }
        }

        public Func<int, int, (Matrix<double>, Matrix<double>)> DataExtractor { get; set; }

        public Learner()
        {
            lastErrorPosition = 0;
            errors = Vector<double>.Build.Dense(errorMemoryLenght, 1000000);
        }

        public void LearnToGivenError(double error)
        {
            while (LearnOneBlock() > error) { }
        }

        public double LearnGivenData(Matrix<double>[] input, Matrix<double>[] expectedOutput)
        {
            return MeanSquaredError;
        }

        public double LearnOneBlock()
        {
            PrepareOneBlock();
            (Matrix<double>[], Matrix<double>[], double) gradientTouple;
            gradientTouple = brain.CalculateOneGradients(exampleBlock[0].input, exampleBlock[0].output);
            synapsGradient = gradientTouple.Item1;
            biasGradient = gradientTouple.Item2;

            errors[lastErrorPosition] = gradientTouple.Item3;
            MeanSquaredError = errors.Sum() / errorMemoryLenght;
            lastErrorPosition++;
            lastErrorPosition %= errorMemoryLenght;

            for (int i = 1; i < exampleBlockSize; i++)
            {
                gradientTouple = brain.CalculateOneGradients(exampleBlock[i].input, exampleBlock[i].output);
                for (int l = 0; l < brain.TotalSynapsLayers; l++)
                {
                    synapsGradient[l] += gradientTouple.Item1[l];
                    biasGradient[l] += gradientTouple.Item2[l];
                }
            }

            for (int l = 0; l < brain.TotalSynapsLayers; l++)
            {
                synapsGradient[l] /= (double)exampleBlockSize;
                synapsGradientMomentum[l] = synapsGradient[l] / accuracy + synapsGradientMomentum[l] * momentum;

                biasGradient[l] /= (double)exampleBlockSize;
                biasGradientMomentum[l] = biasGradient[l] / accuracy + biasGradientMomentum[l] * momentum;
            }
            brain.ApplyGradients(synapsGradientMomentum, biasGradientMomentum);
            return MeanSquaredError;
        }

        void PrepareOneBlock()
        {
            if (DataExtractor == null)
                throw new NullReferenceException("DataExtractor");

            (Matrix<double>, Matrix<double>) data;
            for (int i = 0; i < exampleBlockSize; i++)
            {
                data = DataExtractor(BlocksDone, i);
                exampleBlock[i].input = data.Item1;
                exampleBlock[i].output = data.Item2;
            }
            BlocksDone++;
            ExamplesDone += exampleBlockSize;
        }


    }
}
