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

        public static (Tableau tableau, bool madeChanges) DualFrom(Tableau tableau, List<string> steps)
        {
            bool madeChanges = false;
            for (int j = 0; j < tableau.Width; j++)
            {
                int Negative1I = -1;
                // indentify basic-like column. remember i of -1
                for (int i = 1; i < tableau.Height; i++)
                {
                    if (tableau[i, j] == -1)
                    {
                        Negative1I = i;
                    }
                    else if (tableau[i, j] != 0)
                    {
                        Negative1I = -1; // non basic-like column
                        break;
                    }
                }
                if (Negative1I < 0)
                    continue; // non basic-like column found
                // Multiply the row by -1
                for (int k = 0; k < tableau.Width; k++)
                    tableau[Negative1I, k] *= -1;
                steps.Add($"Multiplied row **{tableau.RowNames[Negative1I]}** by -1\nTo make **{tableau.ColumnNames[j]}** basic\n{tableau}");
                madeChanges = true;
            }
            return (tableau, madeChanges);
        }
    }
}
