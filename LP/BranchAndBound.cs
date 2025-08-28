using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381.LP
{
    public static class BranchAndBound
    {
        private const int maxBranchingDepth = 16;
        public static List<string> Solve(Tableau tableau)
        {
            var steps = new List<String>() { "Start Branch&Bound" };
            var candidates = new List<(Tableau tableau, string problemName)>();
            RecursiveSolve(tableau.Copy(), "", ref candidates, ref steps);

            const int problemNameMaxLength = 40;
            steps.Add(String.Join("\n", candidates
                .Select(candidate => $"| {candidate.tableau.ObjectiveValue,3:0.#} | {candidate.problemName, /* */ -problemNameMaxLength /**/ } |")
                .Prepend(/*       */ $"| {/*                            */ "--:"} | {/*           */ ":-".PadRight(problemNameMaxLength, '-')} |")
                .Prepend(/*       */ $"| {/*                            */ "  Z"} | {"Problem Name", /*        */ -problemNameMaxLength /**/ } |")
                .Prepend("Candidates:\n")
            ));

            if (candidates.Count <= 0)
            {
                steps.Add("**Infeasible**: No candidates that satisfy all integer constraints found.");
            }
            else
            {
                var bestCandidate = candidates.Aggregate((max, c) => c.tableau.ObjectiveValue > max.tableau.ObjectiveValue ? c : max);
                steps.Add($"Best solution found: {bestCandidate.problemName} with Z={bestCandidate.tableau.ObjectiveValue}\n\n{bestCandidate.tableau}");
                tableau.Assign(bestCandidate.tableau);
            }

            steps.Add("End Branch&Bound");
            return steps;
        }
         
        private static void RecursiveSolve(
            /*     */ Tableau tableau, string problemName,
            ref List<(Tableau tableau, string problemName)> candidates,
            ref List<String> steps,
            int depthTracker = 0)
        {
            if (depthTracker >= maxBranchingDepth)
            {
                steps.Add($"[{problemName}] Max branching depth reached. **Branch abandoned**");
                return;
            }

            steps.AddRange(DualSimplex.Solve(tableau));
            if (tableau.IsDualInfeasible)
            {
                steps.Add($"{problemName} Dual solution is infeasible. **Branch abandoned**");
                return;
            }

            steps.AddRange(PrimalSimplex.Solve(tableau));
            if (tableau.IsPrimalInfeasible)
            {
                steps.Add($"{problemName} Primal solution is infeasible. **Branch abandoned**");
                return;
            }
            if (tableau.IsPrimalInoptimal) throw new Exception("Primal solution is inoptimal, but Dual solution is feasible. This should not happen in Branch&Bound.");

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
                steps.Add($"[{problemName}] All integer constraints satisfied");
                steps.Add($"[{problemName}] New Candidate: {tableau.ObjectiveValue}");
                candidates.Add((tableau, problemName));
                return;
            }

            var tableauParent = tableau;
            var problemNameParent = problemName;
            var fraction = tableau[fractionI, tableau.Width - 1];
            steps.Add($"[{problemName}] Branch on **{tableau.ColumnNames[fractionJ]}**={fraction}");
            var newCol = new double[tableau.Height];
            var newRow = new double[tableau.Width + 1];

            for (int branch = 0; branch < 2; branch++)
            {
                var floor = branch == 0;
                tableau = tableauParent.Copy();
                tableau.InitialTableau = tableau.InitialTableau?.Copy();
                problemName = problemNameParent + (floor ? ".1" : ".2");
                if (problemName.StartsWith("."))
                    problemName = problemName.Substring(1);
                tableau.InitialTableau?.AddColumn(newCol, $"{(floor ? "s" : "e")}{tableau.Height}", "+");
                tableau/*            */.AddColumn(newCol, $"{(floor ? "s" : "e")}{tableau.Height}", "+");
                newRow[fractionJ /*  */ ] = floor ? 1.0 /*            */ : 1.0 /*              */;
                newRow[tableau.Width - 2] = floor ? 1.0 /*            */ : -1.0 /*             */;
                newRow[tableau.Width - 1] = floor ? Math.Floor(fraction) : Math.Ceiling(fraction);
                tableau.InitialTableau?.AddRow(newRow, $"c{tableau.Height}");
                tableau/*            */.AddRow(newRow, $"c{tableau.Height}");
                steps.Add($"[{problemName}] Branch **{tableau.ColumnNames[fractionJ]}**{(floor ? "<=" : ">=")}{newRow[tableau.Width - 1]}\n\n{tableau}");
                for (int j = 0; j < tableau.Width; j++)
                    tableau[tableau.Height - 1, j] = floor ? tableau[tableau.Height - 1, j] - tableau[fractionI, j]
                                                           : tableau[fractionI, j] - tableau[tableau.Height - 1, j];
                steps.Add($"[{problemName}] Restore basic variable ({(floor ? "new=new-old" : "new=old-new")})\n\n{tableau}");
                RecursiveSolve(tableau.Copy(), problemName, ref candidates, ref steps, depthTracker + 1);
            }
        }
    }
}
