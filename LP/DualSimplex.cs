using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381.LP
{
    public static class DualSimplex
    {
        public static List<String> Solve(Tableau tableau)
        {
            var steps = new List<String>() { "Start Dual Simplex" };
            DualFrom(tableau, steps);
            for (int iteration = 0; iteration < 128; iteration++)
            {
                // get pivot row IndexMin(rhs)
                int pivotI = 1; // start after objective row and find min value
                for (int i = 2; i < tableau.Height; i++)
                    if (tableau[i, tableau.Width - 1] < tableau[pivotI, tableau.Width - 1])
                        pivotI = i;

                // break if all rhs are positive
                if (tableau[pivotI, tableau.Width - 1] >= 0.0)
                {
                    steps.Add("All rhs values are positive");
                    break;
                }

                // get pivot column
                int pivotJ = -1;
                double minAbsRatio = double.PositiveInfinity;
                for (int j = 0; j < tableau.Width - 1; j++)
                {
                    double numerator = tableau[0, j],
                           demoninator = tableau[pivotI, j],
                           absRatio = Math.Abs(numerator / demoninator);
                    if (demoninator >= 0) continue;
                    if (absRatio < minAbsRatio)
                    {
                        minAbsRatio = absRatio;
                        pivotJ = j;
                    }
                }

                if (pivotJ == -1)
                {
                    steps.Add($"Infeasible. Ratio Test has no valid minimum.\nrow:{tableau.RowNames[pivotI]}");
                    break;
                }

                // Pivot
                steps.Add(tableau.Pivot(pivotI, pivotJ));
            }
            steps.Add("End Dual Simplex");
            return steps;
        }

        public static (Tableau tableau, bool madeChanges) DualFrom(Tableau tableau, List<string> steps = null)
        {
            bool madeChanges = false;
            for (int j = 0; j < tableau.Width; j++)
            {
                int indexOfNegative1 = -1;
                // indentify basic-like column. remember i of -1
                if (!Enumerable.Range(0, tableau.Height).Skip(1).Select(i => (v: tableau[i, j], i))
                    .All(p => p.v == 0.0 || p.v == -1.0 && indexOfNegative1 == -1 && (indexOfNegative1 = p.i) != -1))
                    continue;
                // Multiply the row by -1
                for (int k = 0; k < tableau.Width; k++)
                    tableau[indexOfNegative1, k] *= -1;
                steps?.Add($"Multiplied row **{tableau.RowNames[indexOfNegative1]}** by -1\nTo make **{tableau.ColumnNames[j]}** basic\n{tableau}");
                madeChanges = true;
            }
            return (tableau, madeChanges);
        }
    }
}
