using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace LPR381.LP
{
    public static class CuttingPlane
    {
        public static List<String> Solve(Tableu tableu)
        {
            var steps = new List<String>();
            steps.AddRange(PrimalSimplex.Solve(ref tableu));
            bool first = true;
            
            for (int iFractionalRow = 1; iFractionalRow < tableu.Height; iFractionalRow++)
            {
                var value = tableu.Values[iFractionalRow, tableu.Width - 1];
                if (value == Math.Floor(value))
                    continue;

                if (first)
                {
                    steps.Add("Start Cutting Plane");
                    first = false;
                }

                tableu.AddColumn(new double[tableu.Height]);
                tableu.AddRow(new double[tableu.Width]);
                int j = 0;
                for (; j < tableu.Width - 2; j++)
                    tableu.Values[tableu.Height - 1, j] = Math.Floor(tableu.Values[iFractionalRow, j]) - tableu.Values[iFractionalRow, j];
                for (; j < tableu.Width - 1; j++)
                    tableu.Values[tableu.Height-1, tableu.Width - 2] = 1;
                for (; j < tableu.Width; j++)
                    tableu.Values[tableu.Height-1, j] = Math.Floor(tableu.Values[iFractionalRow, j]) - tableu.Values[iFractionalRow, j];

                steps.Add($"Add Fractional cutting constraint\n\n{tableu}");
                steps.AddRange(DualSimplex.Solve(ref tableu));
            }
            if (!first)
                steps.Add("End Cutting Plane");
            return steps;
        }
    }
}
