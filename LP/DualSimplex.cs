using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPR381.LP.Algorithm
{
    public static class DualSimplex

    {
        public static List<String> Solve(ref Tableu tableu)
        {
            var steps = new List<String>();

            // look for excesses with negatve basic variable form and multiply rows by -1
            for (int j = 0; j < tableu.Width; j++)
            {
                if (!tableu.ColumnNames[j].StartsWith("e"))
                    continue;
                int iNegative1 = -1;
                for (int i = 0; i < tableu.Height; i++)
                {
                    if (tableu.Values[i, j] == 0)
                        continue;
                    if (tableu.Values[i, j] == -1 && iNegative1 == -1)
                    {
                        iNegative1 = i;
                        continue;
                    }
                    iNegative1 = -1;
                    break;
                }
                if (iNegative1 == -1)
                    continue;
                for (int k = 0; k < tableu.Width; k++)
                    tableu.Values[iNegative1, k] *= -1;
            }
            while (true)
            {
                // get pivot row minI(rhs)
                int pivotI = 1, rhsJ = tableu.Width - 1;
                for (int i = 1 + pivotI; i < tableu.Height; i++)
                    if (tableu.Values[i, rhsJ] < tableu.Values[pivotI, rhsJ]) // min rhs
                        pivotI = i;

                // break if all rhs are positive
                if (tableu.Values[pivotI, rhsJ] >= 0)
                    break;

                // get pivot column
                int pivotJ = -1;
                double minRatio = double.PositiveInfinity;
                for (int j = 0; j < tableu.Width; j++)
                {
                    var ratio = Math.Abs(tableu.Values[0, j] /  tableu.Values[pivotI, j]);
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        pivotI = j;
                    }
                }

                // Pivot
                steps.Add(tableu.Pivot(pivotI, pivotJ));
            }

            return steps;
        }
    }
}
