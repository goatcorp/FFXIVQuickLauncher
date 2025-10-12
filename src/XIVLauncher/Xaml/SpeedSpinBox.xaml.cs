using System.Windows;
using System.Windows.Controls;

namespace XIVLauncher.Xaml
{
    public partial class SpeedSpinBox : UserControl
    {
        public event RoutedPropertyChangedEventHandler<double> ValueChanged;

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(SpeedSpinBox),
            new PropertyMetadata(0.0, OnValueChanged));

        public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
            nameof(Unit), typeof(string), typeof(SpeedSpinBox), new PropertyMetadata("MB/s"));

        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
            nameof(Step), typeof(double), typeof(SpeedSpinBox), new PropertyMetadata(1.0));

        public static readonly DependencyProperty MinProperty = DependencyProperty.Register(
            nameof(Min), typeof(double), typeof(SpeedSpinBox), new PropertyMetadata(0.0));

        public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
            nameof(Max), typeof(double), typeof(SpeedSpinBox), new PropertyMetadata(10000.0));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public double Step
        {
            get => (double)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public double Min
        {
            get => (double)GetValue(MinProperty);
            set => SetValue(MinProperty, value);
        }

        public double Max
        {
            get => (double)GetValue(MaxProperty);
            set => SetValue(MaxProperty, value);
        }

        public SpeedSpinBox()
        {
            InitializeComponent();
        }

        private void IncreaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Value + Step <= Max)
                Value += Step;
        }

        private void DecreaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Value - Step >= Min)
                Value -= Step;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (SpeedSpinBox)d;
            double oldValue = (double)e.OldValue;
            double newValue = (double)e.NewValue;
            control.ValueChanged?.Invoke(control, new RoutedPropertyChangedEventArgs<double>(oldValue, newValue));
        }
    }
}
