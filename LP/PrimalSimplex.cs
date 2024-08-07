using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR381.LP
{
    public static class PrimalSimplex
    {
        // Solve method implements Primal Simplex algorithm
        public static List<string> Solve(ref Tableu tableu)
        {
            var steps = new List<string>(); 
            while (true)
            {
                //  Check for optimality by looking at the objective row
                int pivotColumn = -1;
                for (int j = 0; j < tableu.Width - 1; j++)
                {
                    if (tableu.Values[0, j] < 0)
                    {
                        pivotColumn = j;
                        break;
                    }
                }
                if (pivotColumn == -1)
                {
                    // If no negative entries in objective row, optimal solution is found
                    steps.Add(ConstructSolution(tableu));
                    return steps;
                }

                // Determine the pivot row using the minimum ratio test
                double minRatio = double.PositiveInfinity;
                int pivotRow = -1;
                for (int i = 1; i < tableu.Height; i++)
                {
                    if (tableu.Values[i, pivotColumn] > 0)
                    {
                        double ratio = tableu.Values[i, tableu.Width - 1] / tableu.Values[i, pivotColumn];
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            pivotRow = i;
                        }
                    }
                }
                if (pivotRow == -1)
                {
                    // If no valid pivot row is found, the solution is unbounded
                    steps.Add("Unbounded solution");
                    return steps;
                }

                // Perform the pivot operation
                steps.Add(tableu.Pivot(pivotRow, pivotColumn));
            }
        }

        // Constructs and returns a string representation of the optimal solution
        private static string ConstructSolution(Tableu tableu)
        {
            var result = new StringBuilder();
            result.AppendLine("Optimal Solution:");
            double optimalValue = -tableu.Values[0, tableu.Width - 1]; // Extract optimal value from RHS of objective row
            result.AppendLine($"Optimal Value: {optimalValue:F3}");

            // Extract values of decision variables from the final tableau
            for (int j = 0; j < tableu.Width - 1; j++)
            {
                string varName = tableu.ColumnNames[j];
                double varValue = 0;
                for (int i = 1; i < tableu.Height; i++)
                {
                    if (tableu.RowNames[i].StartsWith("c") && tableu.Values[i, j] == 1)
                    {
                        varValue = tableu.Values[i, tableu.Width - 1];
                        break;
                    }
                }
                result.AppendLine($"{varName} = {varValue:F3}");
            }
            return result.ToString();
        }
    }
}
