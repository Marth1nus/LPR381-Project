using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381.LP
{
    public static class BranchAndBoundSimplex
    {
        public static List<string> Solve(ref Tableu tableu)
        {
            var steps = new List<string>();
            double bestSolutionValue = double.NegativeInfinity;
            Tableu bestTableu = null;

            var nodeQueue = new SortedSet<Node>(Comparer<Node>.Create((a, b) => a.Bound.CompareTo(b.Bound)));

            //LP relaxation
            steps.AddRange(PrimalSimplex.Solve(ref tableu));

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
                    double currentValue = currentSolution.Value;

                    if (currentValue > bestSolutionValue)
                    {
                        bestSolutionValue = currentValue;
                        bestTableu = currentTableu;
                        steps.Add($"New best integer solution found: {bestSolutionValue}");
                    }
                }
                else
                {
                    //!!!Branch on the first non-integer variable
                    var fractionalIndex = GetFirstFractionalIndex(currentSolution);

                    // Two branches
                    var leftTableu = currentTableu.Clone();
                    leftTableu.AddConstraint(fractionalIndex, Math.Floor(currentSolution[fractionalIndex]));

                    var rightTableu = currentTableu.Clone();
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

       private static Dictionary<int, double> ExtractSolution(Tableu tableu)
{
    var solution = new Dictionary<int, double>();

    // Go through each row
    for (int i = 1; i < tableu.Height; i++)
    {
        // Find coeff 1
        for (int j = 0; j < tableu.Width - 1; j++)
        {
            if (tableu.Values[i, j] == 1)
            {
                // Store the variable index and RHS
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
        // not integer, return false
        if (Math.Abs(value - Math.Round(value)) > 1e-6)
            return false;
    }

    // All values are integers
    return true;
}

private static int GetFirstFractionalIndex(Dictionary<int, double> solution)
{
    foreach (var kvp in solution)
    {
        int index = kvp.Key;
        double value = kvp.Value;

        // value is not an integer
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
