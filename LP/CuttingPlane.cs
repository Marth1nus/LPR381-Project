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

                tableau.AddColumn(new double[tableau.Height]);
                tableau.AddRow(new double[tableau.Width]);
                int j = 0;
                for (; j < tableau.Width - 2; j++)
                    tableau[tableau.Height - 1, j] = Math.Floor(tableau[iFractionalRow, j]) - tableau[iFractionalRow, j];
                for (; j < tableau.Width - 1; j++)
                    tableau[tableau.Height-1, tableau.Width - 2] = 1;
                for (; j < tableau.Width; j++)
                    tableau[tableau.Height-1, j] = Math.Floor(tableau[iFractionalRow, j]) - tableau[iFractionalRow, j];

                steps.Add($"Add Fractional cutting constraint\n\n{tableau}");
                steps.AddRange(DualSimplex.Solve(tableau));
                steps.AddRange(PrimalSimplex.Solve(tableau));
            }
            if (!first)
                steps.Add("End Cutting Plane");
            return steps;
        }
    }
}
