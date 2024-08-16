﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381.LP
{
    public static class BranchAndBound
    {
        public static List<string> Solve(Tableu tableu)
        {
            //List that will store the steps of the algorhitm
            var steps = new List<string>();

            bool isMaximization = tableu.RowNames[0] == "max z";

            //Initialization of the best solution
            double bestSolutionValue = isMaximization ? double.NegativeInfinity : double.PositiveInfinity;
            Dictionary<int, double> bestSolution = null;

            //Priority queue for nodes it is ordered by bound and level
            var nodeQueue = new SortedSet<Node>(Comparer<Node>.Create((a, b) =>
            {
                //Comapres based on the bound and level
                int boundComparison = isMaximization ? b.Bound.CompareTo(a.Bound) : a.Bound.CompareTo(b.Bound);
                if (boundComparison != 0)
                    return boundComparison;
                return a.Level.CompareTo(b.Level); //avoid collisions
            }));

            // LP relaxation of original problem
            steps.AddRange(PrimalSimplex.Solve(tableu));

            //Add OG node to the search tree
            nodeQueue.Add(new Node(tableu, null, 0));


            //Main branch and bound loop
            while (nodeQueue.Count > 0)
            {
                var currentNode = nodeQueue.Min;
                nodeQueue.Remove(currentNode);

                // Solve LP relaxation at the current level
                steps.Add($"Solving LP relaxation at level {currentNode.Level}");
                Tableu currentTableu = currentNode.Tableu;

                var currentSolution = ExtractSolution(currentTableu);
                if (currentSolution == null)
                {
                    steps.Add("Infeasible solution, skipping node.");
                    continue;
                }

                // Check if current solution is a integer solution
                if (IsIntegerSolution(currentSolution))
                {
                    double currentValue = CalculateObjectiveValue(currentSolution, currentTableu);

                    //Update best solution if the current solution is in fact better
                    if (isMaximization ? currentValue > bestSolutionValue : currentValue < bestSolutionValue)
                    {
                        bestSolutionValue = currentValue;
                        bestSolution = currentSolution;
                        steps.Add($"New best integer solution found: {bestSolutionValue}");
                    }
                }
                else
                {
                    // Branch on the first non-integer variable
                    var fractionalIndex = GetFirstFractionalIndex(currentSolution);

                    // Create two new branches
                    var leftTableu = currentTableu.Copy();
                    var rightTableu = currentTableu.Copy();

                    //Create constraints for the newly created branches
                    AddConstraint(leftTableu, fractionalIndex, Math.Floor(currentSolution[fractionalIndex]), true, steps);
                    AddConstraint(rightTableu, fractionalIndex, Math.Ceiling(currentSolution[fractionalIndex]), false, steps);

                    steps.Add($"Branching on variable x{fractionalIndex}");

                    //Add new nodes to the queue for each new branch
                    nodeQueue.Add(new Node(leftTableu, currentNode, currentNode.Level + 1));
                    nodeQueue.Add(new Node(rightTableu, currentNode, currentNode.Level + 1));
                }
            }

            //The best solution found
            if (bestSolution != null)
            {
                steps.Add("Optimal integer solution found: " + bestSolutionValue);
                steps.Add("Solution: " + string.Join(", ", bestSolution.Select(kvp => $"x{kvp.Key} = {kvp.Value}")));
            }
            else
            {
                steps.Add("No feasible integer solution found.");
            }

            return steps;
        }

        private static void AddConstraint(Tableu tableu, int index, double value, bool isUpperBound, List<string> steps)
        {
            // Add new constraint row
            tableu.AddRow(Enumerable.Range(0, tableu.Width - 1)
                .Select(j => (j == index) ? (isUpperBound ? 1.0 : -1.0) : 0.0) // variable selector
                .Append(isUpperBound ? value : -value) // rhs
                .ToArray());
            // if Dual needed 
            if (tableu.Values[tableu.Height - 1, tableu.Width - 1] < 0)
                steps.AddRange(DualSimplex.Solve(tableu));
            // Recalculate the objective value after adding the constraint
            steps.AddRange(PrimalSimplex.Solve(tableu)); // Resolve with the new constraint
        }

        private static Dictionary<int, double> ExtractSolution(Tableu tableu)
        {
            var solution = new Dictionary<int, double>();

            // Go through each row to extract solution
            for (int i = 0; i < tableu.Height; i++)
            {
                // Find coefficient 1
                for (int j = 0; j < tableu.Width - 1; j++)
                {
                    if (Math.Abs(tableu.Values[i, j] - 1.0) < 1e-6)
                    {
                        // Store the variable index, RHS
                        solution[j] = tableu.Values[i, tableu.Width - 1];
                        break;
                    }
                }
            }

            return solution;
        }

        private static bool IsIntegerSolution(Dictionary<int, double> solution)
        {
            //check if the value is a integer
            foreach (var value in solution.Values)
            {
                if (Math.Abs(value - Math.Round(value)) > 1e-6)
                    return false;
            }
            return true;
        }

        private static int GetFirstFractionalIndex(Dictionary<int, double> solution)
        {
            //find non integer value
            foreach (var kvp in solution)
            {
                int index = kvp.Key;
                double value = kvp.Value;

                if (Math.Abs(value - Math.Round(value)) > 1e-6)
                {
                    return index;
                }
            }
            return -1;
        }

        private static double CalculateObjectiveValue(Dictionary<int, double> solution, Tableu tableu)
        {
            double objectiveValue = 0.0;

            // the last row of the tableau contains the objective function coefficients
            for (int i = 0; i < solution.Count; i++)
            {
                if (solution.ContainsKey(i))
                {
                    objectiveValue += solution[i] * tableu.Values[tableu.Height - 1, i];
                }
            }

            return objectiveValue;
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
                Bound = tableu[0, tableu.Width - 1];
            }
        }
    }
}
