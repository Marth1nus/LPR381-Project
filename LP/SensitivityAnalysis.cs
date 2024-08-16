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

                analysis.AppendLine($"C{tableau.ColumnNames[j]} can range from ({Cb} - {allowableDecrease} = {Cb - allowableDecrease}) to ({Cb} + {allowableIncrease} = {Cb + allowableDecrease})");
            }

            return analysis.ToString();
        }
    }
}
