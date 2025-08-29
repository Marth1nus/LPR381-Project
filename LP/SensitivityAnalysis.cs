using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using System.Text;

namespace LPR381.LP
{
    public class SensitivityAnalysis
    {

        /* 
          1. [x] Display the range of a selected Non-Basic Variable.
          2. [ ] Apply and display a change of a selected Non-Basic Variable.
          3. [x] Display the range of a selected Basic Variable.
          4. [ ] Apply and display a change of a selected Basic Variable.
          5. [x] Display the range of a selected constraint right-hand-side value.
          6. [ ] Apply and display a change of a selected constraint right-hand - side value.
          7. [?] Display the range of a selected variable in a Non-Basic Variable column.
          8. [ ] Apply and display a change of a selected variable in a Non-Basic Variable column.
          9. [ ] Add a new activity to an optimal solution.
         10. [ ] Add a new constraint to an optimal solution.
         11. [x] Display the shadow prices.
         12. [ ] Duality:
             1. [ ] Apply Duality to the programming model.
             2. [ ] Solve the Dual Programming Model.
             3. [ ] Verify whether the Programming Model has Strong or Weak Duality.
        */

        public static List<String> Solve(Tableau tableau)
        {
            var steps = new List<String>() { "Start Sensitivity Analysis" };
            if (!EnsureOptimal(tableau, steps))
                return steps;

            // Basic + Non-Basic Variables
            steps.Add($"Non-Basic Variables : [{String.Join(", ", tableau./*   */IndicesForBasicVariables.Select(j => tableau.ColumnNames[j]))}]  \n" +
                      $"Basic Variables     : [{String.Join(", ", tableau./**/IndicesForNonBasicVariables.Select(j => tableau.ColumnNames[j]))}]");
            steps.AddRange(SolveDisplayObjectiveRanges(tableau, tableau.IndicesForNonBasicVariables)); // (1. [x])
            steps.AddRange(SolveDisplayObjectiveRanges(tableau, tableau.IndicesForBasicVariables)); // (3. [x])
            steps.AddRange(SolveDisplayRhsRanges(tableau)); // (5. [x])
            steps.AddRange(SolveDisplayNonBasicRanges(tableau)); // (7. [?])
            steps.AddRange(SolveDisplayShadowPrices(tableau)); // (11. [x])

            // More code here <3
            //
            steps.AddRange(SolveDisplayNonBasicRanges(tableau, new[] { Array.IndexOf(tableau.ColumnNames, "s3") }));
            //

            steps.Add("End Sensitivity Analysis");
            return steps;
        }

        public static List<String> SolveDisplayObjectiveRanges(Tableau tableau, IEnumerable<int> variableIndices = null)
        {
            variableIndices = variableIndices ?? tableau.IndicesForVariables;
            var steps = new List<String>();
            if (!EnsureOptimal(tableau, steps))
                return steps;
            var variableRanges = GetObjectiveRanges(tableau, variableIndices);
            var /*    */ rangesBasic = (/**/"Basic", variableRanges.Where(x => tableau.IsBasicVariable(x.j)).ToArray());
            var /* */ rangesNonBasic = ("Non-Basic", variableRanges.Where(x => !tableau.IsBasicVariable(x.j)).ToArray());
            var /*      */ rangesAll = (/*  */"All", variableRanges);
            var groups = /*    */ rangesBasic.Item2.Length == 0 ? new[] { /* */ rangesNonBasic }
                       : /* */ rangesNonBasic.Item2.Length == 0 ? new[] { /*    */ rangesBasic }
                                                                : new[] { rangesBasic, rangesNonBasic, rangesAll };
            foreach (var (groupName, group) in groups)
            {
                if (group.Length == 0)
                    continue;
                var markdownTable = MarkdownTable(
                    headers: "Name|isBasic|Low|Current|High".Split('|'),
                    table: group.Select(g => new[]
                    {
                        g.name /*    */.ToString(),
                        g.isBasic /* */.ToString(),
                        g.low /*     */.ToString(),
                        g.current /* */.ToString(),
                        g.high /*    */.ToString(),
                    }).ToArray());
                var s = group.Length > 1 ? "s" : "";
                steps.Add($"Range{s} for {groupName} variable coefficient{s}\n\n{markdownTable}");
            }
            return steps;
        }

        public static List<String> SolveApplyDisplayObjective(Tableau tableau, IEnumerable<int> variableIndices = null)
        {
            variableIndices = variableIndices ?? tableau.IndicesForVariables;
            var steps = new List<String>();
            if (!EnsureOptimal(tableau, steps))
                return steps;
            var newTableau = ApplyDisplayObjective(tableau, variableIndices);

            return steps;
        }

        public static List<String> ApplyDisplayObjective(Tableau optimalTableau, IEnumerable<int> variableIndices = null)
        {
            var steps = new List<String>();

            var initialTableau = optimalTableau.InitialTableau ?? optimalTableau;
            var B = initialTableau[optimalTableau.IndicesForConstraints, optimalTableau.IndicesForBasicVariables];
            var BInverse = B.Inverse();
            var cBv = initialTableau[0, optimalTableau.IndicesForBasicVariables];


            return steps;
        }

        public static List<String> SolveApplyDisplayRHS(Tableau tableau, int constraintRow, double newValue)
        {
            //code here
            return null;
        }

        public static Tableau ChangeRhsValue(Tableau optimalTableau, int constraintRow, double newValue)
        {
            // 1. Create a deep copy of the tableau to avoid modifying the original
            var newTableau = optimalTableau.Copy();

            // This line updates a single cell (row, column) with a single double value.
            newTableau[constraintRow, newTableau.Width - 1] = newValue;

            //make optimal again if needed
            
            return newTableau;
        }

        public static List<String> SolveDisplayRhsRanges(Tableau tableau, IEnumerable<int> constaintIndices = null)
        {
            constaintIndices = constaintIndices ?? tableau.IndicesForConstraints;
            var steps = new List<String>();
            if (!EnsureOptimal(tableau, steps))
                return steps;
            var rhsRanges = GetRhsRanges(tableau, constaintIndices);
            if (rhsRanges.Length == 0)
            {
                steps.Add("No constraints found");
                return steps;
            }
            var markdownTable = MarkdownTable(
                headers: "Name|Low|Current|High".Split('|'),
                table: rhsRanges.Select(r => new[]
                {
                    r.name /*    */.ToString(),
                    r.low /*     */.ToString(),
                    r.current /* */.ToString(),
                    r.high /*    */.ToString(),
                }).ToArray());
            var s = rhsRanges.Length > 1 ? "s" : "";
            steps.Add($"Range{s} for rhs coefficient{s}\n\n{markdownTable}");
            return steps;
        }

        public static List<String> SolveDisplayNonBasicRanges(Tableau tableau, IEnumerable<int> variableIndices = null)
        {
            var steps = new List<String>();
            if (!EnsureOptimal(tableau, steps))
                return steps;
            variableIndices = variableIndices ?? tableau.IndicesForNonBasicVariables;
            var nonBasicRanges = GetNonBasicRanges(tableau, variableIndices);
            if (nonBasicRanges.Length == 0)
            {
                steps.Add("No constraints found");
                return steps;
            }
            var markdownTable = MarkdownTable(
                headers: "Name|Low|Current|High".Split('|'),
                table: nonBasicRanges.Select(r => new[]
                {
                    r.name /*    */.ToString(),
                    r.low /*     */.ToString(),
                    r.current /* */.ToString(),
                    r.high /*    */.ToString(),
                }).ToArray());
            var s = nonBasicRanges.Length > 1 ? "s" : "";
            steps.Add($"Range{s} for non-basic value{s}\n\n{markdownTable}");
            return steps;
        }

        public static List<String> SolveDisplayShadowPrices(Tableau optimalTableau)
        {
            var steps = new List<String>();
            if (!EnsureOptimal(optimalTableau, steps))
                return steps;
            var initialTableau = optimalTableau.InitialTableau ?? optimalTableau;
            var B = initialTableau[optimalTableau.IndicesForConstraints, optimalTableau.IndicesForBasicVariables];
            var BInverse = B.Inverse();
            var cBv = initialTableau[0, optimalTableau.IndicesForBasicVariables];
            var shadowPrices = cBv * BInverse;

            var markdownTable = MarkdownTable(
                headers: optimalTableau.IndicesForBasicVariables.Select(j => optimalTableau.ColumnNames[j]).Prepend("").ToArray(),
                table: new[]
                {
                    cBv /*          */.Select(c=>c.ToString()).Prepend("c" /*            */).ToArray(),
                    shadowPrices /* */.Select(c=>c.ToString()).Prepend("Shadow Price" /* */).ToArray(),
                });
            steps.Add($"Shadow Prices\n\n{markdownTable}");
            return steps;
        }

        private static bool EnsureOptimal(Tableau tableau, List<String> steps)
        {
            if (tableau.IsOptimal)
                return true;
            steps.Add("Tableau is not Optimal  \nAttempting Cutting Plane Solve");
            var attemptTableau = tableau.Copy();
            var cuttingPlaneSteps = CuttingPlane.Solve(attemptTableau);
            if (attemptTableau.IsOptimal)
            {
                tableau.Assign(attemptTableau);
                steps.AddRange(cuttingPlaneSteps);
                return true;
            }
            steps.Add("Tableau is still not Optimal  \nAttempting Branch&Bound Solve");
            attemptTableau = tableau.Copy();
            var branchAndBoundSteps = BranchAndBound.Solve(attemptTableau);
            if (tableau.IsOptimal)
            {
                tableau.Assign(attemptTableau);
                steps.AddRange(branchAndBoundSteps);
                return true;
            }
            steps.Add("Sensitivity Analysis requires an optimal tableau");
            steps.Add("End Sensitivity Analysis");
            return false;
        }
                
        private static (int j, string name, bool isBasic, double low, double current, double high)[] GetObjectiveRanges(Tableau optimalTableau, IEnumerable<int> variableIndices)
        {
            var initialTableau = optimalTableau.InitialTableau ?? optimalTableau;
            var B = initialTableau[optimalTableau.IndicesForConstraints, optimalTableau.IndicesForBasicVariables];
            var BInverse = B.Inverse();
            var cBv = initialTableau[0, optimalTableau.IndicesForBasicVariables];
            return variableIndices.Select(j =>
            {
                var isBasic /* */ = optimalTableau.IsBasicVariable(j);
                var name /*    */ = optimalTableau.ColumnNames[j];
                var current /* */ = initialTableau[0, j];
                double low, high;
                if (isBasic) /* Basic */
                {
                    var basicIndices = optimalTableau.IndicesForBasicVariables.ToArray();
                    var nonBasicIndices = optimalTableau.IndicesForNonBasicVariables.ToArray();

                    var k = Array.IndexOf(basicIndices, j);
                    var A = initialTableau[optimalTableau.IndicesForConstraints, nonBasicIndices];
                    var r = nonBasicIndices.Select((jn, idx) =>
                    {
                        var aj = initialTableau[optimalTableau.IndicesForConstraints, jn];
                        return initialTableau[0, jn] - cBv * (BInverse * aj);
                    }).ToArray();

                    var deltaMin = double.NegativeInfinity;
                    var deltaMax = double.PositiveInfinity;
                    for (int idx = 0; idx < nonBasicIndices.Length; idx++)
                    {
                        var jn = nonBasicIndices[idx];
                        var aj = initialTableau[optimalTableau.IndicesForConstraints, jn];
                        var u = (BInverse * aj)[k];
                        var rj = r[idx];
                        if (Math.Abs(u) < 1e-12) // u == 0
                            continue;
                        var bound = rj / u;
                        if (u < 0.0)
                            deltaMin = Math.Max(deltaMin, bound);
                        else
                            deltaMax = Math.Min(deltaMax, bound);
                    }
                    low = current + deltaMin;
                    high = current + deltaMax;
                }
                else /* Non Basic */
                {
                    var aj = initialTableau[optimalTableau.IndicesForConstraints, j];
                    var rj = current - cBv * (BInverse * aj);
                    low = double.NegativeInfinity;
                    high = rj < 0.0 ? current - rj
                                    : current;
                }
                return (j, name, isBasic, low, current, high);
            }).ToArray();
        }
        
        private static (int i, string name, double low, double current, double high)[] GetRhsRanges(Tableau optimalTableau, IEnumerable<int> constraintIndices = null)
        {
            constraintIndices = constraintIndices ?? optimalTableau.IndicesForConstraints;
            var initialTableau = optimalTableau.InitialTableau ?? optimalTableau;
            var B = initialTableau[initialTableau.IndicesForConstraints, optimalTableau.IndicesForBasicVariables];
            var BInverse = B.Inverse();
            var b = initialTableau[initialTableau.IndicesForConstraints, initialTableau.Width - 1];
            var xB = BInverse * b;
            return constraintIndices.Select(i =>
            {
                var name = optimalTableau.RowNames[i];
                var current = initialTableau[i, initialTableau.Width - 1];
                double low, high;
                var y = BInverse.Column(i - 1); // i-1 because BInverse does not include the objective row
                var deltaMin = double.NegativeInfinity;
                var deltaMax = double.PositiveInfinity;
                for (int k = 0; k < y.Count; k++)
                {
                    var yk = y[k];
                    if (Math.Abs(yk) < 1e-12) // yk == 0
                        continue;
                    var xBk = xB[k];
                    var bound = -xBk / yk;
                    if (yk > 0.0)
                        deltaMin = Math.Max(deltaMin, bound);
                    else
                        deltaMax = Math.Min(deltaMax, bound);
                }
                if (deltaMin > deltaMax)
                    low = high = double.NaN;
                else
                {
                    low = current + deltaMin;
                    high = current + deltaMax;
                }
                return (i, name, low, current, high);
            }).ToArray();
        }

        private static (int j, string name, double low, double current, double high)[] GetNonBasicRanges(Tableau optimalTableau, IEnumerable<int> variableIndices)
        {
            var initialTableau = optimalTableau.InitialTableau ?? optimalTableau;
            var nonBasicIndices = optimalTableau.IndicesForNonBasicVariables.ToArray();
            var B = initialTableau[initialTableau.IndicesForConstraints, optimalTableau.IndicesForBasicVariables];
            var BInverse = B.Inverse();
            var b = initialTableau[initialTableau.IndicesForConstraints, initialTableau.Width - 1];
            var N = initialTableau[initialTableau.IndicesForConstraints, optimalTableau.IndicesForNonBasicVariables];
            var xB = BInverse * b;
            return variableIndices.Where(optimalTableau.IsNonBasicVariable).Select(j =>
            {
                var name = initialTableau.ColumnNames[j];
                var current = 0.0;
                var IInN= Array.IndexOf(nonBasicIndices, j);
                var Ncol = initialTableau[optimalTableau.IndicesForConstraints, j];
                var u = BInverse * Ncol;

                var maxIncrease = double.PositiveInfinity;
                for (int i = 0; i < xB.Count; i++)
                {
                    if (u[i] > 1e-12) // only positive entries limit the increase
                        maxIncrease = Math.Min(maxIncrease, xB[i] / u[i]);
                }

                var low = 0.0;
                var high = maxIncrease;

                return (j, name, low, current, high);
            }).ToArray();
        }

        private static string MarkdownTable(string[] headers, string[][] table, int decimalLength = 3)
        {
            var sb = new StringBuilder();
            var height = table.Length + 2;
            var width = headers.Length;

            string formatDouble(double d) => Tableau.FormatDouble(d, 0, decimalLength);
            for (int i = 0; i<table.Length; i++)
                for (int j = 0; j < table[i].Length; j++)
                    if (double.TryParse(table[i][j], out var d))
                        table[i][j] = formatDouble(d);

            var columnWidth = table.Prepend(headers).Max(row => row.Max(cell => cell.Length));
            var aligns = Enumerable.Repeat("-:".PadLeft(columnWidth, '-'), width).ToArray();
            var capacity = height * (width * ("| ".Length + columnWidth + " ".Length) + "|\n".Length);
            sb.EnsureCapacity(capacity);

            foreach (var row in table.Prepend(aligns).Prepend(headers))
            {
                foreach (var cell in row)
                {
                    sb.Append("| ");
                    sb.Append(cell.PadLeft(columnWidth));
                    sb.Append(" ");
                }
                sb.Append("|\n");
            }
            sb.Length--; // remove last \n
            return sb.ToString();
        }
    }
}
