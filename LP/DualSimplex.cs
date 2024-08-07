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
        public static List<String> Solve(ref Tableu tableu)
        {
            var steps = new List<String>();
            bool first = true;
            while (true)
            {
                // get pivot row IndexMin(rhs)
                int pivotI = 1;
                for (int i = 1 + pivotI; i < tableu.Height; i++)
                    if (tableu.Values[i, tableu.Width - 1] < tableu.Values[pivotI, tableu.Width - 1])
                        pivotI = i;

                // break if all rhs are positive
                if (tableu.Values[pivotI, tableu.Width - 1] >= 0)
                {
                    if (!first)
                        steps.Add("End Dual Simplex");
                    break;
                }

                if (first)
                {
                    steps.Add("Start Dual Simplex");
                    first = false;
                }

                // get pivot column
                int pivotJ = -1;
                double minRatio = double.PositiveInfinity;
                for (int j = 0; j < tableu.Width; j++)
                {
                    var ratio = Math.Abs(tableu.Values[0, j] / tableu.Values[pivotI, j]);
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        pivotI = j;
                    }
                }

                // Pivot
                steps.Add(tableu.Pivot(pivotI, pivotJ));
            }

            steps.AddRange(PrimalSimplex.Solve(ref tableu));

            return steps;
        }
    }
}
