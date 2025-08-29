using MathNet.Numerics.LinearAlgebra;
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
            const int maxIterations = 128;
            for (int iteration = 0; ; iteration++)
            {
                if (iteration >= maxIterations)
                {
                    steps.Add("Algorithm terminated: Maximum iterations reached.");
                    break;
                }

                var rhs = tableau[null, tableau.Width - 1];
                var minRhs = rhs.Min();
                if (minRhs >= 0.0)
                {
                    steps.Add("All rhs values are positive");
                    break;
                }
                var pi = Array.IndexOf(rhs.ToArray(), minRhs);

                var numerator = tableau[0, tableau.IndicesForVariables];
                var denominator = tableau[pi, tableau.IndicesForVariables];
                var minAbsRatio = double.MaxValue;
                var pj = -1;
                foreach (var j in tableau.IndicesForVariables.Where(j => denominator[j] < 0.0))
                {
                    var absRatio = Math.Abs(numerator[j] / denominator[j]);
                    if (absRatio < minAbsRatio)
                    {
                        minAbsRatio = absRatio;
                        pj = j;
                    }
                }
                if (pj < 0)
                {
                    steps.Add($"Infeasible. Ratio Test has no valid minimum.\nrow:**{tableau.RowNames[pi]}**");
                    break;
                }

                steps.Add(tableau.Pivot(pi, pj));
            }
            steps.Add("End Dual Simplex");
            return steps;
        }

        public static (Tableau tableau, bool madeChanges) DualFrom(Tableau tableau, List<string> steps)
        {
            var excessConstraintIndices = tableau.IndicesForVariables
                .Select(j => tableau.GetBasicVariableI(j, -1.0) ?? -1)
                .Where(i => i >= 0).ToArray();
            if (excessConstraintIndices.Length == 0)
                return (tableau, false);
            tableau[excessConstraintIndices, null] *= -1.0;
            foreach (var i in excessConstraintIndices)
                steps.Add($"{tableau.RowNames[i]} *= -1");
            return (tableau, true);
        }
    }
}
