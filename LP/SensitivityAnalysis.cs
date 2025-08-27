using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace LPR381.LP
{
    public class SensitivityAnalysis
    {
        public static List<String> Solve(Tableau tableau)
        {
            var steps = new List<String>() { "Start Sensitivity Analysis" };
            /* 
             * Display the range of a selected Non - Basic Variable.
             * Apply and display a change of a selected Non - Basic Variable.
             * Display the range of a selected Basic Variable.
             * Apply and display a change of a selected Basic Variable.
             * Display the range of a selected constraint right-hand - side value.
             * Apply and display a change of a selected constraint right-hand - side value.
             * Display the range of a selected variable in a Non-Basic Variable column.
             * Apply and display a change of a selected variable in a Non-Basic Variable column.
             * Add a new activity to an optimal solution.
             * Add a new constraint to an optimal solution.
             * Display the shadow prices.
             * Duality:
                 * Apply Duality to the programming model.
                 * Solve the Dual Programming Model.
                 * Verify whether the Programming Model has Strong or Weak Duality.
            */
            // Code here <3
            steps.Add("End Sensitivity Analysis");
            return steps;
        }
    }
}
