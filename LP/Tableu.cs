using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPR381.LP
{
    public struct Tableu
    {
        public string[] RowNames { get; set; } // example [max z, c1, c2]
        public string[] ColumnNames { get; set; } // example [x1, x2, s1, s2] (rhs is excluded)
        public string[] ColumnRestrictions { get; set; } // one of [+, -, urs, int, bin]
        public double[,] Values { get; set; } // contains Width, Height, and the values in the tableu.
        public int TableIteration { get; set; } // Just keeps track of how many pivots have been done.
        public uint Height { get { return (uint)Values.GetLength(0); } }
        public uint Width { get { return (uint)Values.GetLength(1); } }

        public string Pivot(uint rowI, uint colI)
        {
            double pivot = Values[rowI, colI];
            for (uint j = 0; j < Values.GetLength(1); j++)
            {
                Values[rowI, j] /= pivot;
            }
            for (uint i = 0; i < Values.GetLength(0); i++)
            {
                if (i == rowI)
                    continue;
                double factor = Values[i, colI];
                for (uint j = 0; j < Values.GetLength(1); j++)
                {
                    Values[i, j] -= factor * Values[rowI, j];
                }
            }
            TableIteration++;
            return $"Pivot on {RowNames[rowI]}, {ColumnNames[colI]}";
        }

        public void AddRow(double[] values, string name)
        {
            if (values.Length != Values.GetLength(0))
                throw new ArgumentException($"New row must have {Values.GetLength(0)} values");
            var oldValues = Values;
            Values = new double[oldValues.GetLength(0) + 1, oldValues.GetLength(1)];
            for (uint i = 0; i < oldValues.GetLength(0); i++)
                for (uint j = 0; j < oldValues.GetLength(1); j++)
                    Values[i, j] = oldValues[i, j];
            for (uint j = 0; j < values.Length; j++)
                Values[Values.GetLength(0) - 1, j] = values[j];
            RowNames.Append(name);
        }

        public void RemoveRow(uint rowI)
        {
            if (rowI >= Values.GetLength(0))
                throw new ArgumentException($"Out of range rowI:{rowI} parameter");
            var oldValues = Values;
            Values = new double[oldValues.GetLength(0) - 1, oldValues.GetLength(1)];
            for (uint i = 0; i < oldValues.GetLength(0); i++)
                for (uint j = 0; j < oldValues.GetLength(1); j++)
                    Values[i - (i > rowI ? 1 : 0), j] = oldValues[i, j];
        }

        public void AddColumn(double[] values, string name, string restriction = "urs")
        {
            if (values.Length != Values.GetLength(0))
                throw new ArgumentException($"New row must have {Values.GetLength(0)} values");
            var oldValues = Values;
            Values = new double[oldValues.GetLength(0), oldValues.GetLength(1) + 1];
            for (uint i = 0; i < oldValues.GetLength(0); i++)
                for (uint j = 0; j < oldValues.GetLength(1); j++)
                    Values[i, j] = oldValues[i, j];
            for (uint i = 0; i < values.Length; i++)
                Values[i, Values.GetLength(1) - 1] = values[i];
            ColumnNames.Append(name);
            ColumnRestrictions.Append(restriction);
        }

        public void RemoveColumn(uint colI)
        {
            if (colI >= Values.GetLength(1))
                throw new ArgumentException($"Out of range colI:{colI} parameter");
            var oldValues = Values;
            Values = new double[oldValues.GetLength(0), oldValues.GetLength(1) - 1];
            for (uint i = 0; i < oldValues.GetLength(0); i++)
                for (uint j = 0; j < oldValues.GetLength(1); j++)
                    Values[i, j - (j > colI ? 1 : 0)] = oldValues[i, j];
        }

        public override string ToString()
        {
            const int colWidth = 6; // "00.000".Length
            StringBuilder sb = new StringBuilder();
            /* | T1     |     x1 |     s1 |    rhs | */
            sb.Append($"| T{TableIteration,1 - colWidth} ");
            for (uint j = 0; j < Width - 1; j++)
                sb.Append($"| {ColumnNames[j], colWidth} ");
            sb.AppendLine($"| {"rhs", colWidth}");
            /* | -----: | -----: | -----: | -----: | */
            for (uint j = 0; j < Width + 1; j++)
                sb.Append($"| {"-----:", colWidth} ");
            sb.AppendLine($"|");
            /* |  max Z | 00.000 | 00.000 | 00.000 | */
            /* |     C1 | 00.000 | 00.000 | 00.000 | */
            for (uint i = 0; i < Height; i++)
            {
                sb.Append($"| {RowNames[i], colWidth} ");
                for (uint j = 0; j < Width; j++)
                    sb.Append($"| {Values[i, j], colWidth:F3} ");
                sb.AppendLine($"|");
            }
            /* |   Sign |    int |      + |        | */
            sb.Append($"| {"Sign", colWidth} ");
            for (uint j = 0; j < Width - 1; j++)
                sb.Append($"| {ColumnRestrictions[j], colWidth} ");
            sb.AppendLine($"| {"", colWidth} |");
            return sb.ToString();
        }

        public static Tableu FromFile(string filename)
        {
            var lines = File.ReadAllLines(filename, Encoding.UTF8)
                .Select(line => Regex.Split(line, @"\s+"))
                .ToArray();
            if (lines.Length < 2)
                throw new Exception("Too Few Rows");

            var objectiveLine = lines.First();
            var constraintLines = lines.Skip(1).Reverse().Skip(1).Reverse().ToArray();
            var restrictionsLine = lines.Last();
            var rowLength = objectiveLine.Length;
            lines = null; // use named variables instead

            if (objectiveLine.Length < 2) // must include at least one decision variable
                throw new Exception("Objective Row has too few columns");
            if (!objectiveLine.Skip(1).All(col => Regex.IsMatch(col, @"^[+-]\d$"))) // validate coefficients
                throw new Exception("Objective Row contains invalid coefficient");
            if (!objectiveLine.Take(1).All(col => Regex.IsMatch(col, @"^(min|max)$"))) // ojective row must start with min/max
                throw new Exception("Objecive Row must start with min/max");

            if (!constraintLines.All(line => line.Length == rowLength)) // object and constaint rows must have the same width
                throw new Exception("Constraint Rows have inconsistent lengths");
            if (!constraintLines.All(line => line.Reverse().Skip(1).All(col => Regex.IsMatch(col, @"^[+-]\d$"))))
                throw new Exception("Constraint Rows contains invalid coefficient");
            if (!constraintLines.All(line => line.Reverse().Take(1).All(col => Regex.IsMatch(col, @"^(=|<=|>=)\d+$"))))
                throw new Exception("Constraint Rows contains invalid rhs");

            if (!restrictionsLine.All(line => line.Length == rowLength)) // object and constaint rows must have the same width
                throw new Exception("Restrictions Row have inconsistent lengths");
            if (!restrictionsLine.All(col => Regex.IsMatch(col, @"^(\+|-|urs|int|bin)$"))) // restrictions row must have valid restrictions
                throw new Exception("Restrictions Row contains unknown symbol");

            // Row Names
            var rowNamesList = new List<string>();
            rowNamesList.Add($"{objectiveLine.First()} z");
            for (int i = 0; i < constraintLines.Length; i++)
                rowNamesList.Add($"c{1 + i}");
            var rowNames = rowNamesList.ToArray();
            rowNamesList = null;

            // Column Names
            var columnNamesList = new List<string>();
            for (int j = 0; j < objectiveLine.Length - 1; j++)
                columnNamesList.Add($"x{1 + j}");
            for (int i = 0; i < constraintLines.Length; i++)
            {
                var line = constraintLines[i];
                var matches = Regex.Matches(line.Last(), @"^(=|<=|>=)(\d+)$");
                var ineq = matches[1].Value;
                var rhs = matches[2].Value;
                line[line.Length - 1] = rhs;
                if (ineq == "=" || ineq == "<=")
                    columnNamesList.Add($"s{1 + i}");
                if (ineq == "=" || ineq == ">=")
                    columnNamesList.Add($"e{1 + i}");
                if (ineq == "=" || ineq == "<=") 
                    columnNamesList.Add($"s{1 + i}"); // add a slack varaible
                if (ineq == "=" || ineq == ">=")
                    columnNamesList.Add($"e{1 + i}"); // add an excess variable
            }
            var columnNames = columnNamesList.ToArray();
            columnNamesList = null;

            // Column Restructions
            var columnRestrictions = columnNames.Select((_, j) => j < restrictionsLine.Length ? restrictionsLine[j] : "+").ToArray();

            // Values
            var height = 1 + constraintLines.Length;
            var width = columnNames.Length + 1;
            var values = new double[height, width];
            for (int i = 1; i < height; i++)
                for (int j = 0; j < width; j++)
                    values[i, j] = 
                        i == 0 // Objective row
                        ? j < objectiveLine.Length - 1 
                            ? -double.Parse(objectiveLine[1 + j]) 
                            : 0
                        : j < constraintLines[i - 1].Length - 1 // Constaint coefficients
                        ? double.Parse(constraintLines[i - 1][j]) 
                        : j == width - 1 // RHS
                        ? double.Parse(constraintLines[i - 1].Last())
                        : 0;
            for (int j = 1; j < height; j++)
            {
                var s = columnNames[j].StartsWith("s");
                var e = columnNames[j].StartsWith("e");
                if (!s && !e)
                    continue;
                int i = int.Parse(columnNames[j].Substring(1));
                values[i, j] = s ? 1 : e ? -1 : 0;
            }    

            return new Tableu()
            {
                RowNames = rowNames,
                ColumnNames = columnNames,
                ColumnRestrictions = columnRestrictions,
                Values = values,
                TableIteration = 0
            };
        }
    }
}
