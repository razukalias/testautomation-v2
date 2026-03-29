using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Test_Automation.Models.Editor
{
    public class ConditionRow : INotifyPropertyChanged
    {
        private string _logicalOperator;
        private string _source;
        private string _variable;
        private string _operator;
        private string _expected;
        private string _action;

        public string LogicalOperator
        {
            get => _logicalOperator;
            set
            {
                if (_logicalOperator == value) return;
                _logicalOperator = value;
                OnPropertyChanged();
            }
        }

        public string Source
        {
            get => _source;
            set
            {
                if (_source == value) return;
                _source = value;
                OnPropertyChanged();
            }
        }

        public string Variable
        {
            get => _variable;
            set
            {
                if (_variable == value) return;
                _variable = value;
                OnPropertyChanged();
            }
        }

        public string Operator
        {
            get => _operator;
            set
            {
                if (_operator == value) return;
                _operator = value;
                OnPropertyChanged();
            }
        }

        public string Expected
        {
            get => _expected;
            set
            {
                if (_expected == value) return;
                _expected = value;
                OnPropertyChanged();
            }
        }

        public string Action
        {
            get => _action;
            set
            {
                if (_action == value) return;
                _action = value;
                OnPropertyChanged();
            }
        }

        public ConditionRow()
        {
            _logicalOperator = "And";
            _source = "PreviewOutput";
            _variable = string.Empty;
            _operator = "Equals";
            _expected = string.Empty;
            _action = "None";
        }

        public ConditionRow(string logicalOperator, string source, string variable, string @operator, string expected, string action)
        {
            _logicalOperator = logicalOperator;
            _source = source;
            _variable = variable;
            _operator = @operator;
            _expected = expected;
            _action = action;
        }

        public ConditionRow Clone()
        {
            return new ConditionRow(_logicalOperator, _source, _variable, _operator, _expected, _action);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}