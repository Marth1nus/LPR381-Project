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
        public static List<String> Solve(Tableau tableau)
        {
            var steps = new List<String>();
            steps.AddRange(PrimalSimplex.Solve(tableau));
            bool first = true;
            
            for (int iFractionalRow = 1; iFractionalRow < tableau.Height; iFractionalRow++)
            {
                var value = tableau[iFractionalRow, tableau.Width - 1];
                if (value == Math.Floor(value))
                    continue;

                if (first)
                {
                    steps.Add("Start Cutting Plane");
                    first = false;
                }

                tableau.AddRow(Enumerable.Range(0, tableau.Width).Select(j =>
                    Math.Floor(tableau[iFractionalRow, j]) - tableau[iFractionalRow, j]).ToArray());
                tableau.AddColumn(Enumerable.Repeat(0.0, tableau.Height - 1).Append(1.0).ToArray());

                steps.Add($"Add Fractional cutting constraint\n\n{tableau}");
                if (tableau[tableau.Height - 1, tableau.Width - 1] < 0)
                    steps.AddRange(DualSimplex.Solve(tableau));
                steps.AddRange(PrimalSimplex.Solve(tableau));
            }
            if (!first)
                steps.Add("End Cutting Plane");
            return steps;
        }
    }
}
