using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LPR381.LP
{
    public static class BranchAndBoundKnapsack
    {

        private const int maxBranchingDepth = 16;
        public static List<string> Solve(LP.Tableau tableau)
        {
            var steps = new List<String>() { "Start Branch&Bound-Knapsack" };
            Tableau knapsackTableau = new Tableau
            {
                Budget = tableau[1, tableau.Width - 1],
                Variables = tableau.GetVariableIndices()
                    .Where(j => tableau.GetBasicVariableI(j, +1.0) == null && // exclude slacks
                                tableau.GetBasicVariableI(j, -1.0) == null)   // exclude excesses
                    .Select(j => new Variable
                    {
                        Name /*   */ = tableau.ColumnNames[j],
                        Profit /* */ = -tableau[0, j],
                        Cost /*   */ = +tableau[1, j]
                    })
                    .ToArray(),
            };
            steps.Add($"[{knapsackTableau.ProblemName}] Initial Table  \n{knapsackTableau}");
            
            var candidatesList = new List<Tableau>();
            RecursiveSolve(knapsackTableau, candidatesList, steps, 0);

            var candidates = candidatesList.ToArray();
            if (candidates.Length <= 0)
            {
                steps.Add("No Feasible Solutions Found");
            }
            else
            {
                var problemNameMaxLength = candidates.Select(c => c.ProblemName.Length).Max();
                var best = candidates
                    .Select(candidate => { var profit = candidate.GetProfit(); return (profit, candidate); })
                    .Aggregate((max, x) => x.profit > max.profit ? x : max).candidate;
                steps.Add("Solutions:  \n" + String.Join("\n", candidates.Select((c, i) => c.ToStringDefaultExclude(
                    includeValuesExtraColumns /* */: true,
                    includeValues /*             */: true,
                    firstColWidth /*             */: problemNameMaxLength
                )).Prepend(best.ToStringDefaultExclude(
                    includeValuesExtraColumns /* */: true,
                    includeHeaders /*            */: true,
                    includeAlignment /*          */: true,
                    firstColWidth /*             */: problemNameMaxLength
                ))));
                steps.Add($"Best Solution Found  \n" + best.ToStringDefaultExclude(
                    includeValuesExtraColumns /* */: true,
                    includeHeaders /*            */: true,
                    includeAlignment /*          */: true,
                    includeValues /*             */: true,
                    firstColWidth /*             */: problemNameMaxLength
                ));
            }
            steps.Add("End Branch&Bound");
            return steps;
        }

        private static void RecursiveSolve(
            Tableau tableau,
            List<Tableau> condidates,
            List<string> steps,
            int depth)
        {
            var problemName = tableau.ProblemName;
            if (depth > maxBranchingDepth)
            {
                steps.Add($"[{problemName}] Max branching depth reached! Branch Abandoned");
                return;
            }
            var previousBranchI = tableau.LatestBranchI;
            if (previousBranchI >= 0)
            {
                var variableName /* */ = tableau /*  */ [previousBranchI].variable.Name;
                var sign /*         */ = tableau.Choices[previousBranchI].Branch == Branch.Floor ? "<=" : ">=";
                var value /*        */ = tableau.Choices[previousBranchI].Value;
                steps.Add($"[{problemName}] Branch **{variableName}** {sign} {value}  ");
            }

            int branchI = -1;
            {
                int i = 0;
                var remainingBudget = tableau.Budget;
                // Drain fixed costs
                for (; i < tableau.Choices.Length && tableau.Choices[i].Branch != Branch.None; i++)
                {
                    var (choice, variable) = tableau[i];
                    remainingBudget -= choice.Value * variable.Cost;
                    if (remainingBudget < 0)
                    {
                        steps.Add($"Solution Infeasible  \n{tableau}");
                        return;
                    }
                }
                // Drain non-fixed costs
                for (; i < tableau.Choices.Length; i++)
                {
                    tableau.Choices[i].Value = 1.0;
                    var (choice, variable) = tableau[i];
                    remainingBudget -= choice.Value * variable.Cost;
                    if (remainingBudget == 0.0)
                        break;
                    if (remainingBudget < 0)
                    {
                        tableau.Choices[i].Value = 1.0 + remainingBudget / variable.Cost;
                        branchI = i++;
                        break;
                    }
                }
                // Zero budget exceding variables
                for (; i < tableau.Choices.Length; i++)
                {
                    tableau.Choices[i].Value = 0.0;
                }
            }
            if (branchI < 0)
            {
                steps.Add($"Solution Feasible  \n{tableau}");
                condidates.Add(tableau);
                return;
            }

            {
                var variableName = tableau[branchI].variable.Name;
                var tableauString = tableau.ToStringDefaultExclude(
                        includeHeaders /*            */ : true,
                        includeAlignment /*          */ : true,
                        includeValues /*             */ : true,
                        includeValuesExtraColumns /* */ : true,
                        includeValuesBranching /*    */ : true);
                steps.Add($"Branch on **{variableName}** \n{tableauString}");
            }

            for (Branch branch = Branch.Floor; branch <= Branch.Ceiling; branch++)
            {
                var nextTableau = new Tableau
                {
                    Budget = tableau.Budget,
                    Variables = tableau.Variables,
                    Choices = tableau.Choices.Select((c, i) =>
                    {
                        if (i == branchI)
                        {
                            c.Value = branch == Branch.Floor ? Math.Floor(c.Value) : Math.Ceiling(c.Value);
                            c.Branch = branch;
                        }
                        return c;
                    })
                    .OrderBy(c => c.Branch == Branch.None)
                    .ToArray(),
                };
                RecursiveSolve(nextTableau, condidates, steps, depth + 1);
            }
        }

        private enum Branch
        {
            None = 0,
            Floor = 1,
            Ceiling = 2
        }
        private struct Choice
        {
            public int Index;
            public Branch Branch;
            public double Value;
        }
        private struct Variable 
        { 
            public string Name;
            public double Profit;
            public double Cost;
        }
        private struct Tableau
        {
            private Choice[] choices;
            public double Budget { get; set; }
            public Variable[] Variables { get; set; }
            public Choice[] Choices
            {
                get => choices ?? (choices = Variables?
                    .Select((variable, index) => (variable, index))
                    .OrderByDescending(x => x.variable.Profit / x.variable.Cost)
                    .Select(x => new Choice { Index = x.index, Branch = Branch.None, Value = 0 })
                    .ToArray());
                set => choices = value;
            }
            public (Choice choice, Variable variable) this[int i]
            {
                get { var choice = Choices[i]; var variable = Variables[choice.Index]; return (choice, variable); }
                set => Choices[i] = value.choice;
            }
            public IEnumerable<(Choice choice, Variable variable)> ChoicesWithVariables => Enumerable.Repeat(this, Choices.Length).Select((self, i) => self[i]);
            public string ProblemName => String.Join(".", Choices.Where(c => c.Branch != Branch.None).Select(c => (int)c.Branch));
            public int LatestBranchI => Choices.Select((c, i) => (c, i)).Where(x => x.c.Branch != Branch.None).Select(x => x.i).Prepend(-1).Last();
            public double GetProfit /* */() => ChoicesWithVariables.Select(x => x.choice.Value * x.variable.Profit /* */).Sum();
            public double GetCost /*   */() => ChoicesWithVariables.Select(x => x.choice.Value * x.variable.Cost /*   */).Sum();
            public override string ToString() => ToStringDefaultInclude();
            public string ToStringDefaultExclude(
                bool /* */ includeValuesExtraColumns /* */ = false,
                bool /* */ includeValuesBranching /*    */ = false,
                bool /* */ includeHeaders /*            */ = false,
                bool /* */ includeAlignment /*          */ = false,
                bool /* */ includeValues /*             */ = false,
                bool /* */ includeProfits /*            */ = false,
                bool /* */ includeCosts /*              */ = false,
                int /*  */ colWidth /*                  */ = 6,
                int /*  */ firstColWidth /*             */ = 0)
                => ToStringDefaultInclude(
                    includeValuesExtraColumns /* */: includeValuesExtraColumns /* */,
                    includeValuesBranching /*    */: includeValuesBranching /*    */,
                    includeHeaders /*            */: includeHeaders /*            */,
                    includeAlignment /*          */: includeAlignment /*          */,
                    includeValues /*             */: includeValues /*             */,
                    includeProfits /*            */: includeProfits /*            */,
                    includeCosts /*              */: includeCosts /*              */,
                    colWidth /*                  */: colWidth /*                  */,
                    firstColWidth /*             */: firstColWidth /*             */);
            public string ToStringDefaultInclude(
                bool /* */ includeValuesExtraColumns /* */ = true,
                bool /* */ includeValuesBranching /*    */ = true,
                bool /* */ includeHeaders /*            */ = true,
                bool /* */ includeAlignment /*          */ = true,
                bool /* */ includeValues /*             */ = true,
                bool /* */ includeProfits /*            */ = true,
                bool /* */ includeCosts /*              */ = true,
                int /*  */ colWidth /*                  */ = 6,
                int /*  */ firstColWidth /*             */ = 0)
            {
                var problemName = ProblemName;
                firstColWidth = firstColWidth <= 0 ? colWidth : firstColWidth;
                firstColWidth = Math.Max(firstColWidth, problemName.Length);
                firstColWidth = Math.Max(firstColWidth, colWidth);
                firstColWidth = Math.Max(firstColWidth, 7);
                colWidth = colWidth <= 0 ? firstColWidth : colWidth;
                colWidth = Math.Max(colWidth, 2);
                var choices = ChoicesWithVariables.OrderBy(c => c.choice.Index).ToArray();
                var totalColumnsHeaders = includeValuesExtraColumns ? "Profit|Cost|Budget".Split('|') : new string[0];
                var totalColumnsValues = includeValuesExtraColumns
                    ? new double[] { choices.Select(c => c.choice.Value * c.variable.Profit).Sum(),
                                     choices.Select(c => c.choice.Value * c.variable.Cost).Sum(),
                                     Budget }
                    : new double[0];
                var sb = new StringBuilder();
                if (includeHeaders /*      */)
                {
                    sb.Append($"| {ProblemName.PadRight(firstColWidth)} ");
                    foreach (var choice in choices)
                        sb.Append($"| {choice.variable.Name.PadLeft(colWidth)} ");
                    foreach (var header in totalColumnsHeaders)
                        sb.Append($"| {header.PadLeft(colWidth)} ");
                    sb.Append("|\n");
                }
                if (includeAlignment /*    */)
                {
                    sb.Append($"| {":-".PadRight(firstColWidth, '-')} ");
                    var align = $"| {"-:".PadLeft(colWidth, '-')} ";
                    foreach (var choice in choices)
                        sb.Append(align);
                    foreach (var header in totalColumnsHeaders)
                        sb.Append(align);
                    sb.Append("|\n");
                }
                if (includeValues /*       */)
                {
                    sb.Append($"| {(includeHeaders ? "Values" : problemName).PadRight(firstColWidth)} ");
                    foreach (var choice in choices)
                    {
                        var s = choice.choice.Value.ToString("0.###");
                        var e = !includeValuesBranching /*              */ ? " "
                            : choice.choice.Branch == Branch.Floor /*   */ ? "v"
                            : choice.choice.Branch == Branch.Ceiling /* */ ? "^" : " ";
                        sb.Append($"| {s.PadLeft(colWidth)}{e}");
                    }
                    foreach (var value in totalColumnsValues)
                        sb.Append($"| {value.ToString("0.###").PadLeft(colWidth)} ");
                    sb.Append("|\n");
                }
                if (includeProfits /*      */)
                {
                    sb.Append($"| {"Profits".PadRight(firstColWidth)} ");
                    foreach (var choice in choices)
                        sb.Append($"| {choice.variable.Profit.ToString("0.###").PadLeft(colWidth)} ");
                    foreach (var value in totalColumnsValues)
                        sb.Append($"| {"".PadLeft(colWidth)} ");
                    sb.Append("|\n");
                }
                if (includeCosts /*        */)
                {
                    sb.Append($"| {"Costs".PadRight(firstColWidth)} ");
                    foreach (var choice in choices)
                        sb.Append($"| {choice.variable.Cost.ToString("0.###").PadLeft(colWidth)} ");
                    foreach (var value in totalColumnsValues)
                        sb.Append($"| {"".PadLeft(colWidth)} ");
                    sb.Append("|\n");
                }
                sb.Remove(sb.Length - 1, 1);
                return sb.ToString();
            }
        }
    }
}