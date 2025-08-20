using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;

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
            
            var bestCandidate = candidates.FirstOrDefault();
            foreach (var candidate in candidates.Skip(1))
                if (candidate.tableau.ObjectiveValue > bestCandidate.tableau.ObjectiveValue)
                    bestCandidate = candidate;
            if /*  */ (bestCandidate.tableau != null && bestCandidate.problemName != null)
            {
                steps.Add($"Best solution found: {bestCandidate.problemName} with Z={bestCandidate.tableau.ObjectiveValue}\n\n{bestCandidate.tableau}");
                tableau.Assign(bestCandidate.tableau);
            }
            else if (!(bestCandidate.tableau == null && bestCandidate.problemName == null))
            {
                throw new Exception("Incomplete Pairing");
            }
            else
            {
                steps.Add("**Infeasible**: No candidates that satisfy all integer constraints found.");
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

            // Branch on floor
            tableau = tableauParent.Copy();
            problemName = problemNameParent + ".1";
            if (problemName.StartsWith("."))
                problemName = problemName.Substring(1);
            tableau.AddColumn(new double[tableau.Height], $"s{tableau.Height}", "+");
            tableau.AddRow(new double[tableau.Width], $"c{tableau.Height}");
            tableau[tableau.Height - 1, fractionJ /*  */ ] = 1.0;
            tableau[tableau.Height - 1, tableau.Width - 2] = 1.0;
            tableau[tableau.Height - 1, tableau.Width - 1] = Math.Floor(fraction);
            steps.Add($"[{problemName}] Branch **{tableau.ColumnNames[fractionJ]}**<={Math.Floor(fraction)}\n\n{tableau}");
            for (int j = 0; j < tableau.Width; j++)
                tableau[tableau.Height - 1, j] = tableau[tableau.Height - 1, j] - tableau[fractionI, j];
            steps.Add($"[{problemName}] Restore basic variable (new=new-old)\n\n{tableau}");
            RecursiveSolve(tableau.Copy(), problemName, ref candidates, ref steps, depthTracker + 1);

            // Branch on ceil
            tableau = tableauParent.Copy();
            problemName = problemNameParent + ".2";
            if (problemName.StartsWith("."))
                problemName = problemName.Substring(1);
            tableau.AddColumn(new double[tableau.Height], $"e{tableau.Height}", "+");
            tableau.AddRow(new double[tableau.Width], $"c{tableau.Height}");
            tableau[tableau.Height - 1, fractionJ /*  */ ] = 1.0;
            tableau[tableau.Height - 1, tableau.Width - 2] = -1.0;
            tableau[tableau.Height - 1, tableau.Width - 1] = Math.Ceiling(fraction);
            steps.Add($"[{problemName}] Branch **{tableau.ColumnNames[fractionJ]}**>={Math.Ceiling(fraction)}\n\n{tableau}");
            for (int j = 0; j < tableau.Width; j++)
                tableau[tableau.Height - 1, j] = tableau[fractionI, j] - tableau[tableau.Height - 1, j];
            steps.Add($"[{problemName}] Restore basic variable (new=old-new)\n\n{tableau}");
            RecursiveSolve(tableau.Copy(), problemName, ref candidates, ref steps, depthTracker + 1);
        }
    }
}
