using Markdig.Extensions.Tables;
using MathNet.Numerics.Distributions;
using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using System.Threading;

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
            steps.Add("End Branch&Bound-Knapsack");
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
    bool includeValuesExtraColumns = true,
    bool includeValuesBranching = true,
    bool includeHeaders = true,
    bool includeAlignment = true,
    bool includeValues = true,
    bool includeProfits = true,
    bool includeCosts = true,
    int colWidth = 6,
    int firstColWidth = 0)
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

                // Start building the HTML string
                sb.AppendLine("<!DOCTYPE html>");
                sb.AppendLine("<html>");
                sb.AppendLine("<head>");
                sb.AppendLine("<title>Knapsack Solution</title>");
                sb.AppendLine("<style>");
                sb.AppendLine("body {background - color: #1e2125;font - family: 'Lato', sans - serif;color: #f0f0f0;margin: 40px;}");
                sb.AppendLine(" thead tr {background - color: #353a50;}");
                sb.AppendLine("table {width: 80 %; border - collapse: collapse; color: #e0e2e8; background - color: #2c3044; border - radius: 8px; overflow: hidden; box - shadow: 0 4px 15px rgba(0, 0, 0, 0.2); font - size: 16px; }");
                sb.AppendLine("th, td { border: 1px solid black; padding: 18px 24px; }");
                sb.AppendLine("th {text - align: left;font - weight: 700;font - size: 14px; color: #a0a5b5;}");
                sb.AppendLine("td { text - align: right;}");
                sb.AppendLine("</style>");
                sb.AppendLine("</head>");
                sb.AppendLine("<body>");

                // Add a heading for the problem name
                sb.AppendLine($"<h1>{problemName}</h1>");

                // Start the HTML table
                sb.AppendLine("<table>");

                // Table Headers (<thead>)
                if (includeHeaders)
                {
                    sb.AppendLine("<thead>");
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<th>{problemName}</th>");
                    foreach (var choice in choices)
                    {
                        sb.AppendLine($"<th>{choice.variable.Name}</th>");
                    }
                    foreach (var header in totalColumnsHeaders)
                    {
                        sb.AppendLine($"<th>{header}</th>");
                    }
                    sb.AppendLine("</tr>");
                    sb.AppendLine("</thead>");
                }

                // Table Body (<tbody>)
                sb.AppendLine("<tbody>");

                // Values Row
                if (includeValues)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{(includeHeaders ? "Values" : problemName)}</td>");
                    foreach (var choice in choices)
                    {
                        var s = choice.choice.Value.ToString("0.###");
                        var e = !includeValuesBranching ? ""
                            : choice.choice.Branch == Branch.Floor ? "v"
                            : choice.choice.Branch == Branch.Ceiling ? "^" : "";
                        sb.AppendLine($"<td>{s}{e}</td>");
                    }
                    foreach (var value in totalColumnsValues)
                    {
                        sb.AppendLine($"<td>{value.ToString("0.###")}</td>");
                    }
                    sb.AppendLine("</tr>");
                }

                // Profits Row
                if (includeProfits)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine("<td>Profits</td>");
                    foreach (var choice in choices)
                    {
                        sb.AppendLine($"<td>{choice.variable.Profit.ToString("0.###")}</td>");
                    }
                    foreach (var value in totalColumnsValues)
                    {
                        sb.AppendLine($"<td></td>"); // Empty cells for the extra columns
                    }
                    sb.AppendLine("</tr>");
                }

                // Costs Row
                if (includeCosts)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine("<td>Costs</td>");
                    foreach (var choice in choices)
                    {
                        sb.AppendLine($"<td>{choice.variable.Cost.ToString("0.###")}</td>");
                    }
                    foreach (var value in totalColumnsValues)
                    {
                        sb.AppendLine($"<td></td>"); // Empty cells for the extra columns
                    }
                    sb.AppendLine("</tr>");
                }

                // End the HTML table
                sb.AppendLine("</tbody>");
                sb.AppendLine("</table>");

                // End the HTML document
                sb.AppendLine("</body>");
                sb.AppendLine("</html>");

                return sb.ToString();
            }
        }
    }
}