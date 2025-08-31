using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR381.LP
{
    public static class PrimalSimplex
    {
        private static Tableau ReconstructTableau(
             Matrix<double> bInv, List<int> basicVarIndices,
             Matrix<double> aOrig, Vector<double> bOrig, Vector<double> cOrig,
             Tableau initialTableauForHeaders)
        {
            var numConstraints = initialTableauForHeaders.Height - 1;
            var numTotalVars = initialTableauForHeaders.Width - 1;
            var cB /*  */ = Vector<double>.Build.DenseOfEnumerable(basicVarIndices.Select(j => cOrig[j]));
            var y /*   */ = Vector<double>.Build.DenseOfEnumerable((cB * bInv).Select(v => Math.Round(v, 12)));
            var newValues = new double[numConstraints + 1, numTotalVars + 1];
            for (int j = 0; j < numTotalVars; j++)
            {
                if (basicVarIndices.Contains(j))
                {
                    var basicIndexRow = basicVarIndices.IndexOf(j);
                    newValues[0, j] = 0.0;
                    for (int i = 0; i < numConstraints; i++)
                    {
                        newValues[i + 1, j] = (i == basicIndexRow) ? 1.0 : 0.0;
                    }
                }
                else
                {
                    var aJ = Vector<double>.Build.DenseOfEnumerable(Enumerable.Range(0, numConstraints).Select(i=> aOrig[i, j]));
                    var aBarJ = bInv * aJ;

                    double yAj = 0;
                    for (int i = 0; i < numConstraints; i++)
                    {
                        yAj += y[i] * aOrig[i, j];
                    }
                    newValues[0, j] = yAj - cOrig[j];
                    for (int i = 0; i < numConstraints; i++)
                    {
                        newValues[i + 1, j] = aBarJ[i];
                    }
                }
            }
            var zValue = y.DotProduct(bOrig);
            newValues[0, numTotalVars] = Math.Round(zValue, 12);
            var bBar = bInv * bOrig;
            for (int i = 0; i < numConstraints; i++)
            {
                newValues[i + 1, numTotalVars] = Math.Round(bBar[i], 12);
            }
            var reconstructed = initialTableauForHeaders.Copy();
            reconstructed.Values = newValues;
            return reconstructed;
        }

        public static List<string> SolveRevised(Tableau tableau)
        {
            var steps = new List<string> { "Start Revised Primal Simplex" };

            var bInv = Matrix<double>.Build.DenseIdentity(tableau.Height - 1);
            var basicVarIndices = tableau.IndicesForBasicVariables.ToList();
            var nonBasicVarIndices = tableau.IndicesForNonBasicVariables.ToList();

            var c = -tableau[0, tableau.IndicesForVariables];
            var b = tableau[tableau.IndicesForConstraints, tableau.Width - 1];
            var A = tableau[tableau.IndicesForConstraints, tableau.IndicesForVariables];

            const int maxIterations = 128;
            for (int iteration = 0; ; iteration++)
            {
                if (iteration >= maxIterations)
                {
                    steps.Add("Algorithm terminated: Maximum iterations reached.");
                    break;
                }

                var cB = -tableau[0, basicVarIndices];
                var y = cB * bInv;

                var enteringCol = -1;
                var maxReducedCost = 0.0;
                foreach (var j in nonBasicVarIndices)
                {
                    var yAj = y * A.Column(j);
                    var reducedCost = c[j] - yAj;
                    if (reducedCost > 1e-9 && reducedCost > maxReducedCost)
                    {
                        maxReducedCost = reducedCost;
                        enteringCol = j;
                    }
                }

                if (enteringCol == -1)
                {
                    steps.Add(ConstructSolution(ReconstructTableau(bInv, basicVarIndices, A, b, c, tableau)));
                    break;
                }

                var d = bInv * A.Column(enteringCol);
                if (!d.Any(v => v > 1e-9))
                {
                    steps.Add("Unbounded solution.");
                    break;
                }

                var xB = bInv * b;
                var leavingRowInBasis = -1;
                var minRatio = double.MaxValue;
                for (int i = 0; i < tableau.Height - 1; i++)
                {
                    if (d[i] > 1e-9)
                    {
                        double ratio = xB[i] / d[i];
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            leavingRowInBasis = i;
                        }
                    }
                }
                int leavingCol = basicVarIndices[leavingRowInBasis];

                basicVarIndices[leavingRowInBasis] = enteringCol;
                nonBasicVarIndices.Remove(enteringCol);
                nonBasicVarIndices.Add(leavingCol);

                var pivotElement = d[leavingRowInBasis];
                var E = Matrix<double>.Build.DenseIdentity(tableau.Height - 1);
                E.SetColumn(leavingRowInBasis, -d / pivotElement);
                E[leavingRowInBasis, leavingRowInBasis] = 1.0 / pivotElement;
                bInv = (E * bInv).Map(v => Math.Round(v, 12));
            }

            steps.Add("End Revised Primal Simplex");
            return steps;
        }

        // Solve method implements Primal Simplex algorithm
        public static List<string> Solve(Tableau tableau)
        {
            var steps = new List<string> { "Start Primal Simplex" };
            const int maxIterations = 128;
            for (int iteration = 0; ; iteration++)
            {
                if (iteration >= maxIterations)
                {
                    steps.Add("Algorithm terminated: Maximum iterations reached.");
                    break;
                }

                var isMaxProblem = tableau.RowNames[0].Contains("max");
                int pj = -1;
                if (isMaxProblem)
                {
                    var minObjective = tableau[0, null].Min();
                    if (minObjective >= 0.0)
                    {
                        steps.Add(ConstructSolution(tableau));
                        break;
                    }
                    pj = Array.IndexOf(tableau[0, null].ToArray(), minObjective);
                }
                else // isMinProblem
                {
                    var maxObjective = tableau[0, null].Max();
                    if (maxObjective <= 0.0)
                    {
                        steps.Add(ConstructSolution(tableau));
                        break;
                    }
                    pj = Array.IndexOf(tableau[0, null].ToArray(), maxObjective);
                }

                var pjV = tableau[null, pj];
                var rhs = tableau[null, tableau.Width - 1];
                var ratios = rhs / pjV;
                var minPositiveRatio = ratios.Where((r, i) => rhs[i] < 0.0 == pjV[i] < 0.0).DefaultIfEmpty(-1.0).Min();
                if (minPositiveRatio <= 0.0)
                {
                    steps.Add("Unbounded solution");
                    break;
                }
                var pi = Array.IndexOf(ratios.ToArray(), minPositiveRatio);

                steps.Add(tableau.Pivot(pi, pj));
            }
            steps.Add("End Primal Simplex");
            return steps;
        }

        // Constructs and returns a string representation of the optimal solution
        private static string ConstructSolution(Tableau tableau)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Optimal solution found.  ");
            foreach (var (name, value) in tableau.IndicesForVariables
                .Select(j => (tableau.ColumnNames[j] /* */, tableau.GetVariableValue(j) /* */))
                .Append(/* */("Optimal Value(Z)" /*     */, tableau.ObjectiveValue /*      */)))
            {
                sb.AppendLine($"{name} = {value:0.###}  ");
            }
            return sb.ToString();
        }
    }
}
