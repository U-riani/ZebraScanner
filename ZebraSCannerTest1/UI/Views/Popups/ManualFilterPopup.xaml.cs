using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Views.Popups
{
    public partial class ManualFilterPopup : ContentPage
    {
        private class FilterCondition
        {
            public string Joiner { get; set; } = string.Empty; // AND / OR / ""
            public string Expression { get; set; } = string.Empty;
            public override string ToString() => string.IsNullOrEmpty(Joiner)
                ? Expression
                : $"{Joiner} {Expression}";
        }

        private readonly List<FilterCondition> _conditions = new();
        private readonly TaskCompletionSource<string> _tcs = new();
        public Task<string> Result => _tcs.Task;

        public ManualFilterPopup()
        {
            InitializeComponent();
        }

        //private async void OnAddConditionClicked(object sender, EventArgs e)
        //{
        //    string field = FieldPicker.SelectedItem?.ToString();
        //    string op = OperatorPicker.SelectedItem?.ToString();
        //    string value = ValueEntry.Text?.Trim();

        //    if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(value))
        //    {
        //        await DisplayAlert("Missing", "Please select field, operator, and enter a value.", "OK");
        //        return;
        //    }

        //    string condition;

        //    // ?? Special handling for Price (string but compared as numeric)
        //    if (field == "Price" && decimal.TryParse(value, out _))
        //    {
        //        condition = $"CAST({field} AS REAL) {op} {value}";
        //    }
        //    // ?? LIKE ? case-insensitive
        //    else if (op == "LIKE")
        //    {
        //        condition = $"LOWER({field}) LIKE LOWER('%{value}%')";
        //    }
        //    // ?? Numeric comparison
        //    else if (int.TryParse(value, out _))
        //    {
        //        condition = $"{field} {op} {value}";
        //    }
        //    // ?? Default string comparison (case-insensitive)
        //    else
        //    {
        //        condition = $"LOWER({field}) {op} LOWER('{value}')";
        //    }

        //    // ?? Ask user how to combine with previous
        //    string joiner = string.Empty;
        //    if (_conditions.Count > 0)
        //    {
        //        joiner = await DisplayActionSheet("Combine with previous condition using:", "Cancel", null, "AND", "OR");
        //        if (joiner == "Cancel")
        //            return;
        //    }

        //    // Add condition
        //    _conditions.Add(new FilterCondition
        //    {
        //        Joiner = joiner,
        //        Expression = condition
        //    });

        //    // ?? Refresh visible list
        //    ConditionsList.ItemsSource = null;
        //    ConditionsList.ItemsSource = _conditions;

        //    // ?? Clear input field & reset pickers for smooth UX
        //    ValueEntry.Text = string.Empty;
        //    FieldPicker.SelectedItem = null;
        //    OperatorPicker.SelectedItem = null;
        //    ValueEntry.Unfocus();
        //}

        private async void OnAddConditionClicked(object sender, EventArgs e)
        {
            string field = FieldPicker.SelectedItem?.ToString();
            string op = OperatorPicker.SelectedItem?.ToString();
            string value = ValueEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(value))
            {
                await DisplayAlert("Missing", "Please select field, operator, and enter a value.", "OK");
                return;
            }

            string condition;

            // ?? Numeric fields
            if (field is "InitialQuantity" or "ScannedQuantity")
            {
                condition = $"{field} {op} {value}";
            }
            // ?? Price can be numeric but stored as text
            else if (field == "Price" && decimal.TryParse(value, out _))
            {
                condition = $"CAST({field} AS REAL) {op} {value}";
            }
            // ?? LIKE — always case-insensitive
            else if (op.Equals("LIKE", StringComparison.OrdinalIgnoreCase))
            {
                condition = $"LOWER({field}) LIKE LOWER('%{value}%')";
            }
            // ?? Default string comparison — make case-insensitive
            else
            {
                // Normalize =, !=, >, < etc. for string columns (use LOWER)
                condition = $"LOWER({field}) {op} LOWER('{value}')";
            }

            // ?? Ask user if they want AND/OR joiner
            string joiner = string.Empty;
            if (_conditions.Count > 0)
            {
                joiner = await DisplayActionSheet("Combine with previous condition using:", "Cancel", null, "AND", "OR");
                if (joiner == "Cancel")
                    return;
            }

            _conditions.Add(new FilterCondition
            {
                Joiner = joiner,
                Expression = condition
            });

            ConditionsList.ItemsSource = null;
            ConditionsList.ItemsSource = _conditions;

            // Reset inputs
            ValueEntry.Text = string.Empty;
            FieldPicker.SelectedItem = null;
            OperatorPicker.SelectedItem = null;
            ValueEntry.Unfocus();
        }


        private async void OnApplyFilterClicked(object sender, EventArgs e)
        {
            if (_conditions.Count == 0)
            {
                await DisplayAlert("Empty Filter", "No conditions were added.", "OK");
                return;
            }

            // ?? Combine conditions dynamically (each may have its own joiner)
            string final = string.Join(" ", _conditions.Select((c, i) =>
                i == 0 ? c.Expression : $"{c.Joiner} {c.Expression}"));

            _tcs.TrySetResult(final);
            await Navigation.PopModalAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            _tcs.TrySetResult(string.Empty);
            await Navigation.PopModalAsync();
        }
    }
}
