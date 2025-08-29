using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LPR381.LP
{
    public delegate List<string> Solver(Tableau tableau);
    public delegate List<string> SolverWithTwoParams(Tableau tableau, IEnumerable<int> variableIndices); 
    public delegate List<string> SolverWithThreeParams(Tableau tableau, int variableIndices, double newValue);

    public class Tableau
    {
        public string[] RowNames { get; set; } // example [max z, c1, c2]
        public string[] ColumnNames { get; set; } // example [x1, x2, s1, s2, rhs]
        public string[] ColumnRestrictions { get; set; } // one of [+, -, urs, int, bin]
        public double[,] Values { get; set; } // contains Width, Height, and the values in the tableau.
        public int TableauIteration { get; set; } // Just keeps track of how many pivots have been done.
        public Tableau InitialTableau { get; set; } // Tracks The intial table
        public int Height => Values.GetLength(0);
        public int Width => Values.GetLength(1);
        public double ObjectiveValue => Values[0, Width - 1];

        // Assume Values[, Width-1] is the RHS column
        // Assume Values[0,] is the Objective row

        public /*   */ double this[/*               */ int i, /*               */ int j]
        { 
            get => Values[i, j]; 
            set => Values[i, j] = value; 
        }
        public Vector<double> this[IEnumerable<int> iIndices, /*               */ int j]
        {
            get => Vector<double>.Build.DenseOfEnumerable(
                    (iIndices ?? Enumerable.Range(0, Height /* */)).Select((i, vi) => this[i, j]));
            set => Consume(
                    (iIndices ?? Enumerable.Range(0, Height /* */)).Select((i, vi) => this[i, j] = value[vi]));
        }
        public Vector<double> this[/*               */ int i, IEnumerable<int> jIndices]
        {
            get => Vector<double>.Build.DenseOfEnumerable(
                    (jIndices ?? Enumerable.Range(0, Width /*  */)).Select((j, vj) => this[i, j]));
            set => Consume(
                    (jIndices ?? Enumerable.Range(0, Width /*  */)).Select((j, vj) => this[i, j] = value[vj]));
        }
        public Matrix<double> this[IEnumerable<int> iIndices, IEnumerable<int> jIndices]
        {
            get => Matrix<double>.Build.DenseOfRows(
                    (iIndices ?? Enumerable.Range(0, Height /* */)).Select((i, vi) =>
                    (jIndices ?? Enumerable.Range(0, Width /*  */)).Select((j, vj) => this[i, j])));
            set => Consume(
                    (iIndices ?? Enumerable.Range(0, Height /* */)).Select((i, vi) =>
                    (jIndices ?? Enumerable.Range(0, Width /*  */)).Select((j, vj) => this[i, j] = value[vi, vj])).SelectMany(x => x));
        }

        public Tableau(int height = 2, int width = 3, bool maxElseMin = true)
        {
            RowNames /*           */ = Enumerable.Range(1, height - 1).Select(i => $"c{i}").Prepend(maxElseMin ? "max z" : "min z").ToArray();
            ColumnNames /*        */ = Enumerable.Range(1, width - 1).Select(j => $"x{j}").Append("rhs").ToArray();
            ColumnRestrictions /* */ = Enumerable.Repeat("+", width).ToArray();
            Values /*             */ = new double[height, width];
            TableauIteration /*   */ = 0;
        }

        public Tableau Copy() => new Tableau
        {
            RowNames /*           */ = RowNames.ToArray(),
            ColumnNames /*        */ = ColumnNames.ToArray(),
            ColumnRestrictions /* */ = ColumnRestrictions.ToArray(),
            Values /*             */ = Copy(Values),
            TableauIteration /*   */ = TableauIteration,
            InitialTableau /*     */ = InitialTableau
        };

        public Tableau Assign(Tableau other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            RowNames /*           */ = other.RowNames.ToArray();
            ColumnNames /*        */ = other.ColumnNames.ToArray();
            ColumnRestrictions /* */ = other.ColumnRestrictions.ToArray();
            Values /*             */ = Copy(other.Values);
            TableauIteration /*   */ = other.TableauIteration;
            InitialTableau /*     */ = other.InitialTableau;
            return this;
        }

        public int? GetFirstConstraintViolationJ(int jStart = 0)
        {
            for (int j = jStart; j < Width - 1; j++)
            {
                var optionalI = GetBasicVariableI(j);
                var value = optionalI.HasValue ? Values[optionalI.Value, Width - 1]
                                               : /* non basic variable: */ 0;
                switch (ColumnRestrictions[j])
                {
                    case   "+": if (value >= 0.0) break; else return j;
                    case   "-": if (value <= 0.0) break; else return j;
                    case    "":
                    case "urs": break;
                    case "int": if (value == Math.Floor(value)) break; else return j;
                    case "bin": if (value == 0.0 || value == 1) break; else return j;
                    default   : throw new Exception($"Invalid restriction {ColumnRestrictions[j]} at column {j}");
                }
            }
            return null;
        }
        public bool IsConstraintsSatisfied => GetFirstConstraintViolationJ() == null;
        public bool IsConstraintsDissatisfied => !IsConstraintsSatisfied;

        public (int i, bool feasible)? GetDualInoptimal()
        {
            for (int i = 1; i < Height; i++)
            {
                if (Values[i, Width - 1] < 0.0)
                {
                    int NegativeCount = 0;
                    for (int j = 0; j < Width; j++)
                        if (Values[i, j] < 0.0)
                            NegativeCount++;
                    return (i, NegativeCount > 0);
                }
            }
            return null; // Dual Optimal
        }
        public bool IsDualOptimal => GetDualInoptimal() == null;
        public bool IsDualInoptimal => !IsDualOptimal;
        public bool IsDualFeasible => GetDualInoptimal().GetValueOrDefault((0, true)).feasible;
        public bool IsDualInfeasible => !IsDualFeasible;

        public (int j, bool feasible, bool dualNeeded)? GetPrimalInoptimal()
        {
            if (!IsDualOptimal)
                return (-1, false, true); // Dual Simplex needed
            for (int j = 0; j < Width - 1; j++)
                if (Values[0, j] < 0.0)
                    return (j, true, false);
            return null; // Primal Optimal
        }
        public bool IsPrimalOptimal => GetPrimalInoptimal() == null;
        public bool IsPrimalInoptimal => !IsPrimalOptimal;
        public bool IsPrimalFeasible => GetPrimalInoptimal().GetValueOrDefault((0, true, false)).feasible;
        public bool IsPrimalInfeasible => !IsPrimalFeasible;

        public bool IsOptimal => IsPrimalOptimal && IsConstraintsSatisfied;
        public bool IsInoptimal => !IsOptimal;
        public bool IsFeasible => IsPrimalFeasible;
        public bool IsInfeasible => !IsFeasible;

        public int? GetBasicVariableI(int j, double expectedSingularNonZero = 1.0)
        {
            if (!(0 <= j && j < Width - 1))
                throw new ArgumentOutOfRangeException($"{nameof(j)} must be in range [{0}..{Width - 2}]");
            int? indexOf1 = null;
            for (int i = 0; i < Height; i++)
            {
                if (Values[i, j] == /* 1.0 */ expectedSingularNonZero)
                {
                    if (indexOf1 != null)
                        return null;
                    indexOf1 = i;
                    continue;
                }
                else if (Values[i, j] != 0.0)
                    return null;
            }
            return indexOf1;
        }
        public bool IsBasicVariable /*        */(int j) => /*  */ GetBasicVariableI(j, +1.0).HasValue;
        public bool IsNonBasicVariable /*     */(int j) => /* */ !GetBasicVariableI(j, +1.0).HasValue;
        public bool IsBasicLikeVariable /*    */(int j) => /*  */ GetBasicVariableI(j, -1.0).HasValue;
        public bool IsNonBasicLikeVariable /* */(int j) => /* */ !GetBasicVariableI(j, -1.0).HasValue;

        public IEnumerable<int> IndicesForConstraints /*           */ => Enumerable.Range(1, Height /* */ - 1);
        public IEnumerable<int> IndicesForVariables /*             */ => Enumerable.Range(0, Width /*  */ - 1);
        public IEnumerable<int> IndicesForDecisionVariables /*     */ => IndicesForVariables.Where(j => ColumnNames[j].StartsWith("x"));
        public IEnumerable<int> IndicesForSlackVariables /*        */ => IndicesForVariables.Where(j => !ColumnNames[j].StartsWith("x"));
        public IEnumerable<int> IndicesForBasicVariables /*        */ => IndicesForVariables.Where(IsBasicVariable /*        */);
        public IEnumerable<int> IndicesForNonBasicVariables /*     */ => IndicesForVariables.Where(IsNonBasicVariable /*     */);
        public IEnumerable<int> IndicesForBasicLikeVariables /*    */ => IndicesForVariables.Where(IsBasicLikeVariable /*    */);
        public IEnumerable<int> IndicesForNonBasicLikeVariables /* */ => IndicesForVariables.Where(IsNonBasicLikeVariable /* */);

        public double GetVariableValue(int j)
        {
            var optionalI = GetBasicVariableI(j);
            return optionalI.HasValue ? Values[optionalI.Value, Width - 1]
                                      : /* non basic variable: */ 0.0;
        }

        public IEnumerable<double> GetVariableValues() => IndicesForVariables.Select(GetVariableValue);

        public Vector<double> Get_c /*    */() => (InitialTableau ?? this)[/*                     */ 0, IndicesForDecisionVariables /*  */];
        public Vector<double> Get_b /*    */() => (InitialTableau ?? this)[/* */ IndicesForConstraints, Width - 1 /*                    */];
        public Matrix<double> Get_A /*    */() => (InitialTableau ?? this)[/* */ IndicesForConstraints, IndicesForDecisionVariables /*  */];
        public Matrix<double> Get_B /*    */() => /*             */ (this)[/* */ IndicesForConstraints, IndicesForBasicVariables /*     */];
        public Matrix<double> Get_N /*    */() => /*             */ (this)[/* */ IndicesForConstraints, IndicesForNonBasicVariables /*  */];
        public Vector<double> Get_cBv /*  */() => /*             */ (this)[/*                      */ 0, IndicesForBasicVariables /*    */];
        public Vector<double> Get_cNBv /* */() => /*             */ (this)[/*                      */ 0, IndicesForNonBasicVariables /* */];

        public void ValidateLengths()
        {
            if (!(Height >= 2 && Width >= 3))
                throw new Exception("Table must be at least 2x3");
            if (!(RowNames.Length == Height))
                throw new Exception("Row Length inconsistent");
            if (!(ColumnNames.Length == Width))
                throw new Exception("Column Length inconsistent");
            if (!(ColumnRestrictions.Length <= Width))
                throw new Exception("Column Restriction Length too long");
        }

        public string Pivot(int rowI, int colI)
        {
            InitialTableau = InitialTableau ?? Copy();
            TableauIteration++;
            double pivot = Values[rowI, colI];
            for (int j = 0; j < Width; j++)
            {
                Values[rowI, j] /= pivot;
            }
            for (int i = 0; i < Height; i++)
            {
                if (i == rowI) 
                    continue;
                double factor = Values[i, colI];
                for (int j = 0; j < Width; j++)
                {
                    Values[i, j] -= factor * Values[rowI, j];
                }
            }
            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    Values[i, j] = Math.Round(Values[i, j], 12);
                }
            }
            return $"Pivot on **{RowNames[rowI]}**, **{ColumnNames[colI]}**\n\n{this}";
        }

        public void AddRow(double[] newRow = null, string name = null)
        {
            newRow = newRow ?? new double[Width];
            if (newRow.Length != Width)
                throw new ArgumentException($"New row must have {Width} values");
            var oldValues = Values;
            Values = new double[Height + 1, Width];
            int i = 0;
            for (; i < Height - 1; i++)
                for (int j = 0; j < Width; j++)
                    Values[i, j] = oldValues[i, j];
            for (; i < Height; i++)
                for (int j = 0; j < Width; j++)
                    Values[i, j] = newRow[j];
            RowNames = RowNames.Append(name ?? $"c{Height - 1}").ToArray();
        }

        public void RemoveRow(int rowI = -1)
        {
            if (rowI < 0)
                rowI += Height; // -1 means last row
            if (rowI < 0 || Height <= rowI)
                throw new ArgumentException($"Out of range rowI:{rowI} parameter");
            var oldValues = Values;
            Values = new double[Height - 1, Width];
            int i = 0;
            for (; i < rowI; i++)
                for (int j = 0; j < Width; j++)
                    Values[i, j] = oldValues[i, j];
            for (; i < Height; i++)
                for (int j = 0; j < Width; j++)
                    Values[i, j] = oldValues[i + 1, j];
        }

        public void AddColumn(double[] newColumn = null, string name = null, string restriction = "urs")
        {
            newColumn = newColumn ?? new double[Width];
            if (newColumn.Length != Height)
                throw new ArgumentException($"New row must have {Height} values");
            var oldValues = Values;
            Values = new double[Height, Width + 1];
            int j = 0;
            for (; j < Width - 2; j++)
                for (int i = 0; i < Height; i++)
                    Values[i, j] = oldValues[i, j];
            for (; j < Width - 1; j++)
                for (int i = 0; i < Height; i++)
                    Values[i, j] = newColumn[i];
            for (; j < Width; j++)
                for (int i = 0; i < Height; i++)
                    Values[i, j] = oldValues[i, j - 1];
            var columnNamesLast = ColumnNames.Length > 0 ? ColumnNames.Last() : "rhs";
            ColumnNames = ColumnNames.Take(ColumnNames.Length - 1).Append(name ?? $"s{Height}").Append(columnNamesLast).ToArray();
            ColumnRestrictions = ColumnRestrictions.Append(restriction).ToArray();
        }

        public void RemoveColumn(int colI = -2)
        {
            if (colI < 0)
                colI += Width; // -1 means last column
            if (colI < 0 || Width <= colI)
                throw new ArgumentException($"Out of range colI:{colI} parameter");
            var oldValues = Values;
            Values = new double[Height, Width - 1];
            for (int i = 0; i < Height; i++)
            {
                int j = 0;
                for (; j < colI; j++)
                    Values[i, j] = oldValues[i, j];
                for (; j < Width; j++)
                    Values[i, j] = oldValues[i, j + 1];
            }
        }

        public int AddBinaryLessThanOneConstraints()
        {
            int heightBeforeAdding = Height;
            for (int j = 0; j < Width - 1; j++)
            {
                if (ColumnRestrictions[j] != "bin")
                    continue;
                AddColumn(new double[Height], $"s{Height}", "+");
                var newRow = new double[Width];
                newRow[j /*   */] = 1; // decision variable
                newRow[Width - 2] = 1; // slack variable
                newRow[Width - 1] = 1; // RHS
                AddRow(newRow, $"c{Height}");
            }
            return Height - heightBeforeAdding;
        }

        public override string ToString()
        {
            const int colWidth = 8, // "-000.000".Length
                      decimalLength = 3;
            StringBuilder sb = new StringBuilder();
            /* | T1     |     x1 |     s1 |    rhs | */
            sb.Append($"| T{TableauIteration,1 - colWidth} ");
            for (int j = 0; j < Width; j++)
                sb.Append($"| {ColumnNames[j].PadLeft(colWidth - 1 - decimalLength),-colWidth} ");
            sb.AppendLine($"|");
            /* | ------ | ------ | ------ | ------ | */
            for (int j = 0; j < Width + 1; j++)
                sb.Append($"| {"-:".PadLeft(colWidth, '-')} ");
            sb.AppendLine($"|");
            /* |  max Z | 00.000 | 00.000 | 00.000 | */
            /* |     C1 | 00.000 | 00.000 | 00.000 | */
            for (int i = 0; i < Height; i++)
            {
                sb.Append($"| {RowNames[i],colWidth} ");
                for (int j = 0; j < Width; j++)
                    sb.Append($"| {FormatDouble(Values[i, j], colWidth, decimalLength)} ");
                sb.AppendLine($"|");
            }
            /* |   Sign |    int |      + |        | */
            sb.Append($"| {"",colWidth} ");
            for (int j = 0; j < Width; j++)
                sb.Append($"| {(j < ColumnRestrictions.Length ? ColumnRestrictions[j] : "").PadLeft(colWidth - 1 - decimalLength),-colWidth} ");
            sb.AppendLine($"|");
            return sb.ToString();
        }

        public static string FormatDouble(double value, int columnWidth = 8, int decimalLength = 3)
        {
            if (!value.IsFinite())
                return value.ToString().PadLeft(columnWidth);
            var valueString = value.ToString(decimalLength <= 0 ? "0" : "0.".PadRight(2 + decimalLength, '#'));
            var valueStringIndexOfDot = valueString.IndexOf(".");
            if (valueStringIndexOfDot == -1)
                valueStringIndexOfDot = valueString.Length;
            var valueStringTargetLength = valueStringIndexOfDot + 1 + decimalLength;
            valueString = valueString.PadRight(valueStringTargetLength).PadLeft(columnWidth);
            return valueString;
        }

        private static (string[] objectiveLine, string[][] constraintLines, string[] restrictionsLine) FromFileValidateFile(string filename)
        {
            // TODO: canonical form out param
            var lines = File.ReadAllLines(filename, Encoding.UTF8)
                .Select(line => Regex.Split(line.Trim(), @"\s+"))
                .ToArray();
            if (lines.Length < 2)
                throw new Exception("Too Few Rows");

            var objectiveLine = lines.First();
            var constraintLines = lines.Skip(1).Reverse().Skip(1).Reverse().ToArray();
            var restrictionsLine = lines.Last();
            var rowLength = objectiveLine.Length;
            lines = null; // use named variables instead
            var lastChecked = "";

            if (objectiveLine.Length < 2) // must include at least one decision variable
                throw new Exception("Objective Row has too few columns");
            if (!objectiveLine.Skip(1).All(col => Regex.IsMatch(lastChecked = col, @"^[+-]\d$"))) // validate coefficients
                throw new Exception($"Objective Row contains invalid coefficient. While checking: {lastChecked}");
            if (!objectiveLine.Take(1).All(col => Regex.IsMatch(lastChecked = col, @"^(min|max)$"))) // ojective row must start with min/max
                throw new Exception($"Objecive Row must start with min/max. While checking: {lastChecked}");

            if (!constraintLines.All(line => line.Length == rowLength)) // object and constaint rows must have the same width
                throw new Exception($"Constraint Rows have inconsistent lengths");
            if (!constraintLines.All(line => line.Reverse().Skip(1).All(col => Regex.IsMatch(lastChecked = col, @"^[+-]\d+$"))))
                throw new Exception($"Constraint Rows contains invalid coefficient. While checking: {lastChecked}");
            if (!constraintLines.All(line => line.Reverse().Take(1).All(col => Regex.IsMatch(lastChecked = col, @"^(=|<=|>=)\d+$"))))
                throw new Exception($"Constraint Rows contains invalid rhs. While checking: {lastChecked}");

            if (!(restrictionsLine.Length == rowLength - 1)) // object and constaint rows must have the same width
                throw new Exception("Restrictions Row have inconsistent lengths");
            if (!restrictionsLine.All(col => Regex.IsMatch(lastChecked = col, @"^(\+|-|urs|int|bin)$"))) // restrictions row must have valid restrictions
                throw new Exception($"Restrictions Row contains unknown symbol. While checking: {lastChecked}");
            return (objectiveLine, constraintLines, restrictionsLine);
        }

        public static Tableau FromFile(string filename)
        {
            var (objectiveLine, constraintLines, restrictionsLine) = FromFileValidateFile(filename);

            // Row Names
            var rowNamesList = new List<string> { $"{objectiveLine.First()} z" };
            for (int i = 0; i < constraintLines.Length; i++)
                rowNamesList.Add($"c{1 + i}");
            var rowNames = rowNamesList.ToArray();
            rowNamesList = null;

            // Column Names
            var columnNamesList = new List<string>();
            for (int j = 0; j < objectiveLine.Length - 1; j++) // Decision variables
                columnNamesList.Add($"x{1 + j}");
            for (int i = 0; i < constraintLines.Length; i++) // Slack/Excess variables
                columnNamesList.AddRange(
                    constraintLines[i].Last().StartsWith("<=") ? new[] { $"s{1 + i}" } :
                    constraintLines[i].Last().StartsWith(">=") ? new[] { $"e{1 + i}" } :
                    constraintLines[i].Last().StartsWith("=") ? new[] { $"s{1 + i}", $"e{1 + i}" } :
                    throw new Exception("Unkown constraint ineqality"));
            columnNamesList.Add($"rhs"); // RHS
            var columnNames = columnNamesList.ToArray();
            columnNamesList = null;

            // Column Restructions
            var columnRestrictions = columnNames.Select((_, j) => j < restrictionsLine.Length ? restrictionsLine[j] : "+").ToArray();

            // Values
            var height = 1 + constraintLines.Length;
            var widthWithoutSlacks = constraintLines.First().Length;
            var widthOfSlacks = constraintLines.Sum(line => line.Last().StartsWith("=") ? 2 /*slack and excess*/ : 1 /* slack xor excess */);
            var width = widthWithoutSlacks + widthOfSlacks;
            var values = new double[height, width];
            {
                int i = 0;
                int jSlackOrExcess = widthWithoutSlacks - 1;
                for (; i < 1; i++) // Objective Row
                {
                    int j = 0;
                    for (; j < objectiveLine.Length - 1; j++) // Decision Variables
                        values[i, j] = -double.Parse(objectiveLine[1 + j]);
                    for (; j < width; j++) // Slack|Excess Variables and RHS 
                        values[i, j] = 0;
                }
                for (; i < height; i++) // Constraint Rows
                {
                    var rhs = constraintLines[i - 1].Last();
                    var hasS = rhs.StartsWith("<=") || rhs.StartsWith("=");
                    var hasE = rhs.StartsWith(">=") || rhs.StartsWith("=");
                    rhs = rhs.Substring(rhs.StartsWith("=") ? 1 : 2);

                    int j = 0;
                    for (; j < constraintLines[i - 1].Length - 1; j++) // Decision Variables
                        values[i, j] = double.Parse(constraintLines[i - 1][j]);
                    for (; j < width - 1; j++) // Slack|excess Variables
                        values[i, j] = 0;
                    for (; j < width; j++) // RHS
                        values[i, j] = double.Parse(rhs);

                    if (hasS) values[i, jSlackOrExcess++] = 1;
                    if (hasE) values[i, jSlackOrExcess++] = -1;
                    if (hasE) for (j = 0; j < width; j++) values[i, j] *= -1;
                }
            }

            var res = new Tableau
            {
                RowNames /*           */ = rowNames,
                ColumnNames /*        */ = columnNames,
                ColumnRestrictions /* */ = columnRestrictions,
                Values /*             */ = values,
                TableauIteration /*     */ = 0
            };
            res.AddBinaryLessThanOneConstraints();
            res.InitialTableau = res.Copy();
            return res;
        }

        public static string FromFileCanonicalForm(string filename)
        {
            var canonicalForm = "";
            var (objectiveLine, constraintLines, restrictionsLine) = FromFileValidateFile(filename);
            canonicalForm += $"# Canonical Form";
            canonicalForm += $"\n\n## Objective\n\n{objectiveLine[0]} z = {string.Join(" + ", objectiveLine.Skip(1).Select((col, j) => $"{double.Parse(col)}x{1 + j}"))}";
            canonicalForm += $"\n\n## Constraints\n\n" + string.Join("\n", constraintLines.Select((line, i) =>
                string.Join(" + ", line.Take(line.Length - 1).Select((col, j) => $"{double.Parse(col)}x{1 + j}")) +
                (line.Last().StartsWith("=") || line.Last().StartsWith("<=") ? $" + s{1 + i}" : "") +
                (line.Last().StartsWith("=") || line.Last().StartsWith(">=") ? $" + -e{1 + i}" : "") +
                $" = {double.Parse(line.Last().Substring(line.Last().StartsWith("=") ? 1 /* = */ : 2 /* <= or >= */))}"));
            canonicalForm += $"\n\n## Restrictions\n\n{string.Join(", ", restrictionsLine.Select((v, j) => $"x{1 + j}:{v}"))}";
            return canonicalForm;
        }

        private static double[,] Copy(double[,] from, double[,] to = null,
            int /*     */ CountI = 0, int /*      */ CountJ = 0, // 0 means use `from`'s dimention
            int /* */ fromStartI = 0, int /*  */ fromStartJ = 0,
            int /*   */ toStartI = 0, int /*    */ toStartJ = 0)
        {
            to = to ?? new double[from.GetLength(0), from.GetLength(1)];
            CountI = CountI > 0 ? CountI : from.GetLength(0);
            CountJ = CountJ > 0 ? CountJ : from.GetLength(1);
            if (!(to.GetLength(0) <= toStartI + CountI)) throw new ArgumentOutOfRangeException($"Out of Range");
            if (!(to.GetLength(1) <= toStartJ + CountJ)) throw new ArgumentOutOfRangeException($"Out of Range");
            if (!(from.GetLength(0) <= fromStartI + CountI)) throw new ArgumentOutOfRangeException($"Out of Range");
            if (!(from.GetLength(1) <= fromStartJ + CountJ)) throw new ArgumentOutOfRangeException($"Out of Range");
            for (int i = 0; i < CountI; i++)
                for (int j = 0; j < CountJ; j++)
                    to[toStartI + i, toStartJ + j] = from[fromStartI + i, fromStartJ + j];
            return to;
        }

        public static void Consume<T>(IEnumerable<T> enumerable)
        {
            foreach (var _ in enumerable)
            {
            }
        }
    }
}
