using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR381.LP
{
    public static class MatrixHelpers
    {
        public static double[,] CreateIdentityMatrix(int size)
        {
            double[,] matrix = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                matrix[i, i] = 1.0;
            }
            return matrix;
        }

        public static double[] MatrixVectorMultiply(double[,] matrix, double[] vector)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[] result = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i] += matrix[i, j] * vector[j];
                }
                result[i] = Math.Round(result[i], 12);
            }
            return result;
        }

        public static double[] VectorMatrixMultiply(double[] vector, double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[] result = new double[cols];
            for (int j = 0; j < cols; j++)
            {
                for (int i = 0; i < rows; i++)
                {
                    result[j] += vector[i] * matrix[i, j];
                }
            }
            return result;
        }

        public static double[,] MatrixMultiply(double[,] matrixA, double[,] matrixB)
        {
            int aRows = matrixA.GetLength(0);
            int aCols = matrixA.GetLength(1);
            int bCols = matrixB.GetLength(1);
            double[,] result = new double[aRows, bCols];

            for (int i = 0; i < aRows; i++)
            {
                for (int j = 0; j < bCols; j++)
                {
                    for (int k = 0; k < aCols; k++)
                    {
                        result[i, j] += matrixA[i, k] * matrixB[k, j];
                    }
                    result[i, j] = Math.Round(result[i, j], 12);
                }
            }
            return result;
        }

        public static double DotProduct(double[] vectorA, double[] vectorB)
        {
            double result = 0;
            for (int i = 0; i < vectorA.Length; i++)
            {
                result += vectorA[i] * vectorB[i];
            }
            result = Math.Round(result, 12);
            return result;
        }
    }

    public static class PrimalSimplex
    {


        private static Tableau ReconstructTableau(
     int numConstraints, int numTotalVars,
     double[,] bInv, List<int> basicVarIndices,
     double[,] aOrig, double[] bOrig, double[] cOrig,
     Tableau initialTableauForHeaders)
        {
            double[,] newValues = new double[numConstraints + 1, numTotalVars + 1];
            double[] cB = new double[numConstraints];
            for (int i = 0; i < basicVarIndices.Count; i++)
            {
                cB[i] = cOrig[basicVarIndices[i]];
            }
            double[] y = MatrixHelpers.VectorMatrixMultiply(cB, bInv);
            y = y.Select(val => Math.Round(val, 12)).ToArray();

            for (int j = 0; j < numTotalVars; j++)
            {
                int basicIndexRow = -1;
                for (int i = 0; i < basicVarIndices.Count; i++)
                {
                    if (basicVarIndices[i] == j)
                    {
                        basicIndexRow = i;
                        break;
                    }
                }
                if (basicIndexRow != -1)
                {
                    newValues[0, j] = 0.0;
                    for (int i = 0; i < numConstraints; i++)
                    {
                        newValues[i + 1, j] = (i == basicIndexRow) ? 1.0 : 0.0;
                    }
                }
                else
                {
                    double[] aJ = new double[numConstraints];
                    for (int i = 0; i < numConstraints; i++)
                    {
                        aJ[i] = aOrig[i, j];
                    }
                    double[] aBarJ = MatrixHelpers.MatrixVectorMultiply(bInv, aJ);

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
            double zValue = MatrixHelpers.DotProduct(y, bOrig);
            newValues[0, numTotalVars] = Math.Round(zValue, 12);
            double[] bBar = MatrixHelpers.MatrixVectorMultiply(bInv, bOrig);
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
            int numConstraints = tableau.Height - 1;
            int numTotalVars = tableau.Width - 1;

            double[,] bInv = MatrixHelpers.CreateIdentityMatrix(numConstraints);
            var basicVarIndices = tableau.GetBasicVariableIndices().ToList();
            var nonBasicVarIndices = tableau.GetNonBasicVariableIndices().ToList();

            double[] c = new double[numTotalVars];
            for (int j = 0; j < numTotalVars; j++) c[j] = -tableau.Values[0, j];

            double[] b = new double[numConstraints];
            for (int i = 0; i < numConstraints; i++) b[i] = tableau.Values[i + 1, numTotalVars];

            double[,] A = new double[numConstraints, numTotalVars];
            for (int i = 0; i < numConstraints; i++)
                for (int j = 0; j < numTotalVars; j++)
                    A[i, j] = tableau.Values[i + 1, j];

            const int maxIterations = 128;
            for (int iteration = 0; ; iteration++)
            {
                if (iteration >= maxIterations)
                {
                    steps.Add("Algorithm terminated: Maximum iterations reached.");
                    break;
                }

                double[] cB = new double[numConstraints];
                for (int i = 0; i < basicVarIndices.Count; i++)
                {
                    cB[i] = c[basicVarIndices[i]];
                }
                double[] y = MatrixHelpers.VectorMatrixMultiply(cB, bInv);

                int enteringCol = -1;
                double maxReducedCost = 0.0;
                foreach (int j in nonBasicVarIndices)
                {
                    double yAj = 0;
                    for (int i = 0; i < numConstraints; i++)
                    {
                        yAj += y[i] * A[i, j];
                    }
                    double reducedCost = c[j] - yAj;

                    if (reducedCost > 1e-9 && reducedCost > maxReducedCost)
                    {
                        maxReducedCost = reducedCost;
                        enteringCol = j;
                    }
                }

                if (enteringCol == -1)
                {
                    Tableau finalTableau = ReconstructTableau(numConstraints, numTotalVars, bInv, basicVarIndices, A, b, c, tableau);
                    steps.Add(ConstructSolution(finalTableau));
                    break;
                }

                double[] d = new double[numConstraints];
                for (int i = 0; i < numConstraints; i++)
                {
                    for (int k = 0; k < numConstraints; k++)
                    {
                        d[i] += bInv[i, k] * A[k, enteringCol];
                    }
                }

                bool isUnbounded = true;
                for (int i = 0; i < numConstraints; i++)
                {
                    if (d[i] > 1e-9)
                    {
                        isUnbounded = false;
                        break;
                    }
                }
                if (isUnbounded)
                {
                    steps.Add("Unbounded solution.");
                    break;
                }

                double[] xB = MatrixHelpers.MatrixVectorMultiply(bInv, b);
                int leavingRowInBasis = -1;
                double minRatio = double.MaxValue;
                for (int i = 0; i < numConstraints; i++)
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

                double pivotElement = d[leavingRowInBasis];
                double[,] E = MatrixHelpers.CreateIdentityMatrix(numConstraints);
                for (int i = 0; i < numConstraints; i++)
                {
                    E[i, leavingRowInBasis] = (i == leavingRowInBasis) ? (1.0 / pivotElement) : (-d[i] / pivotElement);
                }
                bInv = MatrixHelpers.MatrixMultiply(E, bInv);
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
                //  Check for optimality by looking at the objective row
                int pivotColumn = -1;
                for (int j = 0; j < tableau.Width - 1; j++)
                {
                    if (tableau[0, j] < 0)
                    {
                        pivotColumn = j;
                        break;
                    }
                }
                if (pivotColumn == -1)
                {
                    // If no negative entries in objective row, optimal solution is found
                    steps.Add(ConstructSolution(tableau));
                    break;
                }

                // Determine the pivot row using the minimum ratio test
                double minRatio = double.PositiveInfinity;
                int pivotRow = -1;
                for (int i = 1; i < tableau.Height; i++)
                {
                    if (tableau[i, pivotColumn] > 0)
                    {
                        double numerator = tableau[i, tableau.Width - 1],
                               demoninator = tableau[i, pivotColumn],
                               ratio = numerator / demoninator;
                        if (ratio < 0) continue;
                        if (ratio == 0 && (numerator < 0) != (demoninator < 0)) continue;
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            pivotRow = i;
                        }
                    }
                }
                if (pivotRow == -1)
                {
                    // If no valid pivot row is found, the solution is unbounded
                    steps.Add("Unbounded solution");
                    break;
                }

                // Perform the pivot operation
                steps.Add(tableau.Pivot(pivotRow, pivotColumn));
            }
            steps.Add("End Primal Simplex");
            return steps;
        }

        // Constructs and returns a string representation of the optimal solution
        private static string ConstructSolution(Tableau tableau)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Optimal solution found.  ");
            foreach (var (name, value) in tableau.GetVariableIndices()
                .Select(j => (tableau.ColumnNames[j] /* */, tableau.GetVariableValue(j) /* */))
                .Append(/* */("Optimal Value(Z)" /*     */, tableau.ObjectiveValue /*      */)))
            {
                sb.AppendLine($"{name} = {value:0.###}  ");
            }
            return sb.ToString();
        }
    }
}
