using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Views
{
    public partial class ManualFilterPopup : ContentPage
    {
        private readonly List<string> _conditions = new();
        private readonly TaskCompletionSource<string> _tcs = new();
        public Task<string> Result => _tcs.Task;

        public ManualFilterPopup()
        {
            InitializeComponent();
        }

        private void OnAddConditionClicked(object sender, EventArgs e)
        {
            string field = FieldPicker.SelectedItem?.ToString();
            string op = OperatorPicker.SelectedItem?.ToString();
            string value = ValueEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(value))
            {
                DisplayAlert("Missing", "Please select field, operator, and enter a value.", "OK");
                return;
            }

            string formattedValue = op == "LIKE" ? $"'%{value}%'" :
                                    int.TryParse(value, out _) ? value : $"'{value}'";

            string condition = $"{field} {op} {formattedValue}";
            _conditions.Add(condition);
            ConditionsList.ItemsSource = null;
            ConditionsList.ItemsSource = _conditions;
            ValueEntry.Text = string.Empty;
        }

        private async void OnApplyFilterClicked(object sender, EventArgs e)
        {
            if (_conditions.Count == 0)
            {
                await DisplayAlert("Empty Filter", "No conditions were added.", "OK");
                return;
            }

            string joiner = CombinePicker.SelectedItem?.ToString() ?? "AND";
            string final = string.Join($" {joiner} ", _conditions);
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
