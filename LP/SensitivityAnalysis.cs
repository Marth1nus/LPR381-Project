using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms.VisualStyles;

namespace LPR381.LP
{
    public class SensitivityAnalysis
    {

        /* 
         * [x] Display the range of a selected Non - Basic Variable.
         * [ ] Apply and display a change of a selected Non - Basic Variable.
         * [x] Display the range of a selected Basic Variable.
         * [ ] Apply and display a change of a selected Basic Variable.
         * [ ] Display the range of a selected constraint right-hand - side value.
         * [ ] Apply and display a change of a selected constraint right-hand - side value.
         * [ ] Display the range of a selected variable in a Non-Basic Variable column.
         * [ ] Apply and display a change of a selected variable in a Non-Basic Variable column.
         * [ ] Add a new activity to an optimal solution.
         * [ ] Add a new constraint to an optimal solution.
         * [x] Display the shadow prices.
         * [ ] Duality:
             * [ ] Apply Duality to the programming model.
             * [ ] Solve the Dual Programming Model.
             * [ ] Verify whether the Programming Model has Strong or Weak Duality.
        */

        public static List<String> Solve(Tableau tableau)
        {
            var steps = new List<String>() { "Start Sensitivity Analysis" };
            if (!EnsureOptimal(tableau, steps))
                return steps;

            // Basic + Non-Basic Variables
            steps.Add($"Non-Basic Variables : [{String.Join(", ", tableau./*   */IndicesForBasicVariables.Select(j => tableau.ColumnNames[j]))}]  \n" +
                      $"Basic Variables     : [{String.Join(", ", tableau./**/IndicesForNonBasicVariables.Select(j => tableau.ColumnNames[j]))}]");
            steps.AddRange(SolveShadowPrices(tableau));
            steps.AddRange(SolveVariableObjectiveRowCoefficientRanges(tableau));

            // More code here <3

            steps.Add("End Sensitivity Analysis");
            return steps;
        }

        public static List<String> SolveShadowPrices(Tableau tableau)
        {
            var steps = new List<String>();
            if (!EnsureOptimal(tableau, steps))
                return steps;
            var B = tableau.Get_B();
            var BInverse = B.Inverse();
            var cBv = tableau.Get_cBv();
            var shadowPrices = cBv * BInverse;
            // CBV + Shadow Prices
            steps.Add($"CBV          : [{String.Join(", ", cBv) /*    */}]  \n" +
                      $"Shadow Prices: [{String.Join(", ", shadowPrices)}]");
            return steps;
        }

        public static List<String> SolveVariableObjectiveRowCoefficientRanges(Tableau tableau) => SolveVariableObjectiveRowCoefficientRanges(tableau, tableau.IndicesForVariables);
        public static List<String> SolveVariableObjectiveRowCoefficientRanges(Tableau tableau, int j) => SolveVariableObjectiveRowCoefficientRanges(tableau, Enumerable.Repeat(j, 1));
        public static List<String> SolveVariableObjectiveRowCoefficientRanges(Tableau tableau, IEnumerable<int> variableIndices)
        {
            var steps = new List<String>();
            if (!EnsureOptimal(tableau, steps))
                return steps;
            var variableRanges = GetVariableObjectiveRowCoefficientRanges(tableau, variableIndices);
            var /*    */ rangesBasic = (/**/"Basic", variableRanges.Where(x =>  tableau.IsBasicVariable(x.j)).ToArray());
            var /* */ rangesNonBasic = ("Non-Basic", variableRanges.Where(x => !tableau.IsBasicVariable(x.j)).ToArray());
            var /*      */ rangesAll = (/*  */"All", variableRanges);
            var groups = /*    */ rangesBasic.Item2.Length == 0 ? new[] { /* */ rangesNonBasic }
                       : /* */ rangesNonBasic.Item2.Length == 0 ? new[] { /*    */ rangesBasic }
                                                                : new[] { rangesBasic, rangesNonBasic, rangesAll };
            foreach (var (groupName, group) in groups)
            {
                if (group.Length == 0)
                    continue;
                var s = group.Length > 1 ? "s" : "";
                steps.Add($"Range{s} for {groupName} variable coefficient{s}\n\n{GetVariableRangeMarkdownTable(group)}");
            }
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

        private static Vector<double> GetShadowPrices(Tableau tableau)
        {
            var B = tableau.Get_B();
            var BInverse = B.Inverse();
            var cBv = tableau.Get_cBv();
            var shadowPrices = cBv * BInverse;
            return shadowPrices;
        }

        private static (int j, string name, bool isBasic, double low, double current, double high)[] 
            GetVariableObjectiveRowCoefficientRanges(Tableau tableau, IEnumerable<int> variableIndices = null)
        {
            var shadowPrices = GetShadowPrices(tableau);
            variableIndices = variableIndices ?? tableau.IndicesForDecisionVariables;
            return variableIndices.Select(j =>
            {
                var cb = tableau.InitialTableau[0, j];
                var basicI = tableau.GetBasicVariableI(j);
                var allowableDecrease = double.PositiveInfinity;
                var allowableIncrease = double.PositiveInfinity;
                if (basicI.HasValue) /* Basic */
                {
                    foreach (var i in tableau.IndicesForConstraints)
                    {
                        var coefficient = tableau.InitialTableau[i, j];
                        var ratio = tableau.InitialTableau[i, 0] / coefficient;
                        if /**/ (coefficient < 0.0)
                            allowableDecrease = Math.Min(allowableDecrease, -shadowPrices[i - 1] / coefficient);
                        else if (coefficient > 0.0)
                            allowableIncrease = Math.Min(allowableIncrease, +shadowPrices[i - 1] / coefficient);
                    }
                }
                else /* Non Basic */
                {
                    var column = Vector<double>.Build.DenseOfEnumerable(
                        tableau.IndicesForConstraints
                               .Select(i => tableau.InitialTableau[i, j]));
                    var reducedCost = cb - shadowPrices.DotProduct(column);
                    allowableDecrease = -reducedCost;
                    allowableIncrease = -reducedCost;
                }
                var name /*    */ = tableau.ColumnNames[j];
                var isBasic /* */ = basicI.HasValue;
                var low /*     */ = cb - allowableDecrease;
                var current /* */ = cb;
                var high /*    */ = cb + allowableIncrease;
                return (j, name, isBasic, low, current, high);
            }).ToArray();
        }

        private static string GetVariableRangeMarkdownTable(
            (int j, string name, bool isBasic, double low, double current, double high)[] ranges,
            int columnWidth = 10, int decimalLength = 3)
        {
            var headersUnsplit = "Name|IsBasic|Low|Current|High";
            var sb = new StringBuilder();
            sb.Append("|");
            var headers = headersUnsplit.Split('|');
            foreach (var header in headers)
            {
                sb.Append(" "); sb.Append(header.PadLeft(columnWidth) /*             */); sb.Append(" |");
            }
            sb.Append("\n|");
            var alignmentColumn = "-:".PadLeft(columnWidth, '-');
            foreach (var header in headers)
            {
                sb.Append(" "); sb.Append(alignmentColumn /*                         */); sb.Append(" |");
            }
            foreach (var (j, name, isBasic, low, current, high) in ranges)
            {
                string formatDouble(double d) => Tableau.FormatDouble(d, columnWidth, decimalLength);
                sb.Append("\n|");
                sb.Append(" "); sb.Append(name /*         */.PadLeft(columnWidth) /* */); sb.Append(" |");
                sb.Append(" "); sb.Append(isBasic.ToString().PadLeft(columnWidth) /* */); sb.Append(" |");
                sb.Append(" "); sb.Append(formatDouble(low /*     */) /*             */); sb.Append(" |");
                sb.Append(" "); sb.Append(formatDouble(current /* */) /*             */); sb.Append(" |");
                sb.Append(" "); sb.Append(formatDouble(high /*    */) /*             */); sb.Append(" |");
            }
            return sb.ToString();
        }
    }
}
