using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR381.LP
{
    public static class PrimalSimplex
    {


        #region Matrix and Vector Helpers
        // Helper method to create an identity matrix of a given size.
        private static double[,] CreateIdentityMatrix(int size)
        {
            double[,] matrix = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                matrix[i, i] = 1.0;
            }
            return matrix;
        }

        // Helper method for matrix-vector multiplication (M * v).
        private static double[] MatrixVectorMultiply(double[,] matrix, double[] vector)
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
            }
            return result;
        }

        // Helper method for vector-matrix multiplication (v * M).
        private static double[] VectorMatrixMultiply(double[] vector, double[,] matrix)
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

        // Helper method for matrix-matrix multiplication (A * B).
        private static double[,] MatrixMultiply(double[,] matrixA, double[,] matrixB)
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
                }
            }
            return result;
        }

        // Helper method for vector dot product.
        private static double DotProduct(double[] vectorA, double[] vectorB)
        {
            double result = 0;
            for (int i = 0; i < vectorA.Length; i++)
            {
                result += vectorA[i] * vectorB[i];
            }
            return result;
        }
        #endregion

        /// <summary>
        /// Reconstructs the full simplex tableau from the components of the revised method.
        /// This is used for logging/display purposes only.
        /// </summary>
        private static Tableau ReconstructTableau(
            int numConstraints, int numTotalVars,
            double[,] B_inv, List<int> basicVarIndices,
            double[,] A_orig, double[] b_orig, double[] c_orig,
            Tableau initialTableauForHeaders)
        {
            double[,] newValues = new double[numConstraints + 1, numTotalVars + 1];

            // Calculate y = c_B * B_inv
            double[] c_B = basicVarIndices.Select(index => c_orig[index]).ToArray();
            double[] y = VectorMatrixMultiply(c_B, B_inv);

            // Row 0: Objective Row
            // Tableau format shows y*A_j - c_j, which is -reduced_cost
            for (int j = 0; j < numTotalVars; j++)
            {
                if (basicVarIndices.Contains(j))
                {
                    newValues[0, j] = 0;
                }
                else
                {
                    double[] A_j = new double[numConstraints];
                    for (int i = 0; i < numConstraints; i++) A_j[i] = A_orig[i, j];
                    newValues[0, j] = DotProduct(y, A_j) - c_orig[j];
                }
            }
            // RHS of obj row: Z = y * b
            newValues[0, numTotalVars] = DotProduct(y, b_orig);

            // Rows 1 to m: Constraint Rows
            // Updated A matrix: A_bar = B_inv * A_orig
            double[,] A_bar = MatrixMultiply(B_inv, A_orig);
            for (int i = 0; i < numConstraints; i++)
            {
                for (int j = 0; j < numTotalVars; j++)
                {
                    newValues[i + 1, j] = A_bar[i, j];
                }
            }

            // Updated b vector: b_bar = B_inv * b_orig
            double[] b_bar = MatrixVectorMultiply(B_inv, b_orig);
            for (int i = 0; i < numConstraints; i++)
            {
                newValues[i + 1, numTotalVars] = b_bar[i];
            }

            var reconstructed = initialTableauForHeaders.Copy();
            reconstructed.Values = newValues;
            return reconstructed;
        }

        /// <summary>
        /// Constructs the final solution string from an optimal tableau.
        /// </summary>
        private static string ConstructSolutionRevised(Tableau tableau)
        {
            var sb = new StringBuilder();
            int width = tableau.Width;

            sb.AppendLine("Optimal solution found.");

            for (int j = 0; j < width - 1; j++) // For each variable column
            {
                int? basicRow = tableau.GetBasicVariableI(j);
                if (basicRow.HasValue)
                {
                    // It's a basic variable, its value is on the RHS of its pivot row.
                    sb.AppendLine($"{tableau.ColumnNames[j]} = {tableau.Values[basicRow.Value, width - 1]:F2}");
                }
                else
                {
                    // It's a non-basic variable, its value is 0.
                    sb.AppendLine($"{tableau.ColumnNames[j]} = 0.00");
                }
            }
            sb.Append($"Optimal Value (Z) = {tableau.ObjectiveValue:F2}");
            return sb.ToString();
        }

        /// <summary>
        /// Implements the Revised Primal Simplex algorithm to solve a linear programming problem.
        /// The output format matches the standard Primal Simplex method's step-by-step tableau.
        /// </summary>
        public static List<string> SolveRevised(Tableau tableau)
        {
            // Initial setup to match the requested output format
            var steps = new List<string> { "Start Revised Primal Simplex" };
            Tableau initialTableau = tableau.Copy(); // Keep original for headers and data
            steps.Add(initialTableau.ToString());

            int numConstraints = tableau.Height - 1;
            int numTotalVars = tableau.Width - 1;

            // 1. Initialization
            double[,] B_inv = CreateIdentityMatrix(numConstraints);

            var basicVarIndices = tableau.GetBasicVariableIndices().ToList();
            var nonBasicVarIndices = tableau.GetNonBasicVariableIndices().ToList();

            double[] c = new double[numTotalVars];
            for (int j = 0; j < numTotalVars; j++) c[j] = -initialTableau.Values[0, j];

            double[] b = new double[numConstraints];
            for (int i = 0; i < numConstraints; i++) b[i] = initialTableau.Values[i + 1, numTotalVars];

            double[,] A = new double[numConstraints, numTotalVars];
            for (int i = 0; i < numConstraints; i++)
                for (int j = 0; j < numTotalVars; j++)
                    A[i, j] = initialTableau.Values[i + 1, j];

            const int MAX_ITERATIONS = 100;
            for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
            {
                // A. Check for Optimality
                double[] c_B = basicVarIndices.Select(index => c[index]).ToArray();
                double[] y = VectorMatrixMultiply(c_B, B_inv);

                int enteringCol = -1;
                double maxReducedCost = 0.0;
                foreach (int j in nonBasicVarIndices)
                {
                    double[] A_j = new double[numConstraints];
                    for (int i = 0; i < numConstraints; i++) A_j[i] = A[i, j];
                    double reducedCost = c[j] - DotProduct(y, A_j);

                    if (reducedCost > 1e-9 && reducedCost > maxReducedCost)
                    {
                        maxReducedCost = reducedCost;
                        enteringCol = j;
                    }
                }

                if (enteringCol == -1)
                {
                    // OPTIMAL
                    Tableau finalTableau = ReconstructTableau(numConstraints, numTotalVars, B_inv, basicVarIndices, A, b, c, initialTableau);
                    steps.Add(ConstructSolutionRevised(finalTableau));
                    break;
                }

                // B. Ratio Test
                double[] A_entering = new double[numConstraints];
                for (int i = 0; i < numConstraints; i++) A_entering[i] = A[i, enteringCol];
                double[] d = MatrixVectorMultiply(B_inv, A_entering);

                if (d.All(val => val <= 1e-9))
                {
                    // UNBOUNDED
                    steps.Add("Unbounded solution");
                    break;
                }

                double[] x_B = MatrixVectorMultiply(B_inv, b);
                int leavingRowInBasis = -1;
                double minRatio = double.MaxValue;
                for (int i = 0; i < numConstraints; i++)
                {
                    if (d[i] > 1e-9)
                    {
                        double ratio = x_B[i] / d[i];
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            leavingRowInBasis = i;
                        }
                    }
                }

                int leavingCol = basicVarIndices[leavingRowInBasis];

                // C. LOGGING: Reconstruct tableau and perform pivot for display
                Tableau tableauForLogging = ReconstructTableau(numConstraints, numTotalVars, B_inv, basicVarIndices, A, b, c, initialTableau);
                // Find the actual row index in the full tableau for the leaving variable
                int? pivotRowForLogging = tableauForLogging.GetBasicVariableI(leavingCol);
                if (pivotRowForLogging.HasValue)
                {
                    steps.Add(tableauForLogging.Pivot(pivotRowForLogging.Value, enteringCol));
                }

                // D. PIVOT: Update the actual Revised Simplex state variables
                basicVarIndices[leavingRowInBasis] = enteringCol;
                nonBasicVarIndices.Remove(enteringCol);
                nonBasicVarIndices.Add(leavingCol);

                double pivotElement = d[leavingRowInBasis];
                double[,] E = CreateIdentityMatrix(numConstraints);
                for (int i = 0; i < numConstraints; i++)
                {
                    E[i, leavingRowInBasis] = (i == leavingRowInBasis) ? (1.0 / pivotElement) : (-d[i] / pivotElement);
                }
                B_inv = MatrixMultiply(E, B_inv);

                if (iteration == MAX_ITERATIONS - 1)
                {
                    steps.Add("Algorithm terminated: Maximum iterations reached.");
                }
            }

            steps.Add("End Revised Primal Simplex");
            return steps;
        }

        // Solve method implements Primal Simplex algorithm
        public static List<string> Solve(Tableau tableau)
        {
            var steps = new List<string> { "Start Primal Simplex" };
            for (int iteration = 0; iteration < 128; iteration++)
            {
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
            var result = new StringBuilder();
            result.AppendLine("Optimal Solution:  ");
            double optimalValue = tableau[0, tableau.Width - 1]; // Extract optimal value from RHS of objective row
            result.AppendLine($"Optimal Value: {optimalValue:0.###}  ");

            // Extract values of decision variables from the final tableau
            for (int j = 0; j < tableau.Width - 1; j++)
            {
                string varName = tableau.ColumnNames[j];
                double varValue = 0;
                for (int i = 1; i < tableau.Height; i++)
                {
                    if (tableau.RowNames[i].StartsWith("c") && tableau[i, j] == 1)
                    {
                        varValue = tableau[i, tableau.Width - 1];
                        break;
                    }
                }
                result.AppendLine($"{varName} = {varValue:0.###}  ");
            }
            return result.ToString();
        }
    }
}
