using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;

namespace LPR381.LP
{
    public static class CuttingPlane
    {
        public static List<String> Solve(Tableau tableau)
        {
            var steps = new List<String>() { "Start Cutting Plane" };
            for (int iteration = 0; iteration < 128; iteration++)
            {
                steps.AddRange(DualSimplex.Solve(tableau));
                steps.AddRange(PrimalSimplex.Solve(tableau));
                if (!tableau.IsPrimalOptimal)
                {
                    steps.Add("Relaxed Primal is not optimal.");
                    break;
                }

                int fractionI = -1,
                    fractionJ = -1;
                double fractionDistanceFromNearestHalf = 1.0;
                for (int j = 0; j < tableau.Width - 1; j++)
                {
                    if (!(tableau.ColumnRestrictions[j] == "bin" ||
                          tableau.ColumnRestrictions[j] == "int")) 
                        continue;
                    var optionalI = tableau.GetBasicVariableI(j);
                    if (!optionalI.HasValue)
                        continue;
                    var i = optionalI.Value;
                    var value = tableau[i, tableau.Width - 1];
                    if (value == Math.Floor(value))
                        continue; // already integer
                    var valueDistanceFromNearestHalf = Math.Abs(value - Math.Floor(value) - 0.5);
                    if (valueDistanceFromNearestHalf < fractionDistanceFromNearestHalf)
                    {
                        fractionI = i;
                        fractionJ = j;
                        fractionDistanceFromNearestHalf = valueDistanceFromNearestHalf;
                    }
                }
                if (fractionJ < 0)
                {
                    steps.Add("All integers constraints satisfied");
                    break;
                }

                tableau.AddColumn(new double[tableau.Height], $"s{tableau.Height}", "+");
                var newRow = new double[tableau.Width];
                for (int j = 0; j < tableau.Width; j++)
                {
                    newRow[j] = Math.Floor(tableau[fractionI, j]) - tableau[fractionI, j];
                }
                newRow[tableau.Width - 2] = 1;
                tableau.AddRow(newRow, $"c{tableau.Height}");

                steps.Add($"Cut on **{tableau.ColumnNames[fractionJ]}**={tableau[fractionI, tableau.Width - 1]}\n\n{tableau}");
            }
            steps.Add("End Cutting Plane");
            return steps;
        }
    }
}
