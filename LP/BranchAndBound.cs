using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381.LP
{
    public static class BranchAndBound
    {
        public static List<string> Solve(Tableau tableau)
        {
            //List that will store the steps of the algorhitm
            var steps = new List<string>();

            bool isMaximization = tableau.RowNames[0] == "max z";

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
            steps.AddRange(PrimalSimplex.Solve(tableau));

            //Add OG node to the search tree
            nodeQueue.Add(new Node(tableau, null, 0));


            //Main branch and bound loop
            while (nodeQueue.Count > 0)
            {
                var currentNode = nodeQueue.Min;

                nodeQueue.Remove(currentNode);

                // Solve LP relaxation at the current level
                steps.Add($"Solving LP relaxation at level {currentNode.Level}");
                Tableau currentTableau = currentNode.Tableau;

                var currentSolution = ExtractSolution(currentTableau);
                if (currentSolution == null)
                {
                    steps.Add("Infeasible solution, skipping node.");
                    continue;
                }

                // Check if current solution is a integer solution
                if (IsIntegerSolution(currentSolution))
                {
                    double currentValue = CalculateObjectiveValue(currentSolution, currentTableau);

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
                    var leftTableau = currentTableau.Copy();
                    var rightTableau = currentTableau.Copy();

                    //Create constraints for the newly created branches
                    AddConstraint(leftTableau, fractionalIndex, Math.Floor(currentSolution[fractionalIndex]), true, steps);
                    AddConstraint(rightTableau, fractionalIndex, Math.Ceiling(currentSolution[fractionalIndex]), false, steps);

                    steps.Add($"Branching on variable x{fractionalIndex}");

                    //Add new nodes to the queue for each new branch
                    nodeQueue.Add(new Node(leftTableau, currentNode, currentNode.Level + 1));
                    nodeQueue.Add(new Node(rightTableau, currentNode, currentNode.Level + 1));
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

        private static void AddConstraint(Tableau tableau, int index, double value, bool isUpperBound, List<string> steps)
        {
            // Add new constraint row
            tableau.AddRow(Enumerable.Range(0, tableau.Width - 1)
                .Select(j => (j == index) ? (isUpperBound ? 1.0 : -1.0) : 0.0) // variable selector
                .Append(isUpperBound ? value : -value) // rhs
                .ToArray());
            // if Dual needed 
            if (tableau[tableau.Height - 1, tableau.Width - 1] < 0)
                steps.AddRange(DualSimplex.Solve(tableau));
            // Recalculate the objective value after adding the constraint
            steps.AddRange(PrimalSimplex.Solve(tableau)); // Resolve with the new constraint
        }

        private static Dictionary<int, double> ExtractSolution(Tableau tableau)
        {
            var solution = new Dictionary<int, double>();

            // Go through each row to extract solution
            for (int i = 0; i < tableau.Height; i++)
            {
                // Find coefficient 1
                for (int j = 0; j < tableau.Width - 1; j++)
                {
                    if (Math.Abs(tableau[i, j] - 1.0) < 1e-6)
                    {
                        // Store the variable index, RHS
                        solution[j] = tableau[i, tableau.Width - 1];
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

        private static double CalculateObjectiveValue(Dictionary<int, double> solution, Tableau tableau)
        {
            double objectiveValue = 0.0;

            // the last row of the tableau contains the objective function coefficients
            for (int i = 0; i < solution.Count; i++)
            {
                if (solution.ContainsKey(i))
                {
                    objectiveValue += solution[i] * tableau[tableau.Height - 1, i];
                }
            }

            return objectiveValue;
        }

        private class Node
        {
            public Tableau Tableau { get; }
            public Node Parent { get; }
            public int Level { get; }
            public double Bound { get; }

            public Node(Tableau tableau, Node parent, int level)
            {
                Tableau = tableau;
                Parent = parent;
                Level = level;
                Bound = tableau[0, tableau.Width - 1];
            }
        }
    }
}
