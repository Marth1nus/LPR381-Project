using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381.LP
{
    public class SensitivityAnalysis
    {
        public static string Analise(Tableau tableau)
        {
            var analysis = new StringBuilder();

            var basicVariableIndices = tableau.GetBasicVariableIndices().ToArray();
            var nonBasicVariableIndices = tableau.GetNonBasicVariableIndices().ToArray();

            var B = Matrix<double>.Build.DenseOfColumnArrays(
                basicVariableIndices.Select(
                    j => Enumerable.Range(1, tableau.InitialTable.Height - 1).Select(
                    i => tableau.InitialTable[i, j]).ToArray()).ToArray());
            var BInverse = B.Inverse();

            var Cbv = Vector<double>.Build.DenseOfEnumerable(
                basicVariableIndices.Select(
                    j => tableau.InitialTable[0, j]));

            var shadowPrices = Cbv * BInverse;

            for (int k = 0; k < basicVariableIndices.Length; k++)
            {
                var j = basicVariableIndices[k];
                var Cb = Cbv[k];

                var allowableIncrease = double.PositiveInfinity;
                var allowableDecrease = double.PositiveInfinity;

                for (int i = 1; i < tableau.Height; i++)
                {
                    var coefficient = tableau.InitialTable[i, j];
                    if (coefficient > 0)
                        allowableIncrease = Math.Min(allowableIncrease, shadowPrices[i - 1] / coefficient);
                    else if (coefficient < 0)
                        allowableDecrease = Math.Min(allowableDecrease, -shadowPrices[i - 1] / coefficient);
                }

                analysis.AppendLine(
                    $"Coefficient of {tableau.ColumnNames[j]} (    Basic) can range " +
                    $"from ({Cb,4} - {allowableDecrease,4} = {Cb - allowableDecrease,4}) " +
                    $"to ({Cb,4} + {allowableIncrease,4} = {Cb + allowableIncrease,4})");
            }

            // Analyze non-basic variables
            for (int k = 0; k < nonBasicVariableIndices.Length; k++)
            {
                var j = nonBasicVariableIndices[k];
                var cj = tableau.InitialTable[0, j];
                var reducedCost = cj - shadowPrices * Vector<double>.Build.DenseOfEnumerable(
                        Enumerable.Range(1, tableau.InitialTable.Height - 1).Select(
                            i => tableau.InitialTable[i, j]));

                var allowableIncrease = -reducedCost;
                var allowableDecrease = reducedCost;

                analysis.AppendLine(
                    $"Coefficient of {tableau.ColumnNames[j]} (Non-Basic) can range "+
                    $"from ({cj,4} - {allowableDecrease,4} = {cj - allowableDecrease,4}) " +
                    $"to ({cj,4} + {allowableIncrease,4} = {cj + allowableIncrease,4})");
            }

            return analysis.ToString();
        }
    }
}
