using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPR381.LP
{
    public static class DualSimplex
    {
        public static List<String> Solve(Tableau tableau)
        {
            var steps = new List<String>();
            DualFrom(tableau, steps);
            bool first = true;
            while (true)
            {
                // get pivot row IndexMin(rhs)
                int pivotI = 1;
                for (int i = 1 + pivotI; i < tableau.Height; i++)
                    if (tableau[i, tableau.Width - 1] < tableau[pivotI, tableau.Width - 1])
                        pivotI = i;

                // break if all rhs are positive
                if (tableau[pivotI, tableau.Width - 1] >= 0)
                    break;

                if (first)
                {
                    steps.Add("Start Dual Simplex");
                    first = false;
                }

                // get pivot column
                int pivotJ = -1;
                double minRatio = double.PositiveInfinity;
                for (int j = 0; j < tableau.Width - 1; j++)
                {
                    var ratio = Math.Abs(tableau[0, j] / tableau[pivotI, j]);
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        pivotI = j;
                    }
                }

                if (pivotJ == -1)
                {
                    steps.Add($"Infeasible. Ratio Test has no valid minimum.\nrow:{tableau.RowNames[pivotI]}\n\n{tableau}");
                    break;
                }

                // Pivot
                steps.Add(tableau.Pivot(pivotI, pivotJ));
            }

            if (!first)
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
                steps?.Add($"Multiply row {indexOfNegative1} by -1\n{tableau}");
                // Multiply the row by -1
                for (int k = 0; k < tableau.Width; k++)
                    tableau[indexOfNegative1, k] *= -1;
            }
            return (tableau, madeChanges);
        }
    }
}
