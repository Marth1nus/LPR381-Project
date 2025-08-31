using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace LPR381.LP
{
    public class SensitivityAnalysis
    {

        /* 
          1. [x] Display the range of a selected Non-Basic Variable.
          2. [?] Apply and display a change of a selected Non-Basic Variable.
          3. [x] Display the range of a selected Basic Variable.
          4. [?] Apply and display a change of a selected Basic Variable.
          5. [x] Display the range of a selected constraint right-hand-side value.
          6. [?] Apply and display a change of a selected constraint right-hand - side value.
          7. [x] Display the range of a selected variable in a Non-Basic Variable column.
          8. [?] Apply and display a change of a selected variable in a Non-Basic Variable column.
          9. [x] Add a new activity to an optimal solution.
         10. [x] Add a new constraint to an optimal solution.
         11. [x] Display the shadow prices.
         12. [x] Duality:
             1. [x] Apply Duality to the programming model.
             2. [x] Solve the Dual Programming Model.
             3. [x] Verify whether the Programming Model has Strong or Weak Duality.
        */

        public static List<String> Solve(Tableau tableau)
        {
            var steps = new List<String>() { "Start Sensitivity Analysis" };
            if (!EnsureOptimal(tableau, steps))
            {
                steps.Add("End Sensitivity Analysis");
                return steps;
            }

            steps.Add($"Initial Tableau\n\n{tableau.InitialTableau ?? tableau}");
            steps.Add($"Optimal Tableau\n\n{tableau}");

            // Basic + Non-Basic Variables
            {
                var /*    */ basicVariableNames = tableau./*    */ IndicesForBasicVariables.Select(j => tableau.ColumnNames[j]).ToArray();
                var /* */ nonBasicVariableNames = tableau./* */ IndicesForNonBasicVariables.Select(j => tableau.ColumnNames[j]).ToArray();
                var width = Math.Max(basicVariableNames.Length, nonBasicVariableNames.Length);
                steps.Add(MarkdownTable(
                    headers: Enumerable.Repeat("", width).Prepend("Type").ToArray(),
                    table: new[]
                    {
                        /* */nonBasicVariableNames.Concat(Enumerable.Repeat("", width - /* */nonBasicVariableNames.Length)).Prepend(/* */"Non-Basic").ToArray(),
                        /*    */basicVariableNames.Concat(Enumerable.Repeat("", width - /*    */basicVariableNames.Length)).Prepend(/*     */"Basic").ToArray(),
                    }
                ));
            }

            steps.AddRange(SolveDisplayObjectiveRanges(tableau, tableau.IndicesForVariables)); // (1. [x]) (3. [x])
            steps.AddRange(SolveDisplayRhsRanges(tableau)); // (5. [x])
            steps.AddRange(SolveDisplayNonBasicRanges(tableau)); // (7. [?])
            steps.AddRange(SolveDisplayShadowPrices(tableau)); // (11. [x])

            steps.Add("End Sensitivity Analysis");
            return steps;
        }
        
        public static List<String> SolveApplyChangeObjectiveRow /*   */ (Tableau optimalTableau, int j, double newValue)
        {
            var steps = new List<String>();
            if (!EnsureOptimal(optimalTableau, steps))
                return steps;
            var i = 0; // objective row
            var initialTableau = (optimalTableau.InitialTableau ?? optimalTableau).Copy();
            var currentValue = initialTableau[i, j];
            var range = GetObjectiveRanges(optimalTableau, new[] { j }).FirstOrDefault();
            steps.Add($"{range.current:0.###}{initialTableau.ColumnNames[j]} can range between [{range.low:0.###}{initialTableau.ColumnNames[j]}, {range.high:0.###}{initialTableau.ColumnNames[j]}]");
            if (range.low <= newValue && newValue <= range.high)
            {
                steps.Add($"\n {newValue:0.###} does not change the optimal solution");
                initialTableau[i, j] = newValue;
                EnsureOptimal(initialTableau, new List<String>()); // TODO: replace with math version;
                optimalTableau.Assign(initialTableau);
            }
            else
            {
                steps.Add($"\n {newValue:0.###} requires re-optimization");
                initialTableau[i, j] = newValue;
                EnsureOptimal(initialTableau, steps);
                optimalTableau.Assign(initialTableau);
            }
            steps.Add($"New Optimal Tableau\n\n{optimalTableau}");
            return steps;
        }
        public static List<String> SolveApplyChangeRHSColumn /*      */ (Tableau optimalTableau, int i, double newValue)
        {
            var steps = new List<String>();
            if (!EnsureOptimal(optimalTableau, steps))
                return steps;
            var j = optimalTableau.Width - 1; // rhs column
            var initialTableau = (optimalTableau.InitialTableau ?? optimalTableau).Copy();
            var currentValue = initialTableau[i, j];
            var range = GetRhsRanges(optimalTableau, new[] { i }).FirstOrDefault();
            steps.Add($"{initialTableau.RowNames[i]}:rhs={range.current:0.###} can range between [{range.low:0.###}, {range.high:0.###}]");
            if (range.low <= newValue && newValue <= range.high)
            {
                steps.Add($"\n {newValue:0.###} does not change the optimal solution");
                initialTableau[i, j] = newValue;
                EnsureOptimal(initialTableau, new List<String>()); // TODO: replace with math version;
                optimalTableau.Assign(initialTableau);
            }
            else
            {
                steps.Add($"\n {newValue:0.###} requires re-optimization");
                initialTableau[i, j] = newValue;
                EnsureOptimal(initialTableau, steps);
                optimalTableau.Assign(initialTableau);
            }
            steps.Add($"New Optimal Tableau\n\n{optimalTableau}");
            return steps;
        }
        public static List<String> SolveApplyChangeNonBasicColumn /* */ (Tableau optimalTableau, int i, int j, double newValue)
        {
            var steps = new List<String>();
            if (!EnsureOptimal(optimalTableau, steps))
                return steps;
            if (!optimalTableau.IsNonBasicVariable(j))
            {
                steps.Add($"{optimalTableau.ColumnNames[j]} is not a Non-Basic Variable");
                return steps;
            }
            if (i < 1)
            {
                steps.Add($"Objective row can not be modified using this function");
                return steps;
            }
            var initialTableau = (optimalTableau.InitialTableau ?? optimalTableau).Copy();
            var currentValue = initialTableau[i, j];
            var range = GetRhsRanges(optimalTableau, new[] { i }).FirstOrDefault();
            steps.Add($"{initialTableau.RowNames[j]}:rhs={range.current:0.###} can range between [{range.low:0.###}, {range.high:0.###}]");
            if (range.low <= newValue && newValue <= range.high)
            {
                steps.Add($"{newValue:0.###} does not change the optimal solution");
                initialTableau[i, j] = newValue;
                EnsureOptimal(initialTableau, new List<String>()); // TODO: replace with math version;
                optimalTableau.Assign(initialTableau);
            }
            else
            {
                steps.Add($"{newValue:0.###} requires re-optimization");
                initialTableau[i, j] = newValue;
                EnsureOptimal(initialTableau, steps);
                optimalTableau.Assign(initialTableau);
            }
            steps.Add($"Optimal Tableau\n\n{optimalTableau}");
            return steps;
        }
        public static List<String> SolveDisplayObjectiveRanges /*    */ (Tableau optimalTableau, IEnumerable<int> variableIndices = null)
        {
            variableIndices = variableIndices ?? optimalTableau.IndicesForVariables;
            var steps = new List<String>();
            if (!EnsureOptimal(optimalTableau, steps))
                return steps;
            var variableRanges = GetObjectiveRanges(optimalTableau, variableIndices);
            var /*    */ rangesBasic = (/**/"Basic", variableRanges.Where(x => optimalTableau.IsBasicVariable(x.j)).ToArray());
            var /* */ rangesNonBasic = ("Non-Basic", variableRanges.Where(x => !optimalTableau.IsBasicVariable(x.j)).ToArray());
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
        public static List<String> SolveDisplayRhsRanges /*          */ (Tableau optimalTableau, IEnumerable<int> constaintIndices = null)
        {
            constaintIndices = constaintIndices ?? optimalTableau.IndicesForConstraints;
            var steps = new List<String>();
            if (!EnsureOptimal(optimalTableau, steps))
                return steps;
            var rhsRanges = GetRhsRanges(optimalTableau, constaintIndices);
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
        public static List<String> SolveDisplayNonBasicRanges /*     */ (Tableau optimalTableau, IEnumerable<int> variableIndices = null)
        {
            var steps = new List<String>();
            if (!EnsureOptimal(optimalTableau, steps))
                return steps;
            variableIndices = variableIndices ?? optimalTableau.IndicesForNonBasicVariables;
            var nonBasicRanges = GetNonBasicRanges(optimalTableau, variableIndices);
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
        public static List<String> SolveDisplayShadowPrices /*       */ (Tableau optimalTableau)
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
            if (attemptTableau.IsOptimal)
            {
                tableau.Assign(attemptTableau);
                steps.AddRange(branchAndBoundSteps);
                return true;
            }
            steps.Add("Sensitivity Analysis requires an optimal tableau");
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


        // ARMAND
        // Adds a new decision variable (activity), checks if it improves the solution, and re-optimizes if necessary.
        public static List<string> SolveAddActivity(Tableau optimalTableau, double[] column, double cost)
        {
            var steps = new List<string> { "Adding new activity..." };
            if (!EnsureOptimal(optimalTableau, steps))
                return steps;

            var B = optimalTableau.Get_B();
            var BInverse = B.Inverse();
            var cBv = optimalTableau.Get_cBv();
            var reducedCost = cost - cBv * (BInverse * Vector<double>.Build.Dense(column));

            steps.Add($"Reduced cost = {Tableau.FormatDouble(reducedCost)}");
            if (optimalTableau.RowNames[0].StartsWith("max") ? reducedCost >= 0 : reducedCost <= 0)
            {
                steps.Add("New activity does not improve solution → remains at 0.");
                return steps;
            }

            steps.Add("New activity improves solution → re-optimizing...");
            optimalTableau.AddColumn(column, $"x{optimalTableau.Width}", "+");
            optimalTableau[0, optimalTableau.Width - 1] = optimalTableau.RowNames[0].StartsWith("max") ? -cost : cost;

            steps.Add($"Added new column:\n\n{optimalTableau}");
            steps.AddRange(PrimalSimplex.Solve(optimalTableau));
            return steps;
        }

        // Adds a new constraint, checks if the current solution remains feasible, and re-optimizes if needed.
        public static List<string> SolveAddConstraint(Tableau optimalTableau, double[] row, double rhs)
        {
            var steps = new List<string> { "Adding new constraint..." };
            if (!EnsureOptimal(optimalTableau, steps))
                return steps;

            optimalTableau.AddRow(row.Append(rhs).ToArray(), $"c{optimalTableau.Height}");
            steps.Add($"Added new constraint:\n\n{optimalTableau}");

            if (optimalTableau.IsFeasible)
            {
                steps.Add("Constraint satisfied by current solution → no change.");
                return steps;
            }

            steps.Add("Constraint violated → re-optimizing with Dual Simplex...");
            steps.AddRange(DualSimplex.Solve(optimalTableau));
            return steps;
        }

        // Builds the dual problem from the primal, transposing constraints and objectives
        // & Solves the dual problem to find its optimal solution.
        public static List<string> SolveDual(Tableau primal)
        {
            var steps = new List<string> { "Building Dual Problem..." };
            var dual = primal.BuildDual();
            steps.Add($"Dual Tableau:\n\n{dual}");
            steps.Add("Solving Dual...");
            steps.AddRange(DualSimplex.Solve(dual));
            steps.AddRange(PrimalSimplex.Solve(dual));
            steps.Add(VerifyDuality(primal, dual));
            return steps;
        }

        // Checks if the primal and dual solutions satisfy strong or weak duality, ensuring consistency.
        private static string VerifyDuality(Tableau primal, Tableau dual)
        {
            var primalValue = primal.ObjectiveValue;
            var dualValue = dual.ObjectiveValue;
            var diff = Math.Abs(primalValue - dualValue);

            if (diff < 1e-6 && dual.IsFeasible)
                return $"Strong Duality holds (Primal = {Tableau.FormatDouble(primalValue)}, Dual = {Tableau.FormatDouble(dualValue)})";
            else if (primal.RowNames[0].StartsWith("max") ? primalValue <= dualValue : primalValue >= dualValue)
                return $"Weak Duality holds (Primal = {Tableau.FormatDouble(primalValue)}, Dual = {Tableau.FormatDouble(dualValue)})";
            else
                return $"Duality violated (Primal = {Tableau.FormatDouble(primalValue)}, Dual = {Tableau.FormatDouble(dualValue)}, dual solution may be infeasible)";
        }

    }
}
