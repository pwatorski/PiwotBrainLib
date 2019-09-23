﻿using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using MathNet.Numerics.LinearAlgebra;

namespace PiwotBrainLib
{
    class LearningBrain : OpenBrain
    {
       
        public bool ExtractDataOnTheRun = false;
        protected Matrix<double>[] synapsGradient;
        protected Matrix<double>[] biasGradient;
        protected (Matrix<double>[], Matrix<double>[], double) gradientTouple;
        protected Matrix<double>[] synapsGradientMomentum;
        protected Matrix<double>[] biasGradientMomentum;
        protected Matrix<double>[,] learningData;
        public int BlocksDone { get; protected set; } = 0;
        public long ExamplesDone { get; protected set; } = 0;

        protected Vector<double> errors;
        int lastErrorPosition = 0;
        public double MeanSquaredError { get; protected set; } = double.PositiveInfinity;
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
                    throw new ArgumentOutOfRangeException("errorMemoryLenght", "ErrorMemoryLenght cannot be lower than zero");
                errorMemoryLenght = value;
                errors = Vector<double>.Build.Dense(value);
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
                learningData = new Matrix<double>[2, exampleBlockSize];
            }
        }

        public bool AsyncMode { get; set; } = false;

        protected Queue requests = Queue.Synchronized(new Queue());

        public Func<int, long, (Matrix<double>, Matrix<double>)> DataExtractor { get; set; }
        public Action<int, double> BlockDoneAction { get; set; }

        public LearningBrain(int inputNeurons, int hiddenNeurons, int outputNeurons) : base(inputNeurons, hiddenNeurons, outputNeurons)
        {
            SetupLearningBrain();
        }

        public LearningBrain(int inputNeurons, int[] hiddenNeurons, int outputNeurons) : base(inputNeurons, hiddenNeurons, outputNeurons)
        {
            SetupLearningBrain();
        }

        public LearningBrain(BrainCore brainCore) : base(brainCore)
        {
            SetupLearningBrain();
        }

        public LearningBrain(LearningBrain learningBrain) : base(learningBrain)
        {
            SetupLearningBrain();

        }


        public void SetupLearningBrain()
        {
            errors = Vector<double>.Build.Dense(errorMemoryLenght);
            synapsGradientMomentum = GetSynapsGradientFrame();
            biasGradientMomentum = GetBiasGradientFrame();
        }

        public double LearnBlocks(int count)
        {
            if (DataExtractor == null)
                throw new NullReferenceException("DataExtractor cannot be null");
            (Matrix<double>, Matrix<double>) data;
            for (int c = 0; c < count; c++)
            {
                for (int i = 0; i < exampleBlockSize; i++)
                {
                    data = DataExtractor(BlocksDone, ExamplesDone);
                    learningData[0, i] = data.Item1;
                    learningData[1, i] = data.Item2;
                }
                CalculateOneBlock();
            }
            return MeanSquaredError;
        }

        public double LearnBlocks(Matrix<double>[] input, Matrix<double>[] expectedOutput)
        {
            if (input.Length != expectedOutput.Length)
                throw new ArgumentException("Both input and expected output must be of the same lenght.");
            int limit = input.Length / exampleBlockSize + 1;
            int position = 0;
            for (int c = 0; c < limit; c++)
            {
                for (int i = 0; i < exampleBlockSize; i++)
                {
                    learningData[0, 1] = input[position];
                    learningData[1, 1] = expectedOutput[position];
                    position++;
                    if (position >= input.Length)
                        position = 0;
                }
                CalculateOneBlock();
            }
            return MeanSquaredError;
        }

        public double LearnBlocksWhile(Func<int, long, double, bool> conditionFunction)
        {
            while (conditionFunction(BlocksDone, ExamplesDone, MeanSquaredError))
            { 
                for (int i = 0; i < exampleBlockSize; i++)
                {
                    (Matrix<double>, Matrix<double>) data = DataExtractor(BlocksDone, ExamplesDone);
                    learningData[0, 1] = data.Item1;
                    learningData[1, 1] = data.Item2;
                }
                CalculateOneBlock();
            }
            return MeanSquaredError;
        }

        public double LearnBlocksWhile(Matrix<double>[] input, Matrix<double>[] expectedOutput, Func<int, long, double, bool> conditionFunction)
        {
            if (conditionFunction == null)
                throw new ArgumentNullException("conditionFunction");
            if (input.Length != expectedOutput.Length)
                throw new ArgumentException("Both input and expected output must be of the same lenght.");
            int position = 0;
            while(conditionFunction(BlocksDone, ExamplesDone, MeanSquaredError))
            { 
                for (int i = 0; i < exampleBlockSize; i++)
                {
                    learningData[0, 1] = input[position];
                    learningData[1, 1] = expectedOutput[position];
                    position++;
                    if (position >= input.Length)
                        position = 0;
                }
                CalculateOneBlock();
            }
            return MeanSquaredError;
        }


        protected double CalculateOneBlock()
        {
            gradientTouple = CalculateGradients(learningData[0, 0], learningData[1, 0]);
            synapsGradient = gradientTouple.Item1;
            biasGradient = gradientTouple.Item2;
            errors[lastErrorPosition] = gradientTouple.Item3;
            MeanSquaredError = errors.Sum() / errorMemoryLenght;
            lastErrorPosition++;
            lastErrorPosition %= errorMemoryLenght;

            for (int i = 1; i < exampleBlockSize; i++)
            {
                gradientTouple = CalculateGradients(learningData[0,i], learningData[1,i]);
                for (int l = 0; l < TotalSynapsLayers; l++)
                {
                    synapsGradient[l] += gradientTouple.Item1[l];
                    biasGradient[l] += gradientTouple.Item2[l];
                }
            }

            for (int l = 0; l < TotalSynapsLayers; l++)
            {
                synapsGradient[l] /= (double)exampleBlockSize;
                synapsGradientMomentum[l] = synapsGradient[l] / accuracy + synapsGradientMomentum[l] * momentum;

                biasGradient[l] /= (double)exampleBlockSize;
                biasGradientMomentum[l] = biasGradient[l] / accuracy + biasGradientMomentum[l] * momentum;
            }
            ApplyGradients(synapsGradientMomentum, biasGradientMomentum);
            BlocksDone++;
            BlockDoneAction?.Invoke(BlocksDone, MeanSquaredError);
            ExamplesDone += exampleBlockSize;
            return MeanSquaredError;
        }


    }
}

