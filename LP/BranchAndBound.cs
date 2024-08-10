using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381.LP
{
    public static class BranchAndBoundSimplex//Revised
    {
        public static List<string> Solve(ref Tableu tableu)
        {
            var steps = new List<string>();

            
            double bestSolutionValue = double.NegativeInfinity;
            Tableu bestTableu = null;

            var nodeQueue = new SortedSet<Node>(Comparer<Node>.Create((a, b) => a.Bound.CompareTo(b.Bound)));

            // LP relaxation
            steps.AddRange(PrimalSimplex.Solve(ref tableu)); // Ensure PrimalSimplex.Solve uses the same Tableu
            //steps.AddRange(PrimalSimplex.Solve(ref tableu)); taken from knapsack


            nodeQueue.Add(new Node(tableu, null, 0));

            while (nodeQueue.Count > 0)
            {
                var currentNode = nodeQueue.Min;
                nodeQueue.Remove(currentNode);

                // Solve LP relaxation
                steps.Add($"Solving LP relaxation at level {currentNode.Level}");
                Tableu currentTableu = currentNode.Tableu;

                var currentSolution = ExtractSolution(currentTableu);
                if (currentSolution == null)
                {
                    steps.Add("Infeasible solution, skipping node.");
                    continue;
                }

                // Check if integer
                if (IsIntegerSolution(currentSolution))
                {
                    double currentValue = currentSolution.Values.Max(); // Use .Max() to get the maximum value

                    if (currentValue > bestSolutionValue)
                    {
                        bestSolutionValue = currentValue;
                        bestTableu = currentTableu;
                        steps.Add($"New best integer solution found: {bestSolutionValue}");
                    }
                }
                else
                {
                    // !!!Branch on the first non-integer variable
                    var fractionalIndex = GetFirstFractionalIndex(currentSolution);

                    // Two branches
                    var leftTableu = (Tableu)currentTableu.Clone();
                    leftTableu.AddConstraint(fractionalIndex, Math.Floor(currentSolution[fractionalIndex]));

                    var rightTableu = (Tableu)currentTableu.Clone();
                    rightTableu.AddConstraint(fractionalIndex, Math.Ceiling(currentSolution[fractionalIndex]));

                    steps.Add("Branching on variable x" + fractionalIndex);

                    nodeQueue.Add(new Node(leftTableu, currentNode, currentNode.Level + 1));
                    nodeQueue.Add(new Node(rightTableu, currentNode, currentNode.Level + 1));
                }
            }

            if (bestTableu != null)
            {
                steps.Add("Optimal integer solution found: " + bestSolutionValue);
            }
            else
            {
                steps.Add("No feasible integer solution found.");
            }

            return steps;
        }

        public class Tableu : ICloneable
        {
            public double[,] Values { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public double ObjectiveValue { get; set; }

            public object Clone()
            {
                var clonedTableu = new Tableu
                {
                    Width = this.Width,
                    Height = this.Height,
                    Values = (double[,])this.Values.Clone(),
                    ObjectiveValue = this.ObjectiveValue
                };
                return clonedTableu;
            }

            public void AddConstraint(int index, double value)
            {
                // Create a new matrix with an additional row for newconstraint
                double[,] newValues = new double[this.Height + 1, this.Width];

                // Copy existing values
                for (int i = 0; i < this.Height; i++)
                {
                    for (int j = 0; j < this.Width; j++)
                    {
                        newValues[i, j] = this.Values[i, j];
                    }
                }

                // Add new constraint row
                for (int j = 0; j < this.Width - 1; j++)
                {
                    if (j == index)
                    {
                        newValues[this.Height, j] = 1.0; // Add coefficient for the constraint variable
                    }
                    else
                    {
                        newValues[this.Height, j] = 0.0; // Set other coefficients to zero
                    }
                }
                newValues[this.Height, this.Width - 1] = value; // Add RHS value

                // Update dimensions and values
                this.Height++;
                this.Values = newValues;
            }
        }

        private static Dictionary<int, double> ExtractSolution(Tableu tableu)
        {
            var solution = new Dictionary<int, double>();

            // Go through each row
            for (int i = 1; i < tableu.Height; i++)
            {
                // Find coefficient 1
                for (int j = 0; j < tableu.Width - 1; j++)
                {
                    if (tableu.Values[i, j] == 1)
                    {
                        // Store the variable index,RHS
                        solution[j] = tableu.Values[i, tableu.Width - 1];
                        break;
                    }
                }
            }

            return solution;
        }

        private static bool IsIntegerSolution(Dictionary<int, double> solution)
        {
            foreach (var value in solution.Values)
            {
                // Not integer, return false
                if (Math.Abs(value - Math.Round(value)) > 1e-6)
                    return false;
            }

            // integers
            return true;
        }

        private static int GetFirstFractionalIndex(Dictionary<int, double> solution)
        {
            foreach (var kvp in solution)
            {
                int index = kvp.Key;
                double value = kvp.Value;

                // not integer
                if (Math.Abs(value - Math.Round(value)) > 1e-6)
                {
                    return index;  // Return the index of the first fractional value
                }
            }

            return -1;  // All values are integers
        }

        private class Node
        {
            public Tableu Tableu { get; }
            public Node Parent { get; }
            public int Level { get; }
            public double Bound { get; }

            public Node(Tableu tableu, Node parent, int level)
            {
                Tableu = tableu;
                Parent = parent;
                Level = level;
                Bound = tableu.ObjectiveValue;
            }
        }
    }
}
